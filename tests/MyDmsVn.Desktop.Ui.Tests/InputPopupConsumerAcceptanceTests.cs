using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class InputPopupConsumerAcceptanceTests
{
    [Theory]
    [InlineData(1200, false, false)]
    [InlineData(1200, true, false)]
    [InlineData(550, false, true)]
    [InlineData(550, true, true)]
    public void Catalog_filter_keeps_equal_native_input_heights_without_clipping_when_refresh_wraps(
        int hostWidth, bool dark, bool refreshWraps) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            var previousTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(
                    dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                var form = control.FindForm()!;
                form.Width = hostWidth;
                control.Tabs.SelectedIndex = 2;
                form.PerformLayout();
                System.Windows.Forms.Application.DoEvents();

                var catalog = control.PermissionCatalog;
                var filters = Assert.IsType<FlowLayoutPanel>(catalog.Search.Parent);
                var module = Assert.IsType<BootstrapComboBox>(catalog.Lifecycle);
                Assert.True(catalog.Search.IsHandleCreated && module.IsHandleCreated);
                Assert.Equal(catalog.Search.Height, module.Height);
                Assert.Equal(refreshWraps, catalog.RefreshButton.Top > catalog.Search.Top);
                Assert.All(filters.Controls.Cast<Control>(), filter =>
                {
                    Assert.True(filter.Right <= filters.ClientSize.Width,
                        $"{filter.AccessibleName ?? filter.Text} right {filter.Right}; filters {filters.ClientSize}");
                    Assert.True(filter.Bottom <= filters.ClientSize.Height,
                        $"{filter.AccessibleName ?? filter.Text} bottom {filter.Bottom}; filters {filters.ClientSize}");
                });
                Console.WriteLine($"CATALOG_FILTER host={hostWidth} dark={dark} dpi={form.DeviceDpi} " +
                    $"width={filters.ClientSize.Width} search={catalog.Search.Bounds} combo={module.Bounds} " +
                    $"refresh={catalog.RefreshButton.Bounds}");
            }
            finally { BootstrapThemeManager.CurrentTheme = previousTheme; }
        });
}
