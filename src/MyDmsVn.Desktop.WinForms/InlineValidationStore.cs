using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class ValidationIssue
{
    internal ValidationIssue(string code, string message, string source)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
    internal string Code { get; }
    internal string Message { get; }
    internal string Source { get; }
}

internal readonly struct InlineValidationAddress : IEquatable<InlineValidationAddress>
{
    internal InlineValidationAddress(long documentGeneration, object lineIdentity, string field)
    {
        DocumentGeneration = documentGeneration;
        LineIdentity = lineIdentity ?? throw new ArgumentNullException(nameof(lineIdentity));
        Field = field ?? throw new ArgumentNullException(nameof(field));
    }
    internal long DocumentGeneration { get; }
    internal object LineIdentity { get; }
    internal string Field { get; }
    public bool Equals(InlineValidationAddress other) => DocumentGeneration == other.DocumentGeneration &&
        ReferenceEquals(LineIdentity, other.LineIdentity) && StringComparer.OrdinalIgnoreCase.Equals(Field, other.Field);
    public override bool Equals(object? obj) => obj is InlineValidationAddress other && Equals(other);
    public override int GetHashCode() => ((DocumentGeneration.GetHashCode() * 397) ^ RuntimeHelpers.GetHashCode(LineIdentity)) * 397 ^
        StringComparer.OrdinalIgnoreCase.GetHashCode(Field);
}

internal sealed class InlineValidationStore
{
    private readonly Dictionary<InlineValidationAddress, IReadOnlyList<ValidationIssue>> _issues =
        new Dictionary<InlineValidationAddress, IReadOnlyList<ValidationIssue>>();
    internal void Set(InlineValidationAddress address, IReadOnlyList<ValidationIssue> issues)
    {
        if (issues == null) throw new ArgumentNullException(nameof(issues));
        if (issues.Count == 0) _issues.Remove(address);
        else _issues[address] = issues.ToArray();
    }
    internal IReadOnlyList<ValidationIssue> Get(InlineValidationAddress address) =>
        _issues.TryGetValue(address, out var issues) ? issues : Array.Empty<ValidationIssue>();
    internal void Clear(InlineValidationAddress address) => _issues.Remove(address);
    internal void SetSource(InlineValidationAddress address, string source, IReadOnlyList<ValidationIssue> issues)
    {
        var retained = Get(address).Where(issue => !StringComparer.OrdinalIgnoreCase.Equals(issue.Source, source));
        Set(address, retained.Concat(issues).ToArray());
    }
    internal void ClearSource(string source)
    {
        foreach (var address in _issues.Keys.ToArray()) SetSource(address, source, Array.Empty<ValidationIssue>());
    }
    internal void ClearLine(long documentGeneration, object lineIdentity)
    {
        foreach (var address in _issues.Keys.Where(key => key.DocumentGeneration == documentGeneration &&
            ReferenceEquals(key.LineIdentity, lineIdentity)).ToArray()) _issues.Remove(address);
    }
    internal void ClearAll() => _issues.Clear();
    internal IReadOnlyList<KeyValuePair<InlineValidationAddress, IReadOnlyList<ValidationIssue>>> Entries => _issues.ToArray();
}
