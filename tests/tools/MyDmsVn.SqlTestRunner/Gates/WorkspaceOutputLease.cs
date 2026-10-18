using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestRunner.Gates;

internal sealed class WorkspaceOutputLease : IAsyncDisposable
{
    private readonly FileStream _stream;

    private WorkspaceOutputLease(FileStream stream) => _stream = stream;

    public static async Task<WorkspaceOutputLease> AcquireAsync(
        string repositoryRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var canonicalRoot = Path.GetFullPath(repositoryRoot).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            canonicalRoot = canonicalRoot.ToUpperInvariant();
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRoot)));
        var lockDirectory = Path.Combine(Path.GetTempPath(), "MyDmsVn", "TestGate", "workspace-locks");
        Directory.CreateDirectory(lockDirectory);
        var lockPath = Path.Combine(lockDirectory, key + ".lock");
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new WorkspaceOutputLease(new FileStream(
                    lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException("Timed out waiting for the repository output build lease.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
