using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class CatalogAccessibilityAcceptanceTests
    {
        [Theory]
        [InlineData(CatalogKind.Product, BootstrapThemeMode.Light)]
        [InlineData(CatalogKind.Product, BootstrapThemeMode.Dark)]
        [InlineData(CatalogKind.Warehouse, BootstrapThemeMode.Light)]
        [InlineData(CatalogKind.Warehouse, BootstrapThemeMode.Dark)]
        [InlineData(CatalogKind.Employee, BootstrapThemeMode.Light)]
        [InlineData(CatalogKind.Employee, BootstrapThemeMode.Dark)]
        [InlineData(CatalogKind.Customer, BootstrapThemeMode.Light)]
        [InlineData(CatalogKind.Customer, BootstrapThemeMode.Dark)]
        public void Catalog_entity_fields_render_inside_editor_and_are_reachable_by_native_tab_navigation(
            CatalogKind kind, BootstrapThemeMode mode)
        {
            StaTest.Run(_ =>
            {
                var originalTheme = BootstrapThemeManager.CurrentTheme;
                using (var guard = new WinFormsTestGuard())
                {
                    try
                    {
                        using (var scenario = CatalogAcceptanceTests.CreateAccessibilityScenario(kind, out var control))
                        {
                            // Change the theme after construction to include the live theme event path.
                            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                            var host = control.FindForm()!;
                            host.PerformLayout();
                            control.PerformLayout();
                            System.Windows.Forms.Application.DoEvents();
                            var targets = new List<Control> { control.SearchBox, control.Grid, control.SaveButton };
                            targets.AddRange(control.EditorControls.Values);
                            foreach (var target in targets)
                            {
                                Assert.True(target.Visible && target.Enabled && target.CanSelect,
                                    kind + ": control cannot receive keyboard focus: " + target.GetType().Name);
                                var visibleBounds = host.RectangleToClient(target.RectangleToScreen(target.ClientRectangle));
                                Assert.True(host.ClientRectangle.Contains(visibleBounds),
                                    kind + ": control is clipped outside the form: " + visibleBounds);
                                Assert.True(target.Width > 0 && target.Height > 0);
                            }
                            foreach (var editor in control.EditorControls.Values)
                            {
                                Assert.False(string.IsNullOrWhiteSpace(editor.AccessibleName));
                                Assert.True(editor.Parent!.ClientRectangle.Contains(editor.Bounds),
                                    kind + ": editor is clipped: " + editor.AccessibleName);
                            }
                            using (var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height))
                                host.DrawToBitmap(bitmap, host.ClientRectangle);

                            // BootstrapTextBox.OnEnter transfers focus to its native child editor.
                            control.SearchBox.Select();
                            Assert.True(control.SearchBox.ContainsFocus);
                            var reached = new HashSet<Control>();
                            // SelectNextControl follows the same native tab order without SendKeys or screen coordinates.
                            for (var step = 0; step < 80 && reached.Count < targets.Count; step++)
                            {
                                foreach (var target in targets.Where(target => target.ContainsFocus)) reached.Add(target);
                                var focused = Descendants(host).FirstOrDefault(child => child.Focused);
                                Assert.NotNull(focused);
                                Assert.True(host.SelectNextControl(focused, true, true, true, true));
                                System.Windows.Forms.Application.DoEvents();
                            }
                            Assert.All(targets, target => Assert.Contains(target, reached));
                        }
                    }
                    finally
                    {
                        BootstrapThemeManager.CurrentTheme = originalTheme;
                    }
                }
            }, TimeSpan.FromSeconds(15));
        }

        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
