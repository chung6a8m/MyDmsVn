using System;
using System.Diagnostics;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

// Optional BCF matrix declaration; the independent observer measures actual monitor DPI.
public sealed class LookupGateEnvironment : IDisposable
{
    private readonly GuiProcessProbeFixture? _probe;
    private readonly string? _scale = Environment.GetEnvironmentVariable("MYDMSVN_BCF_EXPECTED_SCALE");
    private readonly string? _theme = Environment.GetEnvironmentVariable("MYDMSVN_BCF_THEME");

    public LookupGateEnvironment()
    {
        if (_scale == null && _theme == null) return;
        Assert.True(_scale == "100" || _scale == "125", "Declare BCF native scale as 100 or 125.");
        Assert.True(_theme == "Light" || _theme == "Dark", "Declare BCF theme as Light or Dark.");
        _probe = new GuiProcessProbeFixture(package: false, inline: true);
    }

    internal string Observe(GoodsReceiptLookupFixture fixture)
    {
        if (_probe == null) return "BCF matrix declaration absent; hosted regression only.";
        Assert.Equal(_theme, BootstrapThemeManager.CurrentTheme.Mode.ToString());
        var start = _probe.Start(fixture.Form.Handle.ToInt64() + " measure-lookup " + Process.GetCurrentProcess().Id);
        start.EnvironmentVariables["MYDMSVN_BCF_EXPECTED_SCALE"] = _scale;
        var operation = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), fixture.Token);
        fixture.Wait(operation);
        var result = operation.GetAwaiter().GetResult();
        Assert.True(result.CleanupVerified);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Contains("NATIVE_LOOKUP_ENVIRONMENT=PASS", result.StandardOutput);
        return "BCF_THEME=" + _theme + ";OBSERVER_CLEANUP=verified;" + result.StandardOutput;
    }

    public void Dispose() => _probe?.Dispose();
}
