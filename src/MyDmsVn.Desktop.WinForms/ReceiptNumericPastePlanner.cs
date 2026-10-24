using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class NumericPasteFailure
{
    internal NumericPasteFailure(InlineValidationAddress? address, string message)
    { Address = address; Message = message; }
    internal InlineValidationAddress? Address { get; }
    internal string Message { get; }
}

internal sealed class NumericPasteResult<TLine> where TLine : class
{
    internal NumericPasteResult(NumericDraftPatch<TLine>? patch, IReadOnlyList<NumericPasteFailure> failures)
    { Patch = patch; Failures = failures; }
    internal NumericDraftPatch<TLine>? Patch { get; }
    internal IReadOnlyList<NumericPasteFailure> Failures { get; }
    internal bool IsAccepted => Patch != null && Failures.Count == 0;
}

internal sealed class ReceiptNumericPastePlanner
{
    private const int MaximumCharacters = 262144;
    private readonly ReceiptNumericField _amountField;
    private readonly DecimalInputProfile _quantity;
    private readonly DecimalInputProfile _amount;

    internal ReceiptNumericPastePlanner(ReceiptNumericField amountField, DecimalInputProfile quantity, DecimalInputProfile amount)
    {
        if (amountField != ReceiptNumericField.UnitPrice && amountField != ReceiptNumericField.UnitCost)
            throw new ArgumentOutOfRangeException(nameof(amountField));
        _quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        _amount = amount ?? throw new ArgumentNullException(nameof(amount));
        _amountField = amountField;
    }

    internal NumericPasteResult<TLine> Plan<TLine>(string tsv, NumericDraftSnapshot<TLine> snapshot,
        IReadOnlyList<InlineValidationAddress> targets) where TLine : class
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        NumericPasteResult<TLine> Reject(string message, InlineValidationAddress? address = null) =>
            new NumericPasteResult<TLine>(null, new[] { new NumericPasteFailure(address, message) });
        if (tsv == null || tsv.Length == 0 || tsv.Length > MaximumCharacters)
            return Reject("Clipboard text is empty or exceeds the 262,144-character limit.");
        if (tsv.IndexOf('\r') >= 0 && tsv.Replace("\r\n", "").IndexOf('\r') >= 0)
            return Reject("Use CRLF or LF row separators.");
        var rows = tsv.Split('\n');
        if (rows.Length > 1 && rows[rows.Length - 1].Length == 0) rows = rows.Take(rows.Length - 1).ToArray();
        var cells = rows.Select(row => row.TrimEnd('\r').Split('\t')).ToArray();
        if (cells.Length == 0 || cells.Length > snapshot.Lines.Count || cells.Any(row => row.Length < 1 || row.Length > 2))
            return Reject("Paste must fit existing rows and one or two numeric columns.");
        var columns = cells[0].Length;
        if (cells.Any(row => row.Length != columns) || targets.Count != cells.Length * columns)
            return Reject("Paste must form the selected rectangle without ragged or extra cells.");

        var changes = new List<NumericDraftChange<TLine>>(targets.Count);
        var failures = new List<NumericPasteFailure>();
        var previousRow = -1;
        var firstField = default(NumericDraftField?);
        for (var row = 0; row < cells.Length; ++row)
        {
            var rowIndex = -1;
            for (var column = 0; column < columns; ++column)
            {
                var address = targets[row * columns + column];
                var line = address.LineIdentity as TLine;
                var index = line == null ? -1 : IndexOfReference(snapshot.Lines, line);
                if (address.DocumentGeneration != snapshot.DocumentGeneration || index < 0 ||
                    (column > 0 && index != rowIndex) || (column == 0 && row > 0 && index != previousRow + 1))
                    return Reject("Selected cells are stale, unavailable or nonrectangular.", address);
                if (column == 0) rowIndex = index;
                var field = ParseField(address.Field);
                if (field == null || (columns == 2 && field != (column == 0 ? NumericDraftField.Quantity : AmountDraftField)) ||
                    (columns == 1 && firstField.HasValue && firstField != field))
                    return Reject("Only the selected receipt numeric columns can be pasted.", address);
                if (column == 0 && row == 0) firstField = field;
                var profile = field == NumericDraftField.Quantity ? _quantity : _amount;
                var inputField = field == NumericDraftField.Quantity ? ReceiptNumericField.Quantity : _amountField;
                if (!ReceiptNumericInput.TryParse(cells[row][column], inputField, profile.Culture, out var value, out var error) ||
                    value < profile.Minimum || value > profile.Maximum)
                    failures.Add(new NumericPasteFailure(address, error ?? "Value is outside the selected field bounds."));
                else changes.Add(new NumericDraftChange<TLine>(line!, field.Value, value));
            }
            previousRow = rowIndex;
        }
        return failures.Count > 0
            ? new NumericPasteResult<TLine>(null, new ReadOnlyCollection<NumericPasteFailure>(failures))
            : new NumericPasteResult<TLine>(new NumericDraftPatch<TLine>(snapshot, changes), Array.Empty<NumericPasteFailure>());
    }

    private NumericDraftField AmountDraftField => _amountField == ReceiptNumericField.UnitPrice
        ? NumericDraftField.UnitPrice : NumericDraftField.UnitCost;
    private NumericDraftField? ParseField(string field) =>
        string.Equals(field, "qty", StringComparison.OrdinalIgnoreCase) || string.Equals(field, "quantity", StringComparison.OrdinalIgnoreCase)
            ? NumericDraftField.Quantity :
        string.Equals(field, _amountField == ReceiptNumericField.UnitPrice ? "unitPrice" : "unitCost", StringComparison.OrdinalIgnoreCase)
            ? AmountDraftField : (NumericDraftField?)null;
    private static int IndexOfReference<TLine>(IReadOnlyList<TLine> lines, TLine line) where TLine : class
    {
        for (var index = 0; index < lines.Count; ++index)
            if (ReferenceEquals(lines[index], line)) return index;
        return -1;
    }
}
