using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dependencies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.HttpHost;

public static class RequestServices
{
    public static IServiceCollection AddHttpRequestIdentity(this IServiceCollection services)
    {
        services.AddScoped<RequestIdentityAdmission>();
        // Application's default writer is singleton because its default correlation accessor is singleton.
        // HTTP correlation belongs to a request; preserve the implementation and narrow its lifetime.
        var auditWriter = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ISecurityAuditWriter));
        if (auditWriter?.ImplementationType != null)
            services.Replace(ServiceDescriptor.Scoped(typeof(ISecurityAuditWriter), auditWriter.ImplementationType));
        services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(provider =>
            new HttpCurrentUserAccessor(provider.GetRequiredService<RequestIdentityAdmission>().Current)));
        services.Replace(ServiceDescriptor.Scoped<IRequestCorrelationAccessor>(provider =>
            new HttpCorrelationAccessor(provider.GetRequiredService<RequestIdentityAdmission>().CorrelationId)));
        return services;
    }
}
internal sealed class RequestIdentityAdmission
{
    private CurrentUser? _current;
    public CurrentUser Current => _current ?? throw new InvalidOperationException("HTTP identity has not been admitted.");
    private string? _correlation;
    public string CorrelationId => _correlation ?? throw new InvalidOperationException("HTTP correlation has not been initialized.");
    public void BeginCorrelation(string correlationId)
    {
        if (_correlation != null) throw new InvalidOperationException("HTTP correlation was already initialized.");
        _correlation = correlationId;
    }
    public void Admit(CurrentUser current)
    {
        if (_current != null) throw new InvalidOperationException("HTTP identity was already admitted.");
        _current = current;
    }
}
public sealed class HttpCurrentUserAccessor : ICurrentUserAccessor
{
    public HttpCurrentUserAccessor(CurrentUser current) { Current = current ?? throw new ArgumentNullException(nameof(current)); }
    public CurrentUser Current { get; }
}
internal sealed class HttpCorrelationAccessor : IRequestCorrelationAccessor
{
    public HttpCorrelationAccessor(string correlationId) { CorrelationId = correlationId; }
    public string CorrelationId { get; }
}
internal sealed class RequestDependencyScope : IDependencyScope
{
    private readonly IServiceProvider _provider;
    public RequestDependencyScope(IServiceProvider provider) { _provider = provider; }
    public object? GetService(Type serviceType) => _provider.GetService(serviceType);
    public IEnumerable<object> GetServices(Type serviceType) => _provider.GetServices(serviceType).Where(value => value != null)!;
    // OWIN owns the scope until downstream completion, including cancellation/error.
    public void Dispose() { }
}
