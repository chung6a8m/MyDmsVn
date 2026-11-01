using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Http.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin;
using MyDmsVn.Server.Application.Identity;
using Owin;

namespace MyDmsVn.Server.HttpHost;

public static class Web2Pipeline
{
    internal const string ScopeKey = "mydmsvn.requestServices";
    internal const string CorrelationKey = "mydmsvn.correlation";
    public static void Configure(IAppBuilder app, WebHostOptions options, IServiceProvider services, Action<HttpConfiguration>? configureApi = null)
    {
        var limiter = new AuthAttemptLimiter();
        var config = new HttpConfiguration { IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Never };
        config.Services.Replace(typeof(IHttpControllerActivator), new ScopedControllerActivator());
        config.MessageHandlers.Add(new ApiBoundaryHandler());
        configureApi?.Invoke(config);
        config.MapHttpAttributeRoutes();
        config.EnsureInitialized();
        if (app.Properties.TryGetValue("host.OnAppDisposing", out var disposing) && disposing is CancellationToken stop)
            stop.Register(config.Dispose);
        app.MapWhen(context => context.Request.Path.StartsWithSegments(new PathString("/api")), branch =>
        {
            branch.Use(async (context, next) =>
            {
                var correlation = SafeCorrelation(context.Request.Headers.GetValues("X-Correlation-ID"));
                context.Set(CorrelationKey, correlation);
                SetHeaders(context.Response, correlation);
                if (HttpContext.Current != null)
                {
                    HttpContext.Current.Response.SuppressFormsAuthenticationRedirect = true;
                    HttpContext.Current.Response.TrySkipIisCustomErrors = true;
                }
                try
                {
                    var guard = CheckTransport(context, options);
                    if (guard != null) { using (guard) await WriteAsync(context, guard); return; }
                    if (long.TryParse(context.Request.Headers.Get("Content-Length"), out var contentLength) && contentLength > options.MaximumRequestBytes)
                    { using var large = HttpJson.Error(413, "Request.TooLarge", "The request is too large.", correlation); await WriteAsync(context, large); return; }
                    using var body = new MemoryStream();
                    var buffer = new byte[8192];
                    while (true)
                    {
                        var read = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length, context.Request.CallCancelled).ConfigureAwait(false);
                        if (read == 0) break;
                        if (body.Length + read > options.MaximumRequestBytes)
                        { using var large = HttpJson.Error(413, "Request.TooLarge", "The request is too large.", correlation); await WriteAsync(context, large); return; }
                        await body.WriteAsync(buffer, 0, read, context.Request.CallCancelled).ConfigureAwait(false);
                    }
                    body.Position = 0; context.Request.Body = body;
                    using var scope = services.CreateScope();
                    var admission = scope.ServiceProvider.GetRequiredService<RequestIdentityAdmission>();
                    admission.BeginCorrelation(correlation);
                    var actor = CurrentUser.Anonymous;
                    Guid? sessionId = null;
                    var cookieEndpoint = context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && IsCookieEndpoint(context.Request.Path.Value);
                    var anonymous = cookieEndpoint || context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && context.Request.Path.Value == "/api/v1/health";
                    var authorization = context.Request.Headers.GetValues("Authorization");
                    if (!anonymous || !cookieEndpoint && authorization != null)
                    {
                        var token = ReadBearer(authorization);
                        var valid = token == null ? null : new BearerTokenValidator(options, scope.ServiceProvider.GetRequiredService<IUtcClock>()).Validate(token);
                        if (valid == null)
                        { using var denied = Unauthorized(correlation); await WriteAsync(context, denied); return; }
                        var user = await scope.ServiceProvider.GetRequiredService<IWebAuthSessionStore>()
                            .GetActiveUserAsync(valid.SessionId, valid.UserId, context.Request.CallCancelled).ConfigureAwait(false);
                        if (user == null || !user.IsAuthenticated || !user.IsActive || user.UserId != valid.UserId)
                        { using var denied = Unauthorized(correlation); await WriteAsync(context, denied); return; }
                        actor = user;
                        sessionId = valid.SessionId;
                    }
                    admission.Admit(actor, sessionId);
                    context.Set(ScopeKey, scope.ServiceProvider);
                    context.Set(AuthController.OptionsKey, options);
                    context.Set(AuthController.LimiterKey, limiter);
                    await next().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (context.Request.CallCancelled.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // Never emit exception text, SQL, credentials, claims or token material.
                    using var failure = HttpJson.Error(500, "InternalError", "An unexpected error occurred.", correlation);
                    await WriteAsync(context, failure).ConfigureAwait(false);
                }
            });
            branch.UseWebApi(config);
            // Web API's OWIN adapter passes unmatched routes to the next middleware.
            // Keep /api failures in the JSON boundary instead of the SPA/IIS fallback.
            branch.Run(async context =>
            {
                using var missing = HttpJson.Error(404, "NotFound", "The resource was not found.", context.Get<string>(CorrelationKey));
                await WriteAsync(context, missing).ConfigureAwait(false);
            });
        });
    }
    private static HttpResponseMessage Unauthorized(string correlation)
    {
        var response = HttpJson.Error(401, "Auth.Unauthorized", "Authentication is required.", correlation);
        response.Headers.WwwAuthenticate.Add(new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer"));
        return response;
    }
    private static string? ReadBearer(IList<string>? values)
    {
        if (values == null || values.Count != 1) return null;
        var text = values[0];
        if (!text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || text.Length <= 7) return null;
        var token = text.Substring(7);
        return token.Any(c => char.IsWhiteSpace(c) || c == ',') ? null : token;
    }
    private static string SafeCorrelation(IList<string>? values) => values?.Count == 1 && Regex.IsMatch(values[0], "\\A[A-Za-z0-9_-]{1,128}\\z")
        ? values[0] : Guid.NewGuid().ToString("N");
    private static HttpResponseMessage? CheckTransport(IOwinContext context, WebHostOptions options)
    {
        var correlation = context.Get<string>(CorrelationKey);
        if (!context.Request.IsSecure || context.Request.Host.Value != new Uri(options.PublicOrigin).Authority ||
            context.Request.Headers.Keys.Any(key => key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase) || key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)))
            return HttpJson.Error(400, "Request.InvalidTransport", "The request transport is invalid.", correlation);
        var origins = context.Request.Headers.GetValues("Origin");
        var safeMethod = context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) || context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
        var sites = context.Request.Headers.GetValues("Sec-Fetch-Site");
        if ((!safeMethod && origins == null) || (origins != null && (origins.Count != 1 || origins[0] != options.PublicOrigin)) ||
            (!safeMethod && sites != null && (sites.Count != 1 || sites[0] != "same-origin")))
            return HttpJson.Error(403, "Auth.Forbidden", "Access is denied.", correlation);
        if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && IsCookieEndpoint(context.Request.Path.Value))
        {
            var requested = context.Request.Headers.GetValues("X-Requested-With");
            if (requested == null || requested.Count != 1 || requested[0] != "XMLHttpRequest")
                return HttpJson.Error(403, "Auth.Forbidden", "Access is denied.", correlation);
        }
        return null;
    }
    private static bool IsCookieEndpoint(string path)
    {
        // Attribute routes ignore case and trailing slashes; admission must cover the same paths.
        var route = path.TrimEnd('/');
        return route.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) ||
            route.Equals("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            route.Equals("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase);
    }
    private static void SetHeaders(IOwinResponse response, string correlation)
    {
        response.Headers.Set("X-Correlation-ID", correlation);
        response.Headers.Set("Cache-Control", "no-store");
        response.Headers.Set("Pragma", "no-cache");
        response.Headers.Set("Expires", "0");
        response.Headers.Set("X-Content-Type-Options", "nosniff");
        response.Headers.Set("Strict-Transport-Security", "max-age=31536000");
        response.Headers.Set("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    }
    private static async Task WriteAsync(IOwinContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers) context.Response.Headers.SetValues(header.Key, header.Value.ToArray());
        if (response.Content != null)
        {
            context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length, context.Request.CallCancelled).ConfigureAwait(false);
        }
    }
    private sealed class ScopedControllerActivator : IHttpControllerActivator
    {
        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            var provider = request.GetOwinContext().Get<IServiceProvider>(ScopeKey);
            return (IHttpController)(provider.GetService(controllerType) ?? ActivatorUtilities.CreateInstance(provider, controllerType));
        }
    }
    private sealed class ApiBoundaryHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var context = request.GetOwinContext();
            request.Properties[HttpPropertyKeys.DependencyScope] = new RequestDependencyScope(context.Get<IServiceProvider>(ScopeKey));
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 300 && !(response is ApiHttpResponseMessage))
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                var code = status == 401 ? "Auth.Unauthorized" : status == 403 ? "Auth.Forbidden" : status == 404 ? "NotFound" : status >= 500 ? "InternalError" : "Request.Invalid";
                var message = status == 401 ? "Authentication is required." : status == 403 ? "Access is denied." : status == 404 ? "The resource was not found." : status >= 500 ? "An unexpected error occurred." : "The request is invalid.";
                return HttpJson.Error(status >= 300 && status < 400 ? 500 : status, code, message, context.Get<string>(CorrelationKey));
            }
            return response;
        }
    }
}

[RoutePrefix("api/v1")]
public sealed class HealthController : ApiController
{
    [HttpGet, Route("health")]
    public HttpResponseMessage Get() => HttpJson.Success(new { status = "ok" });
}
