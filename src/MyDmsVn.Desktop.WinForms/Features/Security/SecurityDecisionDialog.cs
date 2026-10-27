using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

// BC-08: c232a4c BootstrapModalHeader.ApplyTypography disposes an equal Font still
// retained by its title Label. Public BodyPanel/FooterPanel cannot bypass that path.
// Retain OS chrome until T06 disposition; use toolkit content and native modal keys.
internal sealed class SecurityDecisionDialog : Form
{
    internal BootstrapPanel Body { get; } = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true, SurfaceRole = BootstrapSurfaceRole.Surface };
    internal BootstrapLabel Message { get; } = new() { Dock = DockStyle.Top, AutoSize = true, TabStop = false };
    internal FlowLayoutPanel Actions { get; } = new() { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

    internal SecurityDecisionDialog(string title, string message, string confirm = "Xác nhận")
    {
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
        Controls.Add(Body);
        Controls.Add(Actions);
        AcceptButton = AddChoice(confirm, DialogResult.OK, BootstrapVariant.Primary);
        CancelButton = AddChoice("Hủy", DialogResult.Cancel, BootstrapVariant.Secondary);
        Body.SizeChanged += (_, _) => Message.MaximumSize = new Size(Math.Max(1, Body.ClientSize.Width - Body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth), 0);
        BusinessCompactAppearance.Apply(this);
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
