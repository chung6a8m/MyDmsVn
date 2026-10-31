using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Owin;

namespace MyDmsVn.Server.HttpHost;

internal static class SpaAssets
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".js"] = "text/javascript; charset=utf-8", [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json", [".png"] = "image/png", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon", [".woff2"] = "font/woff2",
    };
    public static void Configure(IAppBuilder app, string directory, WebHostOptions options)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        app.Run(async context =>
        {
            if (!context.Request.IsSecure || context.Request.Host.Value != new Uri(options.PublicOrigin).Authority ||
                context.Request.Headers.Keys.Any(key => key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase) || key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)))
            { context.Response.StatusCode = 400; return; }
            if (context.Request.Method != "GET" && context.Request.Method != "HEAD") { context.Response.StatusCode = 405; return; }
            var path = context.Request.Path.Value;
            if (path.IndexOf('\\') >= 0 || path.IndexOf('\0') >= 0 || path.Split('/').Any(segment => segment == ".." || segment == "."))
            { context.Response.StatusCode = 404; return; }
            var file = Path.GetFullPath(Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { context.Response.StatusCode = 404; return; }
            // Unknown asset filenames never become HTML; extensionless client routes use the SPA index.
            if (!File.Exists(file) && Path.GetExtension(path).Length == 0) file = Path.Combine(root, "index.html");
            if (!File.Exists(file) || !ContentTypes.TryGetValue(Path.GetExtension(file), out var contentType))
            { context.Response.StatusCode = 404; return; }
            context.Response.ContentType = contentType;
            context.Response.Headers.Set("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Set("Strict-Transport-Security", "max-age=31536000");
            context.Response.Headers.Set("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'none'");
            context.Response.Headers.Set("Cache-Control", "no-store");
            context.Response.StatusCode = 200;
            if (context.Request.Method != "HEAD")
            {
                using var stream = File.OpenRead(file);
                await stream.CopyToAsync(context.Response.Body, 81920, context.Request.CallCancelled).ConfigureAwait(false);
            }
        });
    }
}
