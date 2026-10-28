using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

internal sealed class SecurityDecisionDialog : BootstrapModal
{
    internal Panel Body => BodyPanel;
    internal BootstrapLabel Message { get; } = new() { Dock = DockStyle.Top, AutoSize = true, TabStop = false };
    internal FlowLayoutPanel Actions => FooterPanel;

    internal SecurityDecisionDialog(string title, string message, string confirm = "Xác nhận")
    {
        ModalSize = BootstrapModalSize.Custom;
        BackdropMode = BootstrapModalBackdropMode.Static;
        FooterPanel.WrapContents = true;
        FooterPanel.AutoSize = true;
        FooterPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(610, 330);
        MinimumSize = new Size(530, 260);
        ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false;
        Message.Text = message;
        Message.AccessibleName = "Nội dung xác nhận";
        Message.AccessibleDescription = message;
        Body.Controls.Add(Message);
        AcceptButton = AddChoice(confirm, DialogResult.OK, BootstrapVariant.Primary);
        CancelButton = AddChoice("Hủy", DialogResult.Cancel, BootstrapVariant.Secondary);
        void FitMessage() => Message.MaximumSize = new Size(Math.Max(1, Body.ClientSize.Width - Body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth), 0);
        Body.SizeChanged += (_, _) => FitMessage();
        BusinessCompactAppearance.Apply(this);
        FitMessage();
    }

    internal BootstrapButton AddChoice(string text, DialogResult result, BootstrapVariant variant)
    {
        var button = new BootstrapButton { Text = text, AutoSize = true, DialogResult = result, Variant = variant, Outline = variant != BootstrapVariant.Primary };
        Actions.Controls.Add(button);
        return button;
    }

    internal DialogResult ShowOwned(SecurityWorkspaceControl owner)
    {
        void Boundary(object? sender, EventArgs args)
        {
            if ((!owner.ViewModel.IsCurrent || owner.IsDisposed) && !IsDisposed) { DialogResult = DialogResult.Cancel; Close(); }
        }
        if (!owner.ViewModel.IsCurrent || owner.IsDisposed) return DialogResult.Cancel;
        owner.ViewModel.Changed += Boundary;
        owner.Disposed += Boundary;
        try { return ShowDialog(owner); }
        finally { owner.ViewModel.Changed -= Boundary; owner.Disposed -= Boundary; }
    }
}
