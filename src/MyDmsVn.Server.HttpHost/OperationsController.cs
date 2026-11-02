using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

[RoutePrefix("api/v1/operations")]
public sealed class OperationsController : ApiController
{
    private readonly IRequestCorrelationAccessor _correlation;
    public OperationsController(IRequestCorrelationAccessor correlation) => _correlation = correlation;

    [HttpGet, Route("context", Order = 0)]
    public Task<HttpResponseMessage> Context(CancellationToken cancellationToken) => Read(async provider =>
    {
        WebOperationWireInput.ContextPath(Request);
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var identity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        var epoch = await uow.Repository<IWebOperationReadRepository>().ReadContextAsync(identity.Current.UserId!.Value,
            identity.SessionId!.Value, cancellationToken).ConfigureAwait(false);
        if (epoch == Guid.Empty) throw new WebOperationException("Operation.Unavailable");
        // Serialize while epoch/account/session authority is still held by the read transaction.
        var response = HttpJson.Success(new { deploymentEpoch = epoch.ToString("D"), actorUserId = identity.Current.UserId.Value, contractVersion = "web4-v1" });
        try { uow.Commit(); return response; }
        catch { response.Dispose(); throw; }
    }, cancellationToken);

    [HttpGet, Route("{operationId}", Order = 1)]
    public Task<HttpResponseMessage> Status(string operationId, CancellationToken cancellationToken) => Read(async provider =>
    {
        var key = WebOperationWireInput.OperationId(operationId);
        WebOperationWireInput.StatusPath(Request, key);
        var epoch = WebOperationWireInput.DeploymentEpoch(Request);
        var fingerprint = WebOperationWireInput.StatusFingerprint(Request);
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        var identity = Identity(provider);
        using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        var status = await uow.Repository<IWebOperationReadRepository>().ReadStatusAsync(identity.Current.UserId!.Value,
            identity.SessionId!.Value, epoch, key, fingerprint, cancellationToken).ConfigureAwait(false);
        if (status == null) throw new WebOperationException("Operation.NotObserved");
        var response = HttpJson.Success(Envelope(status, epoch, key, fingerprint));
        try { uow.Commit(); return response; }
        catch { response.Dispose(); throw; }
    }, cancellationToken);

    private async Task<HttpResponseMessage> Read(Func<IServiceProvider, Task<HttpResponseMessage>> read, CancellationToken cancellationToken)
    {
        try
        {
            var context = Request.GetOwinContext();
            if (!context.Get<WebHostOptions>(AuthController.OptionsKey).Web4Enabled)
                return HttpJson.Error(404, "NotFound", "The resource was not found.", _correlation.CorrelationId!);
            return await read(context.Get<IServiceProvider>(Web2Pipeline.ScopeKey)).ConfigureAwait(false);
        }
        catch (WireInputException)
        {
            return HttpJson.Error(400, "ValidationError", "The request is invalid.", _correlation.CorrelationId!);
        }
        catch (WebOperationException error) { return WebOperationHttpErrors.Map(error.Code, _correlation.CorrelationId); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return WebOperationHttpErrors.Map("Operation.Unavailable", _correlation.CorrelationId); }
    }

    private static RequestIdentityAdmission Identity(IServiceProvider provider)
    {
        var identity = provider.GetRequiredService<RequestIdentityAdmission>();
        if (!identity.Current.IsAuthenticated || !identity.Current.IsActive || !identity.Current.UserId.HasValue || !identity.SessionId.HasValue)
            throw new WebOperationException("Auth.Unauthorized");
        return identity;
    }

    private static JObject Envelope(WebOperationStatus status, Guid epoch, Guid key, byte[] fingerprint)
    {
        var create = status.Kind == WebOperationKind.SalesCreate || status.Kind == WebOperationKind.ReceiptCreate;
        if (status.DeploymentEpoch != epoch || status.OperationId != key || !status.RequestFingerprint.SequenceEqual(fingerprint) ||
            (create ? status.RequestTargetId.HasValue : !status.RequestTargetId.HasValue || status.RequestTargetId.Value <= 0))
            throw new WebOperationException("Operation.Unavailable");
        var state = status.State;
        JObject? outcome = null;
        if (state == "COMMITTED")
        {
            try
            {
                outcome = new WebOperationResultCodec().DecodeEnvelope(status.Kind, status.ResultBytes!);
                var sales = status.Kind == WebOperationKind.SalesCreate || status.Kind == WebOperationKind.SalesUpdate || status.Kind == WebOperationKind.SalesPost;
                var resultId = (string?)outcome["data"]![sales ? "saleId" : "receiptId"];
                if (!status.ResultDocumentId.HasValue || resultId != status.ResultDocumentId.Value.ToString(CultureInfo.InvariantCulture) ||
                    (!create && status.ResultDocumentId != status.RequestTargetId))
                    throw new WebOperationException("Operation.ResultUnavailable");
            }
            catch (WebOperationException error) when (error.Code == "Operation.ResultUnavailable")
            {
                state = "RESULT_UNAVAILABLE";
                outcome = null;
            }
        }
        else if (state != "RESULT_EXPIRED" && state != "RESULT_UNAVAILABLE")
            throw new WebOperationException("Operation.Unavailable");
        return new JObject
        {
            ["deploymentEpoch"] = status.DeploymentEpoch.ToString("D"), ["operationId"] = status.OperationId.ToString("D"),
            ["operationKind"] = WebOperationWireInput.KindName(status.Kind), ["method"] = WebOperationWireInput.Method(status.Kind),
            ["routeScope"] = WebOperationWireInput.RouteScope(status.Kind),
            ["requestTargetId"] = status.RequestTargetId.HasValue ? new JValue(status.RequestTargetId.Value.ToString(CultureInfo.InvariantCulture)) : JValue.CreateNull(),
            ["requestFingerprint"] = WebOperationWireInput.FingerprintHex(status.RequestFingerprint), ["state"] = state,
            ["outcome"] = outcome == null ? JValue.CreateNull() : (JToken)outcome
        };
    }
}
