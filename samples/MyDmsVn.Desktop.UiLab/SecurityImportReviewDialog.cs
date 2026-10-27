using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.WinForms;

namespace MyDmsVn.Desktop.UiLab;

internal sealed class SecurityImportReviewDialog : Form
{
    internal Button AdvanceTargetRevisionButton { get; } = new BootstrapButton
        { Text = "Đổi revision đối tượng", AutoSize = true };
    internal Button RotateCatalogDigestButton { get; } = new BootstrapButton
        { Text = "Đổi catalog digest", AutoSize = true };
    internal Button SwitchTargetButton { get; } = new BootstrapButton
        { Text = "Đổi đối tượng", AutoSize = true };
    internal Button ConfirmButton { get; } = new BootstrapButton
        { Text = "Áp dụng", AutoSize = true, Variant = BootstrapVariant.Primary };

    internal SecurityImportReviewDialog(string summary, Action advanceTargetRevision,
        Action rotateCatalogDigest, Action? switchTarget = null)
    {
        Text = "Xem trước nhập quyền — UI Lab";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(530, 260);
        Size = new Size(610, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;

        // Same c232a4c BootstrapModalHeader typography compatibility exception as the
        // product decision dialog. Lab fault controls never enter production composition.
        var details = new BootstrapLabel
        {
            Text = summary, AutoSize = true, MaximumSize = new Size(530, 0), Dock = DockStyle.Top, TabStop = false,
            AccessibleName = "Bản xem trước nhập quyền", AccessibleDescription = summary
        };
        var body = new BootstrapPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), SurfaceRole = BootstrapSurfaceRole.Surface };
        body.Controls.Add(details);
        body.SizeChanged += (_, _) => details.MaximumSize = new Size(Math.Max(1, body.ClientSize.Width - 40), 0);
        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8)
        };
        var cancel = new BootstrapButton { Text = "Hủy", AutoSize = true, Variant = BootstrapVariant.Secondary, Outline = true };
        var instruction = new BootstrapLabel
        {
            Text = "Công cụ phát triển — UI Lab: tạo biến cố sau Preview, trước Apply.",
            Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), TextRole = BootstrapTextRole.Warning
        };
        var faults = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new Padding(8) };
        faults.Controls.AddRange(new Control[] { AdvanceTargetRevisionButton, RotateCatalogDigestButton, SwitchTargetButton });
        foreach (var button in new[] { AdvanceTargetRevisionButton, RotateCatalogDigestButton, SwitchTargetButton })
        { ((BootstrapButton)button).Variant = BootstrapVariant.Secondary; ((BootstrapButton)button).Outline = true; }
        controls.Controls.AddRange(new Control[]
        {
            ConfirmButton, cancel
        });
        Controls.Add(body);
        Controls.Add(faults);
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
        BusinessCompactAppearance.Apply(this);
    }
}
