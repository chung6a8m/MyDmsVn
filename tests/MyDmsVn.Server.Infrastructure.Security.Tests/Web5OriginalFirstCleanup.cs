using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class Web5OriginalFirstCleanup
{
    internal static async Task<List<Exception>> Run(bool browserDrained, bool poisoned, IEnumerable<Func<Task>> cleanOwned, Action completeManifest)
    {
        var failures = new List<Exception>();
        if (!browserDrained || poisoned)
        {
            failures.Add(new InvalidOperationException("Browser or SQL ownership is uncertain; retain original-first manifest and database."));
            return failures;
        }
        foreach (var clean in cleanOwned)
            try { await clean(); } catch (Exception error) { failures.Add(error); break; }
        if (failures.Count == 0)
            try { completeManifest(); } catch (Exception error) { failures.Add(error); }
        return failures;
    }
}
