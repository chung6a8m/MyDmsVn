using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web5IisExpressProcessObservation
{
    internal Web5IisExpressProcessObservation(int processId, DateTime startTimeUtc, string executablePath, string commandLine)
    {
        ProcessId = processId;
        StartTimeUtc = startTimeUtc;
        ExecutablePath = executablePath;
        CommandLine = commandLine;
    }
    internal int ProcessId { get; }
    internal DateTime StartTimeUtc { get; }
    internal string ExecutablePath { get; }
    internal string CommandLine { get; }
}

internal sealed class Web5IisExpressIdentity
{
    internal Web5IisExpressIdentity(Web5IisExpressProcessObservation process, string configurationPath,
        byte[] configurationHash, string siteName, Uri origin)
    {
        Process = process;
        ConfigurationPath = configurationPath;
        ConfigurationHash = configurationHash;
        SiteName = siteName;
        Origin = origin;
    }
    internal Web5IisExpressProcessObservation Process { get; }
    internal string ConfigurationPath { get; }
    internal byte[] ConfigurationHash { get; }
    internal string SiteName { get; }
    internal Uri Origin { get; }
}

internal static class Web5IisExpressHostAdmission
{
    private const int MaximumConfigurationLength = 1024 * 1024;
    private static readonly Regex SiteNamePattern = new Regex("\\A[A-Za-z0-9_.-]{1,64}\\z", RegexOptions.CultureInvariant);

    internal static string ExpectedExecutablePath => Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe"));

    internal static string ExpectedCommandLine(string executablePath, string configurationPath, string siteName) =>
        "\"" + Path.GetFullPath(executablePath) + "\" /config:\"" + Path.GetFullPath(configurationPath) +
        "\" /site:" + siteName + " /systray:false";

    internal static Web5IisExpressIdentity Capture(Process process, string configurationPath, string siteName, Uri origin)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        process.Refresh();
        if (process.HasExited) throw new InvalidDataException("Owned IIS Express process has exited.");
        var ownedProcessId = process.Id;
        var ownedStartTimeUtc = process.StartTime.ToUniversalTime();
        var observation = Observe(ownedProcessId);
        process.Refresh();
        if (process.HasExited || process.Id != ownedProcessId || process.StartTime.ToUniversalTime() != ownedStartTimeUtc)
            throw new InvalidDataException("Owned IIS Express process handle changed during capture.");
        ValidateOwnedHandleIdentity(ownedProcessId, ownedStartTimeUtc, observation);
        return Capture(observation, configurationPath, siteName, origin);
    }

    internal static void ValidateOwnedHandleIdentity(int ownedProcessId, DateTime ownedStartTimeUtc,
        Web5IisExpressProcessObservation observation)
    {
        if (observation == null || ownedProcessId <= 0 || ownedStartTimeUtc.Kind != DateTimeKind.Utc ||
            observation.ProcessId != ownedProcessId ||
            Math.Abs((observation.StartTimeUtc - ownedStartTimeUtc).Ticks) > 9)
            throw new InvalidDataException("Fixture process handle and WMI identity do not match.");
    }

    internal static Web5IisExpressIdentity Capture(Web5IisExpressProcessObservation process,
        string configurationPath, string siteName, Uri origin)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (string.IsNullOrWhiteSpace(configurationPath) || !Path.IsPathRooted(configurationPath))
            throw new ArgumentException("IIS Express configuration path must be absolute.", nameof(configurationPath));
        if (!SiteNamePattern.IsMatch(siteName ?? string.Empty))
            throw new ArgumentException("IIS Express site name is invalid.", nameof(siteName));
        if (origin == null || !origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps ||
            origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new ArgumentException("IIS Express origin must be an HTTPS root origin.", nameof(origin));
        var fullConfigurationPath = Path.GetFullPath(configurationPath);
        var exactSiteName = siteName!;
        ValidateProcess(process, fullConfigurationPath, exactSiteName, expected: null);
        return new Web5IisExpressIdentity(process, fullConfigurationPath, HashConfiguration(fullConfigurationPath), exactSiteName, origin);
    }

    internal static async Task<Web5HttpRequestQueue> VerifyAsync(Web5IisExpressIdentity identity, CancellationToken token)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        token.ThrowIfCancellationRequested();
        var before = Observe(identity.Process.ProcessId);
        ValidateProcess(before, identity.ConfigurationPath, identity.SiteName, identity.Process);
        ValidateConfiguration(identity);
        var snapshot = await Web5HttpServiceState.ReadAsync(TimeSpan.FromSeconds(10), token);
        var queue = ValidateEvidence(identity, before, snapshot);
        token.ThrowIfCancellationRequested();
        var after = Observe(identity.Process.ProcessId);
        ValidateProcess(after, identity.ConfigurationPath, identity.SiteName, identity.Process);
        return queue;
    }

    internal static async Task VerifyAbsentAsync(Web5IisExpressIdentity identity, CancellationToken token)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        token.ThrowIfCancellationRequested();
        var observation = ObserveOptional(identity.Process.ProcessId);
        var snapshot = await Web5HttpServiceState.ReadAsync(TimeSpan.FromSeconds(10), token);
        ValidateAbsent(identity, observation, snapshot);
    }

    internal static async Task WaitForAdmissionAsync(Web5IisExpressIdentity identity, Process process,
        TimeSpan budget, CancellationToken token)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(budget));
        var deadline = DateTime.UtcNow + budget;
        Exception? last = null;
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            token.ThrowIfCancellationRequested();
            try { await VerifyAsync(identity, token); return; }
            catch (InvalidDataException error) { last = error; }
            await Task.Delay(200, token);
        }
        throw new TimeoutException("Owned IIS Express queue was not admitted within the bounded startup window.", last);
    }

    internal static async Task WaitForAbsenceAsync(Web5IisExpressIdentity identity, TimeSpan budget, CancellationToken token)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(budget));
        var deadline = DateTime.UtcNow + budget;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            try { await VerifyAbsentAsync(identity, token); return; }
            catch (InvalidDataException error) { last = error; }
            await Task.Delay(200, token);
        }
        throw new TimeoutException("Owned IIS Express process or queue absence was not proven.", last);
    }

    internal static Web5HttpRequestQueue ValidateEvidence(Web5IisExpressIdentity identity,
        Web5IisExpressProcessObservation observation, string serviceState)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        ValidateProcess(observation, identity.ConfigurationPath, identity.SiteName, identity.Process);
        ValidateConfiguration(identity);
        return Web5HttpServiceState.RequireOwnedQueue(
            Web5HttpServiceState.Parse(serviceState), identity.Origin, identity.Process.ProcessId);
    }

    internal static void ValidateAbsent(Web5IisExpressIdentity identity,
        Web5IisExpressProcessObservation? observation, string serviceState)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        if (observation != null) throw new InvalidDataException("Owned IIS Express process absence was not proven.");
        if (Web5HttpServiceState.Parse(serviceState).Any(queue =>
            queue.RegisteredUrls.Any(url => Web5HttpServiceState.MatchesOrigin(url, identity.Origin))))
            throw new InvalidDataException("Owned IIS Express request queue absence was not proven.");
    }

    internal static HttpWebRequest CreateDirectRequest(Uri uri)
    {
        if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Direct IIS Express request must use HTTPS.", nameof(uri));
        return ConfigureDirectRequest(uri);
    }

    internal static HttpWebRequest CreateDirectHttpRejectionRequest(Uri uri)
    {
        if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttp ||
            !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Port != 18081 ||
            uri.AbsolutePath != "/api/v1/health" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Direct HTTP rejection probe target is invalid.", nameof(uri));
        return ConfigureDirectRequest(uri);
    }

    private static HttpWebRequest ConfigureDirectRequest(Uri uri)
    {
        var request = (HttpWebRequest)WebRequest.Create(uri);
        request.Proxy = null;
        request.AllowAutoRedirect = false;
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
        request.KeepAlive = false;
        return request;
    }

    private static void ValidateProcess(Web5IisExpressProcessObservation process, string configurationPath,
        string siteName, Web5IisExpressProcessObservation? expected)
    {
        if (process.ProcessId <= 0 || process.StartTimeUtc.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("IIS Express process identity is invalid.");
        if (!PathEquals(process.ExecutablePath, ExpectedExecutablePath))
            throw new InvalidDataException("Owned process is not the installed IIS Express executable.");
        if (!string.Equals(process.CommandLine,
            ExpectedCommandLine(ExpectedExecutablePath, configurationPath, siteName), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("IIS Express command line does not match the owned configuration and site.");
        if (expected != null && (process.ProcessId != expected.ProcessId || process.StartTimeUtc != expected.StartTimeUtc ||
            !PathEquals(process.ExecutablePath, expected.ExecutablePath) ||
            !string.Equals(process.CommandLine, expected.CommandLine, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("IIS Express process identity changed after capture.");
    }

    private static void ValidateConfiguration(Web5IisExpressIdentity identity)
    {
        if (!HashConfiguration(identity.ConfigurationPath).SequenceEqual(identity.ConfigurationHash))
            throw new InvalidDataException("IIS Express configuration changed after capture.");
    }

    private static byte[] HashConfiguration(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0 || file.Length > MaximumConfigurationLength ||
            (file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("IIS Express configuration is missing or unsafe.");
        using var hash = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return hash.ComputeHash(stream);
    }

    private static Web5IisExpressProcessObservation Observe(int processId)
    {
        return ObserveOptional(processId) ??
            throw new InvalidDataException("Owned IIS Express process identity is unavailable.");
    }

    private static Web5IisExpressProcessObservation? ObserveOptional(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId,CreationDate,ExecutablePath,CommandLine FROM Win32_Process WHERE ProcessId=" +
                processId.ToString(CultureInfo.InvariantCulture));
            using var results = searcher.Get();
            var rows = results.Cast<ManagementObject>().ToArray();
            if (rows.Length == 0) return null;
            if (rows.Length != 1) throw new InvalidDataException("Owned IIS Express process identity is ambiguous.");
            using var row = rows[0];
            var creation = row["CreationDate"] as string;
            var executable = row["ExecutablePath"] as string;
            var command = row["CommandLine"] as string;
            if (string.IsNullOrWhiteSpace(creation) || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(command))
                throw new InvalidDataException("Owned IIS Express process metadata is incomplete.");
            var start = ManagementDateTimeConverter.ToDateTime(creation).ToUniversalTime();
            return new Web5IisExpressProcessObservation(processId, start, executable!, command!);
        }
        catch (ManagementException exception)
        {
            throw new InvalidDataException("Owned IIS Express process metadata could not be read.", exception);
        }
    }

    private static bool PathEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

}

internal static class Web5OwnedIisExpressAdmissionFixture
{
    private const string SiteName = "Web5OwnedAdmission";
    private static readonly Uri Origin = new Uri("https://localhost:44321/");

    internal static async Task Run()
    {
        var thumbprint = NormalizeThumbprint(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT"));
        if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == 44321))
            throw new InvalidOperationException("Owned IIS Express admission port is already in use.");

        var runDirectory = CreatePrivateRunDirectory();
        var siteDirectory = Path.Combine(runDirectory, "site");
        CreatePrivateDirectory(siteDirectory);
        var nonce = Guid.NewGuid().ToString("N");
        var releasePath = Path.Combine(runDirectory, "release.signal");
        File.WriteAllText(Path.Combine(siteDirectory, "probe.aspx"),
            "<%@ Page Language=\"C#\" %><% var deadline=System.DateTime.UtcNow.AddSeconds(15); while(!System.IO.File.Exists(@\"" +
            releasePath.Replace("\"", "\"\"") + "\") && System.DateTime.UtcNow<deadline) System.Threading.Thread.Sleep(25); " +
            "Response.ContentType=\"text/plain\"; Response.Write(\"" + nonce + "\"); %>");
        var configuration = CreateConfiguration(runDirectory, siteDirectory);
        Process? process = null;
        Web5IisExpressIdentity? identity = null;
        var absenceProven = false;
        try
        {
            var executable = Web5IisExpressHostAdmission.ExpectedExecutablePath;
            if (!File.Exists(executable)) throw new InvalidOperationException("Installed IIS Express was not found.");
            var start = new ProcessStartInfo(executable,
                "/config:\"" + configuration + "\" /site:" + SiteName + " /systray:false")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            if (!process.Start()) throw new InvalidOperationException("Owned IIS Express process did not start.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            identity = Web5IisExpressHostAdmission.Capture(process, configuration, SiteName, Origin);
            await Web5IisExpressHostAdmission.WaitForAdmissionAsync(identity, process, TimeSpan.FromSeconds(20), CancellationToken.None);
            await Web5IisExpressHostAdmission.VerifyAsync(identity, CancellationToken.None);

            var request = Web5IisExpressHostAdmission.CreateDirectRequest(new Uri(Origin, "probe.aspx"));
            request.ServerCertificateValidationCallback = (_, certificate, _, _) => certificate != null &&
                string.Equals(NormalizeThumbprint(certificate.GetCertHashString()), thumbprint, StringComparison.Ordinal);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var abort = timeout.Token.Register(request.Abort);
            var responseTask = request.GetResponseAsync();
            await Task.Delay(250);
            if (responseTask.IsCompleted) throw new InvalidDataException("Direct probe completed before in-flight ownership verification.");
            await Web5IisExpressHostAdmission.VerifyAsync(identity, timeout.Token);
            if (responseTask.IsCompleted) throw new InvalidDataException("Direct probe did not remain in flight through ownership verification.");
            File.WriteAllText(releasePath, "release");
            using (var response = (HttpWebResponse)await responseTask)
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                if (response.StatusCode != HttpStatusCode.OK || await reader.ReadToEndAsync() != nonce)
                    throw new InvalidDataException("Direct owned-host probe response is invalid.");
            }
            await Web5IisExpressHostAdmission.VerifyAsync(identity, CancellationToken.None);
            Console.WriteLine("WEB5_OWNED_HOST_ADMITTED pid=" + identity.Process.ProcessId.ToString(CultureInfo.InvariantCulture) +
                " origin=" + identity.Origin.GetLeftPart(UriPartial.Authority) + " queue=exclusive snapshots=3 proxy=disabled");
        }
        finally
        {
            if (process != null)
            {
                if (!process.HasExited)
                {
                    try { process.StandardInput.WriteLine("Q"); process.StandardInput.Flush(); }
                    catch (InvalidOperationException) { }
                    if (!process.WaitForExit(15000))
                    {
                        process.Kill();
                        if (!process.WaitForExit(10000))
                            throw new InvalidOperationException("Owned IIS Express process did not stop; fixture retained.");
                    }
                }
                if (identity != null)
                {
                    await Web5IisExpressHostAdmission.WaitForAbsenceAsync(identity, TimeSpan.FromSeconds(20), CancellationToken.None);
                    absenceProven = true;
                }
                process.Dispose();
            }
            if (absenceProven || process == null)
                DeleteOwnedRunDirectory(runDirectory);
            else
                Console.WriteLine("WEB5_OWNED_HOST_QUARANTINED");
        }
    }

    private static string CreatePrivateRunDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mydmsvn-web5-owned-host-" + Guid.NewGuid().ToString("N"));
        CreatePrivateDirectory(path);
        return path;
    }

    private static void CreatePrivateDirectory(string path)
    {
        using var user = WindowsIdentity.GetCurrent();
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
        Web5AttendedLease.VerifyPrivateDirectory(path, user.User!);
    }

    private static void DeleteOwnedRunDirectory(string runDirectory)
    {
        using var user = WindowsIdentity.GetCurrent();
        Web5AttendedLease.VerifyPrivateDirectory(runDirectory, user.User!);
        var siteDirectory = Path.Combine(runDirectory, "site");
        Web5AttendedLease.VerifyPrivateDirectory(siteDirectory, user.User!);
        var expectedRoot = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(runDirectory, "applicationhost.config"),
            Path.Combine(runDirectory, "release.signal"),
            siteDirectory,
        };
        var actualRoot = Directory.GetFileSystemEntries(runDirectory);
        if (actualRoot.Any(path => !expectedRoot.Contains(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidDataException("Owned IIS Express run directory contains an unexpected entry; retain it.");
        var probe = Path.Combine(siteDirectory, "probe.aspx");
        var actualSite = Directory.GetFileSystemEntries(siteDirectory);
        if (actualSite.Length != 1 || !string.Equals(actualSite[0], probe, StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(probe) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Owned IIS Express site directory contains an unexpected entry; retain it.");
        File.Delete(probe);
        Directory.Delete(siteDirectory);
        File.Delete(Path.Combine(runDirectory, "release.signal"));
        File.Delete(Path.Combine(runDirectory, "applicationhost.config"));
        Directory.Delete(runDirectory);
    }

    private static string CreateConfiguration(string runDirectory, string siteDirectory)
    {
        var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "IISExpress", "config", "applicationhost.config");
        var xml = new System.Xml.XmlDocument();
        xml.Load(source);
        var sites = xml.SelectSingleNode("/configuration/system.applicationHost/sites") ??
            throw new InvalidDataException("IIS Express site configuration section is missing.");
        foreach (System.Xml.XmlNode site in sites.SelectNodes("site")!) sites.RemoveChild(site);
        var current = xml.CreateElement("site");
        current.SetAttribute("name", SiteName);
        current.SetAttribute("id", "1");
        var application = xml.CreateElement("application");
        application.SetAttribute("path", "/");
        application.SetAttribute("applicationPool", "Clr4IntegratedAppPool");
        var directory = xml.CreateElement("virtualDirectory");
        directory.SetAttribute("path", "/");
        directory.SetAttribute("physicalPath", Path.GetFullPath(siteDirectory));
        application.AppendChild(directory);
        current.AppendChild(application);
        var bindings = xml.CreateElement("bindings");
        var binding = xml.CreateElement("binding");
        binding.SetAttribute("protocol", "https");
        binding.SetAttribute("bindingInformation", "*:44321:localhost");
        bindings.AppendChild(binding);
        current.AppendChild(bindings);
        sites.AppendChild(current);
        var path = Path.Combine(runDirectory, "applicationhost.config");
        xml.Save(path);
        return path;
    }

    private static string NormalizeThumbprint(string? value)
    {
        var normalized = (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (!Regex.IsMatch(normalized, "\\A[0-9A-F]{40,64}\\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("TLS thumbprint is invalid.");
        return normalized;
    }
}
