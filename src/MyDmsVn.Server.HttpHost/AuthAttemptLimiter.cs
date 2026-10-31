using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MyDmsVn.Server.HttpHost;

// Per-worker admission guard. Bound both cardinality and attempts; a full worker fails closed.
// SQL session rotation remains authoritative across workers; this is not a distributed lock.
internal sealed class AuthAttemptLimiter
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, Window> _windows = new Dictionary<string, Window>(StringComparer.Ordinal);
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(5);
    private const int Capacity = 4096;
    public bool TryAdmit(string operation, string subject, string remoteIp, DateTime now)
    {
        var subjectKey = Hash(operation + ":subject:" + subject);
        var ipKey = Hash(operation + ":ip:" + remoteIp);
        var subjectLimit = operation == "login" ? 10 : 30;
        var ipLimit = operation == "login" ? 30 : 60;
        lock (_gate)
        {
            foreach (var expired in _windows.Where(p => p.Value.Until <= now).Select(p => p.Key).ToArray()) _windows.Remove(expired);
            _windows.TryGetValue(subjectKey, out var account);
            _windows.TryGetValue(ipKey, out var ip);
            // A rejected caller must not allocate fresh subjects/IPs after its counterpart is exhausted.
            if (account?.Attempts >= subjectLimit || ip?.Attempts >= ipLimit) return false;
            var missing = (account == null ? 1 : 0) + (ip == null ? 1 : 0);
            if (_windows.Count + missing > Capacity) return false;
            if (account == null) _windows[subjectKey] = account = new Window(now.Add(Backoff));
            if (ip == null) _windows[ipKey] = ip = new Window(now.Add(Backoff));
            account.Attempts++; ip.Attempts++;
            return true;
        }
    }
    private static string Hash(string value)
    {
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
    private sealed class Window
    {
        public Window(DateTime until) { Until = until; }
        public DateTime Until { get; }
        public int Attempts;
    }
}
