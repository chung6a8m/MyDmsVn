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
        [InlineData(CatalogKind.Product)]
        [InlineData(CatalogKind.Warehouse)]
        [InlineData(CatalogKind.Employee)]
        [InlineData(CatalogKind.Customer)]
        public void Accessible_open_row_uses_the_existing_selection_path_and_rejects_stale_rows(CatalogKind kind)
        {
            StaTest.Run(_ =>
            {
                using var guard = new WinFormsTestGuard();
                using var scenario = CatalogAcceptanceTests.CreateAccessibilityScenario(kind, out var control);
                var code = Convert.ToString(control.Grid[1, 1].Value)!;
                control.NewButton.PerformClick();
                var action = AccessibleDescendants(control.Grid.AccessibilityObject)
                    .SingleOrDefault(item => item.Role == AccessibleRole.PushButton && item.Name == "Open " + code);
                Assert.NotNull(action);
                action!.DoDefaultAction();
                Assert.True(control.LastOperation.Wait(TimeSpan.FromSeconds(3)));
                control.LastOperation.GetAwaiter().GetResult();
                Assert.Equal(code, control.EditorControls["code"].Text);
                var cell = AccessibleDescendants(control.Grid.AccessibilityObject)
                    .First(item => item.Role == AccessibleRole.Cell && item.Name == code);
                Assert.True((cell.State & AccessibleStates.ReadOnly) != 0);
                Assert.Throws<InvalidOperationException>(() => cell.Value = "MUST NOT CHANGE");
                Assert.Equal(code, control.Grid[1, 1].Value);
                control.NewButton.PerformClick();
                control.Grid.Redim(1, 4);
                action.DoDefaultAction();
                Assert.Empty(control.EditorControls["code"].Text);
                Assert.True((action.State & AccessibleStates.Unavailable) != 0);
                Assert.Empty(guard.Exceptions);
            }, TimeSpan.FromSeconds(10));
        }

        private static IEnumerable<AccessibleObject> AccessibleDescendants(AccessibleObject root)
        {
            for (var index = 0; index < root.GetChildCount(); index++)
            {
                var child = root.GetChild(index);
                if (child == null) continue;
                yield return child;
                foreach (var descendant in AccessibleDescendants(child)) yield return descendant;
            }
        }

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
