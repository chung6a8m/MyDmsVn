using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;

namespace MyDmsVn.Server.HttpHost;

[RoutePrefix("api/v3/operations")]
public sealed class Web6ResendEligibilityController : ApiController
{
    private readonly IRequestCorrelationAccessor _correlation;
    public Web6ResendEligibilityController(IRequestCorrelationAccessor correlation) => _correlation = correlation;

    [HttpGet, Route("resend-context", Order = 0)]
    public Task<HttpResponseMessage> Context(CancellationToken token) => Read(async (provider, options) =>
    {
        WebOperationResolutionWire.Path(Request, "/api/v3/operations/resend-context");
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var identity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(token).ConfigureAwait(false);
        uow.BeginTransaction();
        var epoch = await uow.Repository<IWebOperationReadRepository>().ReadContextAsync(identity.Current.UserId!.Value,
            identity.SessionId!.Value, token).ConfigureAwait(false);
        if (epoch == Guid.Empty) throw new WebOperationException("Operation.Unavailable");
        var clock = provider.GetRequiredService<IUtcClock>();
        var response = HttpJson.Success(Web6ResendEligibilityWire.Context(options, epoch, identity.Current.UserId.Value, clock.UtcNow));
        try { uow.Commit(); return response; } catch { response.Dispose(); throw; }
    }, token);

    [HttpGet, Route("{operationId}/resend-receipt", Order = 1)]
    public Task<HttpResponseMessage> Receipt(string operationId, CancellationToken token) => Read(async (provider, options) =>
    {
        var operation = WebOperationWireInput.OperationId(operationId);
        WebOperationResolutionWire.Path(Request, "/api/v3/operations/" + operation.ToString("D") + "/resend-receipt");
        var epoch = WebOperationWireInput.DeploymentEpoch(Request);
        var fingerprint = WebOperationWireInput.StatusFingerprint(Request);
        var kind = WebOperationWireInput.ResendKind(Request);
        var target = WebOperationWireInput.ResendTarget(Request, kind);
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var requestIdentity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(token).ConfigureAwait(false);
        uow.BeginTransaction();
        await uow.Repository<IWebOperationResendEligibilityRepository>().AuthorizeReceiptAsync(requestIdentity.Current.UserId!.Value,
            requestIdentity.SessionId!.Value, epoch, kind, token).ConfigureAwait(false);
        var identity = new Web6ResendIdentity(requestIdentity.Current.UserId.Value, options.Web6CandidateId!, epoch,
            operation, kind, target, fingerprint);
        var issued = new Web6ResendReceiptCodec(options, provider.GetRequiredService<IUtcClock>()).Issue(identity);
        var response = HttpJson.Success(Web6ResendEligibilityWire.Receipt(identity, issued));
        try { uow.Commit(); return response; } catch { response.Dispose(); throw; }
    }, token);

    private async Task<HttpResponseMessage> Read(Func<IServiceProvider, WebHostOptions, Task<HttpResponseMessage>> read, CancellationToken token)
    {
        try
        {
            var context = Request.GetOwinContext();
            var options = context.Get<WebHostOptions>(AuthController.OptionsKey);
            if (!options.Web5Enabled || !options.Web6ResendEligibilityEnabled || options.Web6CandidateId == null)
                return HttpJson.Error(404, "NotFound", "The resource was not found.", _correlation.CorrelationId!);
            return await read(context.Get<IServiceProvider>(Web2Pipeline.ScopeKey), options).ConfigureAwait(false);
        }
        catch (WireInputException) { return HttpJson.Error(400, "ValidationError", "The request is invalid.", _correlation.CorrelationId!); }
        catch (WebOperationException error) { return WebOperationHttpErrors.Map(error.Code, _correlation.CorrelationId); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return WebOperationHttpErrors.Map("Operation.Unavailable", _correlation.CorrelationId); }
    }

    private static RequestIdentityAdmission Identity(IServiceProvider provider)
    {
        var identity = provider.GetRequiredService<RequestIdentityAdmission>();
        if (!identity.Current.IsAuthenticated || !identity.Current.IsActive || !identity.Current.UserId.HasValue || !identity.SessionId.HasValue)
            throw new WebOperationException("Auth.Unauthorized");
        return identity;
    }
}
