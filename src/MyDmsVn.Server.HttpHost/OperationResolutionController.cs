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

[RoutePrefix("api/v2/operations")]
public sealed class OperationResolutionController : ApiController
{
    private readonly IRequestCorrelationAccessor _correlation;
    public OperationResolutionController(IRequestCorrelationAccessor correlation) { _correlation = correlation; }

    [HttpGet, Route("context", Order = 0)]
    public Task<HttpResponseMessage> Context(CancellationToken token) => Read(async provider =>
    {
        WebOperationResolutionWire.Path(Request, "/api/v2/operations/context");
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var identity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(token).ConfigureAwait(false);
        uow.BeginTransaction();
        var epoch = await uow.Repository<IWebOperationReadRepository>().ReadContextAsync(identity.Current.UserId!.Value,
            identity.SessionId!.Value, token).ConfigureAwait(false);
        if (epoch == Guid.Empty) throw new WebOperationException("Operation.Unavailable");
        var response = HttpJson.Success(new { contractVersion = WebOperationResolutionWire.ContractVersion,
            deploymentEpoch = epoch.ToString("D"), actorUserId = identity.Current.UserId.Value, sameKeyResendEnabled = false });
        try { uow.Commit(); return response; } catch { response.Dispose(); throw; }
    }, token);

    [HttpGet, Route("{operationId}/resolution", Order = 1)]
    public Task<HttpResponseMessage> Resolution(string operationId, CancellationToken token) => Read(async provider =>
    {
        var key = WebOperationWireInput.OperationId(operationId);
        WebOperationResolutionWire.Path(Request, "/api/v2/operations/" + key.ToString("D") + "/resolution");
        var epoch = WebOperationWireInput.DeploymentEpoch(Request);
        var fingerprint = WebOperationWireInput.StatusFingerprint(Request);
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var identity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(token).ConfigureAwait(false);
        uow.BeginTransaction();
        var resolution = await uow.Repository<IWebOperationResolutionRepository>().ReadResolutionAsync(identity.Current.UserId!.Value,
            identity.SessionId!.Value, epoch, key, fingerprint, token).ConfigureAwait(false);
        if (resolution == null) throw new WebOperationException("Operation.NotObserved");
        var response = HttpJson.Success(WebOperationResolutionWire.Envelope(resolution, identity.Current.UserId.Value, epoch, key, fingerprint));
        try { uow.Commit(); return response; } catch { response.Dispose(); throw; }
    }, token);

    private async Task<HttpResponseMessage> Read(Func<IServiceProvider, Task<HttpResponseMessage>> read, CancellationToken token)
    {
        try
        {
            var context = Request.GetOwinContext();
            if (!context.Get<WebHostOptions>(AuthController.OptionsKey).Web5Enabled)
                return HttpJson.Error(404, "NotFound", "The resource was not found.", _correlation.CorrelationId!);
            return await read(context.Get<IServiceProvider>(Web2Pipeline.ScopeKey)).ConfigureAwait(false);
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
