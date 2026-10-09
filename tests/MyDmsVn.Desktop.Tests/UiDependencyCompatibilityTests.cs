using System;
using System.Drawing;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using Xunit;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class UiDependencyCompatibilityTests
    {
        [Fact]
        public void Bootstrap_source_grid_tracks_runtime_theme_changes()
        {
            StaTest.Run(
                _ =>
                {
                    var originalTheme = BootstrapThemeManager.CurrentTheme;
                    try
                    {
                        using (var grid = new BootstrapGrid())
                        {
                            BootstrapThemeManager.CurrentTheme =
                                BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);

                            Assert.Equal(
                                BootstrapThemeMode.Dark,
                                BootstrapThemeManager.CurrentTheme.Mode);
                            Assert.False(grid.IsDisposed);
                        }
                    }
                    finally
                    {
                        BootstrapThemeManager.CurrentTheme = originalTheme;
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Bootstrap_source_grid_constructs_and_renders_without_host_startup_state()
        {
            StaTest.Run(
                _ =>
                {
                    using (var grid = new BootstrapGrid())
                    using (var bitmap = new Bitmap(320, 180))
                    {
                        grid.Size = bitmap.Size;
                        grid.Redim(2, 2);
                        grid.CreateControl();

                        grid.DrawToBitmap(bitmap, grid.ClientRectangle);

                        Assert.True(grid.IsHandleCreated);
                    }
                },
                TimeSpan.FromSeconds(10));
        }
    }
}
