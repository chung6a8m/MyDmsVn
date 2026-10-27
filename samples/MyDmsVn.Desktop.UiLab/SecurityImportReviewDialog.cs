using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.UiLab;

internal sealed class SecurityImportReviewDialog : Form
{
    internal Button AdvanceTargetRevisionButton { get; } = new()
        { Text = "Đổi revision đối tượng", AutoSize = true };
    internal Button RotateCatalogDigestButton { get; } = new()
        { Text = "Đổi catalog digest", AutoSize = true };
    internal Button SwitchTargetButton { get; } = new()
        { Text = "Đổi đối tượng", AutoSize = true };
    internal Button ConfirmButton { get; } = new()
        { Text = "Áp dụng", AutoSize = true };

    internal SecurityImportReviewDialog(string summary, Action advanceTargetRevision,
        Action rotateCatalogDigest, Action? switchTarget = null)
    {
        Text = "Xem trước nhập quyền — UI Lab";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(530, 260);
        Size = new Size(610, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;

        var details = new TextBox
        {
            Text = summary, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, TabStop = false
        };
        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8)
        };
        var cancel = new Button { Text = "Hủy", AutoSize = true };
        var instruction = new Label
        {
            Text = "UI Lab: tạo biến cố sau Preview, trước Apply.",
            Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8)
        };
        controls.Controls.AddRange(new Control[]
        {
            AdvanceTargetRevisionButton, RotateCatalogDigestButton, SwitchTargetButton, ConfirmButton, cancel
        });
        Controls.Add(details);
        Controls.Add(controls);
        Controls.Add(instruction);

        AdvanceTargetRevisionButton.Click += (_, _) =>
        {
            advanceTargetRevision();
            AdvanceTargetRevisionButton.Enabled = false;
        };
        RotateCatalogDigestButton.Click += (_, _) =>
        {
            rotateCatalogDigest();
            RotateCatalogDigestButton.Enabled = false;
        };
        SwitchTargetButton.Enabled = switchTarget != null;
        SwitchTargetButton.Click += (_, _) =>
        {
            switchTarget?.Invoke();
            SwitchTargetButton.Enabled = false;
        };
        ConfirmButton.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        AcceptButton = ConfirmButton;
        CancelButton = cancel;
    }
}
