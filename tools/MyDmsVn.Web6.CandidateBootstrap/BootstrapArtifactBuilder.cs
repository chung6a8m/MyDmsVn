using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapArtifactBuilder
{
    private const string MarkerPath = "web6-candidate-build.json";
    private const string MarkerPrefix = "/* MyDmsVn WEB6 build marker ";
    private const string Host = "MyDmsVn.Server.HttpHost";
    private readonly BootstrapToolchain.BootstrapTools _tools;

    internal BootstrapArtifactBuilder(BootstrapToolchain.BootstrapTools tools) => _tools = tools;

    internal async Task RestoreFrontendAsync(BootstrapSource frontend, CancellationToken token)
    {
        await _tools.NpmAsync(frontend.Path, token, new[] { "ci", "--ignore-scripts", "--include=dev", "--no-audit", "--no-fund" }, build: true).ConfigureAwait(false);
    }

    internal async Task<(BootstrapTree Tree, string ManifestDigest)> BuildFrontendAsync(BootstrapSource frontend,
        string? candidate, string? manifestPath, CancellationToken token)
    {
        await _tools.NpmAsync(frontend.Path, token, new[] { "--ignore-scripts", "run", "build" }, candidate, build: true).ConfigureAwait(false);
        var tree = new BootstrapTree(Path.Combine(frontend.Path, "dist"));
        if (!tree.Files.ContainsKey("index.html")) throw new IOException("Bootstrap frontend is missing its entry HTML.");
        if (manifestPath == null)
        {
            // The pinned WEB4 archive predates the WEB6 build marker and verifier.
            if (tree.Files.ContainsKey(MarkerPath)) throw new IOException("Bootstrap archive unexpectedly contains a WEB6 build marker.");
            return (tree, "");
        }

        var mode = candidate == null ? "ordinary" : "candidate";
        var args = new List<string> { "--ignore-scripts", "run", "web6:artifact", "--", "--dist", tree.Root, "--mode", mode, "--output", manifestPath };
        if (candidate != null) args.AddRange(new[] { "--candidate-id", candidate });
        await _tools.NpmAsync(frontend.Path, token, args.ToArray()).ConfigureAwait(false);
        tree.Verify();
        var manifestDigest = VerifyManifest(manifestPath, tree, mode, candidate);
        VerifyMarkers(tree, candidate);
        tree.Verify();
        return (tree, manifestDigest);
    }

    internal async Task<BootstrapTree> BuildBackendAsync(BootstrapSource backend, string publish,
        BootstrapTree? frontend, CancellationToken token)
    {
        var hostDirectory = Path.Combine(backend.Path, "src", Host);
        var project = Path.Combine(hostDirectory, Host + ".csproj");
        var webConfig = Path.Combine(hostDirectory, "Web.config");
        var configHash = BootstrapPathPolicy.HashFile(webConfig);
        var bin = Path.Combine(publish, "bin");
        using var target = new BootstrapPathPolicy.PinnedDirectory(bin, create: true);
        await _tools.RunAsync("dotnet", backend.Path, token,
            new[] { "restore", project, "--locked-mode", "--disable-build-servers", "-p:Configuration=Release" }, build: true).ConfigureAwait(false);
        await _tools.RunAsync("dotnet", backend.Path, token,
            new[] { "build", project, "--configuration", "Release", "--no-restore", "--disable-build-servers", "--output", bin, "-nr:false" }, build: true).ConfigureAwait(false);
        target.Verify();
        var builtBin = new BootstrapTree(bin);
        if (!builtBin.Files.ContainsKey(Host + ".dll")) throw new IOException("Bootstrap build is missing its HTTP host assembly.");
        var config = MergeWebConfig(webConfig, configHash, builtBin);
        BootstrapPathPolicy.WriteNewFile(Path.Combine(publish, "Web.config"), output => output.Write(config, 0, config.Length));
        var before = new BootstrapTree(publish);
        if (!before.Files.TryGetValue("Web.config", out var configDigest) || configDigest != ProtectedJsonFile.Digest(config))
            throw new IOException("Bootstrap published Web.config differs from the merged configuration.");
        if (frontend != null) frontend.CopyTo(Path.Combine(publish, "wwwroot"));
        var after = new BootstrapTree(publish);
        builtBin.Verify();
        before.VerifyExtension(after, "wwwroot");
        return after;
    }

    private static byte[] MergeWebConfig(string sourcePath, string sourceHash, BootstrapTree builtBin)
    {
        const string generatedPath = Host + ".dll.config";
        if (!builtBin.Files.TryGetValue(generatedPath, out var generatedHash))
            throw new IOException("Bootstrap host build is missing the generated runtime configuration.");

        // System.Web loads the host's runtime configuration from Web.config. The
        // generated assembly redirects must be imported from this exact build.
        var source = BootstrapPathPolicy.ReadFile(sourcePath, file => ReadConfig(file, sourceHash)).Value;
        var generated = builtBin.ReadFile(generatedPath, file => ReadConfig(file, generatedHash));
        source.DocumentElement!.ReplaceChild(source.ImportNode(RequireRuntime(generated), deep: true), RequireRuntime(source));
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize }))
            source.Save(writer);
        return output.ToArray();
    }

    private static XmlDocument ReadConfig(FileStream file, string expectedHash)
    {
        if (BootstrapPathPolicy.HashStream(file) != expectedHash)
            throw new IOException("Bootstrap host configuration changed after measurement.");
        file.Position = 0;
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        try { document.Load(reader); }
        catch (XmlException) { throw new IOException("Bootstrap host configuration contains invalid XML."); }
        return document;
    }

    private static XmlNode RequireRuntime(XmlDocument document)
    {
        if (document.DocumentElement is not { Name: "configuration", NamespaceURI: "" } configuration ||
            configuration.SelectNodes("runtime") is not { Count: 1 } runtime)
            throw new IOException("Bootstrap host configuration requires exactly one runtime element.");
        return runtime[0]!;
    }

    private static string VerifyManifest(string path, BootstrapTree tree, string mode, string? candidate)
    {
        var payload = new JObject
        {
            ["version"] = 1, ["mode"] = mode, ["candidateId"] = candidate,
            ["files"] = new JArray(tree.Files.Select(file => new JObject { ["path"] = file.Key, ["sha256"] = file.Value })),
        };
        // Task 1's canonical JSON uses JSON.stringify. Its property order is part of the format.
        payload["manifestSha256"] = ProtectedJsonFile.Digest(BootstrapJson.Serialize(payload));
        var expected = BootstrapJson.Serialize(payload);
        return BootstrapPathPolicy.ReadFile(path, file =>
        {
            if (file.Length != expected.Length) throw new IOException("Bootstrap frontend manifest size differs from the measured artifact.");
            using var bytes = new MemoryStream();
            file.CopyTo(bytes);
            if (!bytes.ToArray().SequenceEqual(expected))
                throw new IOException("Bootstrap frontend manifest mode, candidate ID or inventory differs.");
            return ProtectedJsonFile.Digest(expected);
        }).Value;
    }

    private static void VerifyMarkers(BootstrapTree tree, string? candidate)
    {
        var marker = new JObject { ["version"] = 1, ["candidateId"] = candidate }.ToString(Newtonsoft.Json.Formatting.None);
        if (!tree.Files.ContainsKey(MarkerPath) || ReadText(tree, MarkerPath) != marker)
            throw new IOException("Bootstrap frontend build marker differs from the approved mode.");
        var markers = 0;
        foreach (var js in tree.Files.Keys.Where(path => path.EndsWith(".js", StringComparison.Ordinal)))
        {
            var script = ReadText(tree, js);
            var index = script.IndexOf(MarkerPrefix, StringComparison.Ordinal);
            if (index < 0) continue;
            if (index == 0 || script.Substring(index - 1) != "\n" + MarkerPrefix + marker + " */\n" || ++markers != 1)
                throw new IOException("Bootstrap JavaScript build marker differs from the approved mode.");
        }
        if (markers != 1) throw new IOException("Bootstrap JavaScript build marker is missing.");
    }

    private static string ReadText(BootstrapTree tree, string path) => tree.ReadFile(path, file =>
    {
        if (file.Length > 32 * 1024 * 1024) throw new IOException("Bootstrap frontend entry exceeds the measurement budget.");
        using var reader = new StreamReader(file, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        return reader.ReadToEnd();
    });
}
