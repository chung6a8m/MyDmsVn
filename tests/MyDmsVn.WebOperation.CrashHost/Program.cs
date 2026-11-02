using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Owin.Testing;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Persistence;
using Newtonsoft.Json;

namespace MyDmsVn.WebOperation.CrashHost;

// Built only for the WEB5 test supervisor. No listener, production switch or SQL arrangement.
internal static class Program
{
    private const string Origin = "https://web2.mydmsvn.test";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !Regex.IsMatch(args[0], "\\A[A-Za-z0-9_-]{1,200}\\z")) return 64;
        ControlChannel? channel = null;
        NamedPipeClientStream? pipe = null;
        try
        {
            pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
            await pipe.ConnectAsync(ControlChannel.TimeoutMilliseconds).ConfigureAwait(false);
            channel = new ControlChannel(pipe);
            var input = await channel.ReadRequestAsync().ConfigureAwait(false);
            if (input.Method == "OPERATOR")
            {
                await TerminalOperation.RunAsync(channel, input).ConfigureAwait(false);
                pipe.Dispose();
                return 0;
            }
            var barrier = new CommitBarrier(channel, input);
            var services = new ServiceCollection();
            services.AddServerApplication().AddHttpRequestIdentity();
            services.AddSqlPersistence(input.ConnectionString);
            services.Replace(ServiceDescriptor.Singleton<IDbConnectionFactory>(new RestrictedConnectionFactory(input.ConnectionString)));
            CommitBarrier.Decorate(services, barrier);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var options = new WebHostOptions(Origin, Origin, "http-sql-tests", "sql-test",
                new Dictionary<string, byte[]> { ["sql-test"] = Encoding.UTF8.GetBytes("test-only-key-for-real-sql-http-auth-boundary") }, web4Enabled: true);
            var server = TestServer.Create(app => Web2Pipeline.Configure(app, options, provider));
            await channel.SendAsync(new { @event = "ready", processId = Process.GetCurrentProcess().Id }).ConfigureAwait(false);
            await channel.ExpectAsync("dispatch").ConfigureAwait(false);
            barrier.Arm();
            // Task.Run also bounds a synchronous Commit which cannot observe HTTP cancellation.
            // A timed-out child exits below; it never pretends cancellation proved rollback.
            var response = await ControlChannel.BoundedAsync(Task.Run(async () =>
            {
                using var request = new HttpRequestMessage(new HttpMethod(input.Method), Origin + "/api/v1/" + input.Path);
                request.Headers.TryAddWithoutValidation("Origin", Origin);
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
                request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", "http-sql-test");
                if (input.Access != null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + input.Access);
                foreach (var header in input.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                if (input.Body != null) request.Content = new StringContent(input.Body, Encoding.UTF8, "application/json");
                using var timeout = new CancellationTokenSource(ControlChannel.TimeoutMilliseconds);
                using var reply = await server.HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                return new { @event = "response", status = (int)reply.StatusCode, body = await reply.Content.ReadAsStringAsync().ConfigureAwait(false), headers = reply.Headers.Concat(reply.Content.Headers).ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase) };
            })).ConfigureAwait(false);
            await channel.SendAsync(response).ConfigureAwait(false);
            await ControlChannel.BoundedAsync(Task.Run(() => { server.Dispose(); provider.Dispose(); })).ConfigureAwait(false);
            pipe.Dispose();
            return 0;
        }
        catch (Exception error)
        {
            // The protocol is the only diagnostic surface. Never write exceptions or input bytes.
            if (channel != null)
            {
                try { await channel.SendAsync(new { @event = "error", category = Category(error) }, 1000).ConfigureAwait(false); }
                catch { /* A dead supervisor cannot receive a diagnostic. */ }
            }
            // Do not wait for a blocked SQL call or cleanup after a deadline/control failure.
            Environment.Exit(2);
            return 2;
        }
    }

    private static string Category(Exception error) => error is TimeoutException || error is OperationCanceledException ? "timeout"
        : error is JsonException || error is ArgumentException || error is InvalidDataException ? "protocol"
        : error is IOException ? "control-channel" : "host";
}
