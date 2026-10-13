using System;
using System.IO;

namespace MyDmsVn.Server.Infrastructure.Diagnostics
{
    public sealed class DesktopDiagnosticsOptions
    {
        public DesktopDiagnosticsOptions(
            string hostName,
            string logDirectory,
            int retainedFileCountLimit)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new ArgumentException("A host name is required.", nameof(hostName));
            }

            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                throw new ArgumentException("A log directory is required.", nameof(logDirectory));
            }

            if (retainedFileCountLimit < 1 || retainedFileCountLimit > 366)
            {
                throw new ArgumentOutOfRangeException(nameof(retainedFileCountLimit));
            }

            HostName = hostName.Trim();
            LogDirectory = Path.GetFullPath(logDirectory);
            RetainedFileCountLimit = retainedFileCountLimit;
        }

        public string HostName { get; }
        public string LogDirectory { get; }
        public int RetainedFileCountLimit { get; }

        public static DesktopDiagnosticsOptions CreateDefault(string hostName)
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new DesktopDiagnosticsOptions(
                hostName,
                Path.Combine(root, "MyDmsVn", "Logs", hostName),
                14);
        }
    }

    public interface IDiagnosticsStartupStatus
    {
        bool FileLoggingEnabled { get; }
        string? SafeErrorCode { get; }
    }
}
