using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
// Serializes each operator-first decision and its late-original physical check.
internal sealed class Web5NegativeSequence
{
    private readonly HashSet<Guid> _keys = new HashSet<Guid>();
    private readonly HashSet<string> _kinds = new HashSet<string>(StringComparer.Ordinal);
    private Guid? _pending;
    internal bool Complete => !_pending.HasValue && _kinds.Count == 6;
    internal void Begin(Guid key, string kind)
    {
        if (_pending.HasValue || key == Guid.Empty || _keys.Contains(key) || _kinds.Contains(kind) ||
            !new[] { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" }.Contains(kind))
            throw new InvalidDataException("Invalid negative fixture sequence.");
        _keys.Add(key); _kinds.Add(kind); _pending = key;
    }
    internal void Verify(Guid key)
    {
        if (!_pending.HasValue || _pending.Value != key) throw new InvalidDataException("Negative fixture key mismatch.");
        _pending = null;
    }
}
