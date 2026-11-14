using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.HttpHost;

internal sealed class SecurityAuditReadiness : ISecurityAuditReadiness
{
    private SecurityAuditReadinessSnapshot _snapshot = new();

    public SecurityAuditReadinessSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public void MarkDegraded(DateTime failedAtUtc) =>
        Volatile.Write(ref _snapshot, new SecurityAuditReadinessSnapshot(failedAtUtc));
}

internal sealed class SecurityAuditReadinessFilter : ActionFilterAttribute
{
    private readonly ISecurityAuditReadiness _readiness;

    public SecurityAuditReadinessFilter(ISecurityAuditReadiness readiness) => _readiness = readiness;

    public override void OnActionExecuting(HttpActionContext actionContext)
    {
        if (actionContext.ControllerContext.ControllerDescriptor.ControllerType != typeof(HealthController))
            return;

        var snapshot = _readiness.Snapshot;
        if (snapshot.Status == SecurityAuditReadinessStatus.Degraded)
        {
            var correlation = actionContext.Request.GetOwinContext().Get<string>(Web2Pipeline.CorrelationKey);
            actionContext.Response = HttpJson.Error((int)HttpStatusCode.ServiceUnavailable, snapshot.ReasonCode!,
                "Required security audit persistence is unavailable.", correlation);
        }
    }
}
