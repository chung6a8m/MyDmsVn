using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyDmsVn.Desktop.Application;

public enum NumericDraftField { Quantity, UnitPrice, UnitCost }

public sealed class NumericDraftSnapshot<TLine> where TLine : class
{
    public NumericDraftSnapshot(Guid editorIdentity, long documentGeneration, long revision, IReadOnlyList<TLine> lines)
    {
        if (editorIdentity == Guid.Empty) throw new ArgumentException("An editor identity is required.", nameof(editorIdentity));
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        EditorIdentity = editorIdentity;
        DocumentGeneration = documentGeneration;
        Revision = revision;
        Lines = new ReadOnlyCollection<TLine>(new List<TLine>(lines));
    }

    public Guid EditorIdentity { get; }
    public long DocumentGeneration { get; }
    public long Revision { get; }
    public IReadOnlyList<TLine> Lines { get; }
}

public sealed class NumericDraftChange<TLine> where TLine : class
{
    public NumericDraftChange(TLine line, NumericDraftField field, decimal value)
    {
        Line = line ?? throw new ArgumentNullException(nameof(line));
        Field = field;
        Value = value;
    }
    public TLine Line { get; }
    public NumericDraftField Field { get; }
    public decimal Value { get; }
}

public sealed class NumericDraftPatch<TLine> where TLine : class
{
    public NumericDraftPatch(NumericDraftSnapshot<TLine> snapshot, IReadOnlyList<NumericDraftChange<TLine>> changes)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (changes == null) throw new ArgumentNullException(nameof(changes));
        Changes = new ReadOnlyCollection<NumericDraftChange<TLine>>(new List<NumericDraftChange<TLine>>(changes));
    }
    public NumericDraftSnapshot<TLine> Snapshot { get; }
    public IReadOnlyList<NumericDraftChange<TLine>> Changes { get; }
}
