using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class BusinessCompactNavigationTests
    {
        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Selection_with_vertical_overflow_keeps_horizontal_scrollbar_hidden(BootstrapThemeMode mode) => StaTest.Run(_ =>
        {
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard();
                using var form = new Form { ClientSize = new Size(900, 600) };
                using var navigation = new BusinessCompactNavigation { Dock = DockStyle.Left };
                form.Controls.Add(navigation);
                for (var index = 0; index < 24; index++)
                    navigation.Items.Add(new BusinessCompactNavigationItem { Text = "Item " + index, Group = index < 12 ? "First" : "Second" });
                form.Show();
                Pump();
                var host = navigation.Controls.OfType<FlowLayoutPanel>().Single();
                Assert.True(host.VerticalScroll.Visible, "Expected a real vertical scrollbar before selection.");
                AssertHorizontalFit(host);

                navigation.SelectedItem = navigation.Items[4];
                Pump();
                AssertHorizontalFit(host);
                var button = host.Controls.OfType<Button>().Single(row => ReferenceEquals(row.Tag, navigation.Items[7]));
                button.PerformClick();
                Pump();
                Assert.Same(navigation.Items[7], navigation.SelectedItem);
                AssertHorizontalFit(host);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(15));

        [Fact]
        public void Selection_preserves_focus_metadata_and_event_count() => StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var form = new Form { ClientSize = new Size(900, 600) };
            using var navigation = new BusinessCompactNavigation { Dock = DockStyle.Left };
            form.Controls.Add(navigation);
            using var workspaceInput = new TextBox { Dock = DockStyle.Fill };
            form.Controls.Add(workspaceInput);
            var first = new BusinessCompactNavigationItem { Text = "First" };
            var section = new BusinessCompactNavigationItem { Text = "Section", Expanded = true };
            var child = new BusinessCompactNavigationItem { Text = "Child" };
            section.Items.Add(child);
            navigation.Items.Add(first);
            navigation.Items.Add(section);
            for (var index = 0; index < 18; index++) navigation.Items.Add(new BusinessCompactNavigationItem { Text = "Other " + index });
            form.Show(); Pump();
            var host = navigation.Controls.OfType<FlowLayoutPanel>().Single();
            var firstButton = host.Controls.OfType<Button>().Single(row => ReferenceEquals(row.Tag, first));
            var childButton = host.Controls.OfType<Button>().Single(row => ReferenceEquals(row.Tag, child));
            var events = 0;
            navigation.SelectedItemChanged += (_, __) => events++;
            Assert.True(firstButton.Focus());
            var scroll = host.AutoScrollPosition.Y;
            navigation.SelectedItem = first;
            Pump();
            Assert.Same(firstButton, host.Controls.OfType<Button>().Single(row => ReferenceEquals(row.Tag, first)));
            Assert.True(firstButton.Focused);
            Assert.Equal(scroll, host.AutoScrollPosition.Y);
            Assert.Contains("Selected", firstButton.AccessibleDescription);
            Assert.Equal(1, events);
            navigation.SelectedItem = child;
            Pump();
            Assert.Same(childButton, host.Controls.OfType<Button>().Single(row => ReferenceEquals(row.Tag, child)));
            Assert.DoesNotContain("Selected", firstButton.AccessibleDescription);
            Assert.Contains("Selected", childButton.AccessibleDescription);
            Assert.Equal(2, events);
            navigation.SelectedItem = child;
            Assert.Equal(2, events);
            navigation.SelectedItem = null;
            Assert.DoesNotContain("Selected", childButton.AccessibleDescription);
            Assert.Equal(3, events);
            section.Expanded = false;
            Pump();
            navigation.SelectedItem = child;
            Assert.True(child.Selected);
            Assert.DoesNotContain(host.Controls.OfType<Button>(), row => ReferenceEquals(row.Tag, child));
            section.Expanded = true;
            Pump();
            Assert.Contains("Selected", host.Controls.OfType<Button>()
                .Single(row => ReferenceEquals(row.Tag, child)).AccessibleDescription);
            host.ScrollControlIntoView(host.Controls.OfType<Button>().Last());
            Pump();
            Assert.True(host.AutoScrollPosition.Y < 0);
            Assert.True(workspaceInput.Focus());
            var scrolledPosition = host.AutoScrollPosition.Y;
            navigation.SelectedItem = first;
            Pump();
            Assert.True(workspaceInput.Focused);
            Assert.Equal(scrolledPosition, host.AutoScrollPosition.Y);
            navigation.SelectedItem = null;
            Pump();
            Assert.Equal(scrolledPosition, host.AutoScrollPosition.Y);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(15));

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Viewport_changes_preserve_horizontal_fit_and_vertical_reachability(BootstrapThemeMode mode) => StaTest.Run(_ =>
        {
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard();
                using var form = new Form { ClientSize = new Size(900, 600) };
                using var navigation = new BusinessCompactNavigation { Dock = DockStyle.Left };
                form.Controls.Add(navigation);
                var section = new BusinessCompactNavigationItem { Text = "Long nested section with ellipsis", Expanded = true };
                section.Items.Add(new BusinessCompactNavigationItem { Text = new string('L', 90) });
                navigation.Items.Add(section);
                for (var index = 0; index < 20; index++) navigation.Items.Add(new BusinessCompactNavigationItem { Text = "Row " + index });
                form.Show(); Pump();
                var host = navigation.Controls.OfType<FlowLayoutPanel>().Single();
                Assert.True(host.VerticalScroll.Visible);
                AssertHorizontalFit(host);
                var last = host.Controls.OfType<Button>().Last(button => button.Enabled);
                host.ScrollControlIntoView(last); Pump();
                Assert.InRange(last.Top, 0, host.ClientSize.Height);
                Assert.True(last.Bottom <= host.ClientSize.Height);
                AssertHorizontalFit(host);

                navigation.Collapse(); Pump();
                Assert.Equal(64 * navigation.DeviceDpi / 96, navigation.Width);
                AssertHorizontalFit(host);
                navigation.Expand(); Pump();
                Assert.Equal(224 * navigation.DeviceDpi / 96, navigation.Width);
                AssertHorizontalFit(host);
                form.ClientSize = new Size(900, 1400); Pump();
                while (host.VerticalScroll.Visible && navigation.Items.Count > 1)
                {
                    navigation.Items.RemoveAt(navigation.Items.Count - 1);
                    Pump();
                }
                Assert.False(host.VerticalScroll.Visible);
                AssertHorizontalFit(host);
                for (var index = 0; index < 10; index++) navigation.Items.Add(new BusinessCompactNavigationItem { Text = "Restored " + index });
                Pump();
                form.ClientSize = new Size(900, 600); Pump();
                Assert.True(host.VerticalScroll.Visible);
                AssertHorizontalFit(host);
                navigation.Items.RemoveAt(navigation.Items.Count - 1); Pump();
                AssertHorizontalFit(host);
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(15));

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Vertical_scrollbar_threshold_preserves_horizontal_fit_at_fixed_viewport(BootstrapThemeMode mode) => StaTest.Run(_ =>
        {
            var original = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard();
                var fixedSize = new Size(900, 600);
                using var form = new Form { ClientSize = fixedSize };
                using var navigation = new BusinessCompactNavigation { Dock = DockStyle.Left };
                form.Controls.Add(navigation);
                form.Show(); Pump();
                var host = navigation.Controls.OfType<FlowLayoutPanel>().Single();
                Assert.False(host.VerticalScroll.Visible);
                AssertHorizontalFit(host);

                for (var index = 0; index < 30 && !host.VerticalScroll.Visible; index++)
                {
                    navigation.Items.Add(new BusinessCompactNavigationItem { Text = "Row " + index });
                    Pump();
                    Assert.Equal(fixedSize, form.ClientSize);
                    AssertHorizontalFit(host);
                }
                Assert.True(host.VerticalScroll.Visible, "Expected the vertical scrollbar to appear while adding one row at a time.");

                while (navigation.Items.Count > 0 && host.VerticalScroll.Visible)
                {
                    navigation.Items.RemoveAt(navigation.Items.Count - 1);
                    Pump();
                    Assert.Equal(fixedSize, form.ClientSize);
                    AssertHorizontalFit(host);
                }
                Assert.False(host.VerticalScroll.Visible, "Expected the vertical scrollbar to disappear while removing one row at a time.");
                Assert.Empty(guard.Exceptions);
            }
            finally { BootstrapThemeManager.CurrentTheme = original; }
        }, TimeSpan.FromSeconds(15));

        internal static void AssertHorizontalFit(FlowLayoutPanel host)
        {
            Assert.True(!host.HorizontalScroll.Visible,
                $"horizontal visible; client={host.ClientSize}, display={host.DisplayRectangle}, auto={host.AutoScrollPosition}, " +
                string.Join("; ", host.Controls.Cast<Control>().Take(4).Select(row =>
                    $"{row.Name}/{row.Text} bounds={row.Bounds} margin={row.Margin}")));
            Assert.Equal(0, host.AutoScrollPosition.X);
            foreach (Control row in host.Controls)
                Assert.True(row.Right + row.Margin.Right <= host.ClientSize.Width - host.Padding.Right,
                    $"{row.Name}/{row.Text}: right={row.Right}, margin={row.Margin.Right}, viewport={host.ClientSize.Width}");
        }

        internal static void Pump()
        {
            for (var index = 0; index < 4; index++)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
        }
    }
}
