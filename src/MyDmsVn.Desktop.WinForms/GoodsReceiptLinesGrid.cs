using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms;

// Writable accessibility is deliberately owned by BCI-T04, not the read-only Open-row bridge.
internal sealed class GoodsReceiptLinesGrid : BootstrapGrid
{
    internal GoodsReceiptLinesGrid() { AccessibleName = "Goods receipt lines"; }
}
