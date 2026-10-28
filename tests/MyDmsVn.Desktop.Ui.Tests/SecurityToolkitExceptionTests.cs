using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Icons;
using MyDmsVn.Bootstrap5WinFormUI.Rendering;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

[Trait("Module", "Security")]
public sealed class SecurityToolkitExceptionTests
{
    [Fact]
    public void Decision_and_import_modals_keep_public_content_theme_and_commands()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                using var decision = new SecurityDecisionDialog("Đổi trạng thái", new string('W', 200));
                using var import = new SecurityImportReviewDialog("Bản xem trước nhập quyền", () => { }, () => { });
                foreach (var dialog in new BootstrapModal[] { decision, import })
                {
                    Assert.Equal(FormBorderStyle.None, dialog.FormBorderStyle);
                    Assert.Equal(BootstrapModalSize.Custom, dialog.ModalSize);
                    Assert.Equal(BootstrapModalBackdropMode.Static, dialog.BackdropMode);
                    Assert.Same(dialog.FooterPanel, ((Control)dialog.AcceptButton).Parent);
                    Assert.Same(dialog.FooterPanel, ((Control)dialog.CancelButton).Parent);
                    Assert.NotEmpty(dialog.BodyPanel.Controls.Cast<Control>());
                    // Public content added after Apply must join the app typography/lifetime owner.
                    var dynamicAction = new BootstrapButton { Text = "Lệnh bổ sung", AutoSize = true };
                    dialog.FooterPanel.Controls.Add(dynamicAction);
                    foreach (var mode in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
                    {
                        BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
                        dialog.PerformLayout();
                        Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, dialog.BackColor);
                        Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, dialog.BodyPanel.BackColor);
                        Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, dialog.FooterPanel.BackColor);
                        Assert.Equal(10f, dynamicAction.Font.SizeInPoints);
                    }
                }
                Assert.Contains(import.AdvanceTargetRevisionButton, Descendants(import.BodyPanel));
                Assert.Contains(import.RotateCatalogDigestButton, Descendants(import.BodyPanel));
                Assert.True(decision.Message.MaximumSize.Width > 0);
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        });

    [Fact]
    public void Multiline_security_details_preserve_native_selection_and_readonly_theme()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                foreach (var field in new[] { control.UserDetail.Explanation, control.PermissionCatalog.Details })
                {
                    Assert.IsType<BootstrapTextBox>(field);
                    Assert.True(field.Multiline && field.ReadOnly && field.WordWrap);
                    Assert.Equal(ScrollBars.Vertical, field.ScrollBars);
                    var text = "Dòng một: quyền đã tải\r\nDòng hai: giữ nguyên nội dung";
                    field.Text = text;
                    var editor = Assert.Single(field.Controls.OfType<TextBox>());
                    editor.Select(3, 18);
                    foreach (var mode in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
                    {
                        BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
                        field.PerformLayout();
                        Assert.Same(editor, Assert.Single(field.Controls.OfType<TextBox>()));
                        Assert.Equal(text, editor.Text);
                        Assert.Equal(3, editor.SelectionStart);
                        Assert.Equal(18, editor.SelectionLength);
                        Assert.True(editor.ReadOnly && editor.Multiline);
                        Assert.Equal(field.AccessibleName, editor.AccessibleName);
                        Assert.True(editor.Height > editor.PreferredHeight);
                        Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.SurfaceSecondary, editor.BackColor);
                        Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, editor.ForeColor);
                    }
                }
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        });

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Business_compact_disabled_actions_paint_readable_distinct_labels_and_stay_inert(BootstrapThemeMode mode)
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                var theme = BusinessCompactAppearance.CreateTheme(mode);
                BootstrapThemeManager.CurrentTheme = theme;
                var renderer = new RecordingActionIconRenderer();
                using var button = new BootstrapButton
                {
                    Text = "Làm mới",
                    Bounds = new Rectangle(12, 12, 220, 40),
                    Variant = BootstrapVariant.Secondary,
                    Outline = true,
                    ButtonSize = BootstrapButtonSize.Small,
                    Icon = IconDescriptor.Framework(FrameworkIconGlyph.Check),
                    IconRenderer = renderer,
                    Enabled = false
                };
                var activations = 0;
                button.Click += (_, _) => activations++;
                // Adding public app content uses the same appearance owner and font as Security actions.
                control.Controls.Add(button);
                button.BringToFront();
                NativeLookupTypingDriver.Activate(control.FindForm()!);
                Assert.Equal(10f, button.Font.SizeInPoints);

                button.Enabled = true;
                using var enabledImage = new Bitmap(button.Width, button.Height);
                button.DrawToBitmap(enabledImage, button.ClientRectangle);
                var enabledBackground = enabledImage.GetPixel(8, 8);
                var enabledForeground = renderer.LastColor;
                Assert.Equal(theme.Colors.Secondary, enabledForeground);
                var clickPoint = new Point(button.Width / 2, button.Height / 2);
                NativeLookupTypingDriver.Click(button, clickPoint);
                Assert.Equal(1, activations);

                button.Enabled = false;
                renderer.LastColor = Color.Empty;
                using var disabledImage = new Bitmap(button.Width, button.Height);
                button.DrawToBitmap(disabledImage, button.ClientRectangle);
                var disabledBackground = disabledImage.GetPixel(8, 8);
                var disabledForeground = renderer.LastColor;
                Assert.Equal(theme.Colors.Text, disabledForeground);
                Assert.NotEqual(enabledBackground.ToArgb(), disabledBackground.ToArgb());
                Assert.NotEqual(enabledForeground.ToArgb(), disabledForeground.ToArgb());
                Assert.True(ColorUtil.GetContrastRatio(disabledForeground, disabledBackground) >= 4.5d,
                    "The actual Business Compact disabled paint must retain the product readability target in " + mode);
                Assert.True(CountTextPixels(disabledImage, theme.Colors.Text) >= 8,
                    "The label must use the readable foreground too; the icon renderer intentionally paints no pixels.");

                NativeLookupTypingDriver.Click(button, clickPoint);
                button.PerformClick();
                Assert.Equal(1, activations);
                Assert.False(button.Enabled);
                button.Enabled = true;
                NativeLookupTypingDriver.Click(button, clickPoint);
                Assert.Equal(2, activations);
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        });

    private static int CountTextPixels(Bitmap bitmap, Color foreground)
    {
        var count = 0;
        for (var y = 8; y < bitmap.Height - 8; y++)
            for (var x = 8; x < bitmap.Width - 8; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.R - foreground.R) <= 8 && Math.Abs(pixel.G - foreground.G) <= 8 && Math.Abs(pixel.B - foreground.B) <= 8)
                    count++;
            }
        return count;
    }

    private sealed class RecordingActionIconRenderer : IIconRenderer
    {
        internal Color LastColor { get; set; }
        public bool TryRender(Graphics graphics, IconDescriptor descriptor, Rectangle bounds, Color color)
        {
            LastColor = color;
            return true;
        }
    }

    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
