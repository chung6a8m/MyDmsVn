using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.WinForms;

namespace MyDmsVn.Desktop.UiLab;

internal sealed class SecurityImportReviewDialog : BootstrapModal
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
        ModalSize = BootstrapModalSize.Custom;
        BackdropMode = BootstrapModalBackdropMode.Static;
        FooterPanel.WrapContents = true;
        FooterPanel.AutoSize = true;
        FooterPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Text = "Xem trước nhập quyền — UI Lab";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(530, 260);
        Size = new Size(610, 300);
        ShowInTaskbar = false;

        // Lab fault controls remain inside public body content and never enter production.
        var details = new BootstrapLabel
        {
            Text = summary, AutoSize = true, MaximumSize = new Size(530, 0), Dock = DockStyle.Top, TabStop = false,
            AccessibleName = "Bản xem trước nhập quyền", AccessibleDescription = summary
        };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void FitDetails() => details.MaximumSize = new Size(Math.Max(1, BodyPanel.ClientSize.Width - BodyPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth), 0);
        BodyPanel.SizeChanged += (_, _) => FitDetails();
        var controls = FooterPanel;
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
        content.Controls.Add(details);
        content.Controls.Add(instruction);
        content.Controls.Add(faults);
        BodyPanel.Controls.Add(content);

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
        InitialFocusControl = ConfirmButton;
        BusinessCompactAppearance.Apply(this);
        FitDetails();
    }
}
