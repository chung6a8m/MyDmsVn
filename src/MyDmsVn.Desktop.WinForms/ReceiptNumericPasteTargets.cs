using System;
using System.Collections.Generic;
using System.Linq;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;
using MyDmsVn.Desktop.Application;
using SourceGrid;

namespace MyDmsVn.Desktop.WinForms;

internal static class ReceiptNumericPasteTargets
{
    internal static IReadOnlyList<Position> Capture(BootstrapGrid grid) =>
        grid.Selection.GetSelectionRegion().GetCellsPositions().Distinct()
            .OrderBy(position => position.Row).ThenBy(position => position.Column).ToArray();

    internal static IReadOnlyList<InlineValidationAddress> Address<TLine>(IReadOnlyList<Position> positions,
        NumericDraftSnapshot<TLine> snapshot, Func<int, string> fieldForColumn) where TLine : class
    {
        if (positions == null) throw new ArgumentNullException(nameof(positions));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (fieldForColumn == null) throw new ArgumentNullException(nameof(fieldForColumn));
        return positions.Select(position => new InlineValidationAddress(snapshot.DocumentGeneration,
            position.Row > 0 && position.Row <= snapshot.Lines.Count ? snapshot.Lines[position.Row - 1] : new object(),
            fieldForColumn(position.Column))).ToArray();
    }
}
