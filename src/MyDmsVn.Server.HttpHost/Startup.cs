using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application.Identity;
using Microsoft.Owin;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Operations;
using Owin;

[assembly: OwinStartup(typeof(MyDmsVn.Server.HttpHost.Startup))]
namespace MyDmsVn.Server.HttpHost;

public sealed class Startup
{
    public void Configuration(IAppBuilder app)
    {
        ServiceProvider? provider = null;
        try
        {
            var options = WebHostOptions.FromEnvironment();
            var sessionOptions = WebAuthSessionConfiguration.Read(Environment.GetEnvironmentVariable);
            var connection = Environment.GetEnvironmentVariable("MYDMSVN_WEB_SQL_CONNECTION_STRING");
            var superuser = Environment.GetEnvironmentVariable("MYDMSVN_WEB_SUPERUSER_USERNAME");
            if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(superuser))
                throw new InvalidOperationException();
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var compatibility = new SqlWebSchemaCompatibilityChecker(connection!).CheckAsync(timeout.Token).GetAwaiter().GetResult();
                if (!compatibility.IsCompatible) throw new InvalidOperationException();
                new SqlWebRuntimePrincipalValidator().ValidateAsync(connection!, timeout.Token).GetAwaiter().GetResult();
                new SqlSuperuserBindingValidator().ValidateAsync(connection!, superuser!, timeout.Token).GetAwaiter().GetResult();
            }
            var services = new ServiceCollection();
            services.AddServerApplication(superuser).AddHttpRequestIdentity();
            services.AddSingleton(sessionOptions);
            services.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink, TraceSecurityAuditSink>());
            services.AddSqlPersistence(connection!);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Web2Pipeline.Configure(app, options, provider);
            SpaAssets.Configure(app, System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"), options);
            if (app.Properties.TryGetValue("host.OnAppDisposing", out var disposing) && disposing is CancellationToken stop)
                stop.Register(provider.Dispose);
            else throw new InvalidOperationException();
        }
        catch (Exception)
        {
            provider?.Dispose();
            // A deployment/configuration/SQL failure must not install a working anonymous API.
            // Do not preserve an inner exception that may contain operator credentials.
            throw new InvalidOperationException("WEB2 host startup was refused. Verify the restricted web deployment configuration.");
        }
    }
}
