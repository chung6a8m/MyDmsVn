using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class FoundationShellForm : Form
    {
        private readonly Dictionary<string, TabPage> _documents =
            new Dictionary<string, TabPage>(StringComparer.Ordinal);
        private readonly FoundationViewModel _viewModel;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly ToolStripStatusLabel _currentUserLabel;

        public FoundationShellForm(FoundationViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            Text = "MyDmsVn";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            Size = new Size(1200, 760);
            AutoScaleMode = AutoScaleMode.Dpi;

            Navigation = CreateNavigation();
            Workspace = new BootstrapTabControl
            {
                Dock = DockStyle.Fill,
            };

            var topBar = CreateTopBar();
            var statusBar = new BootstrapStatusStrip
            {
                Dock = DockStyle.Bottom,
                SizingGrip = false,
            };
            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _currentUserLabel = new ToolStripStatusLabel("Not signed in");
            statusBar.Items.Add(_statusLabel);
            statusBar.Items.Add(_currentUserLabel);

            Controls.Add(Workspace);
            Controls.Add(Navigation);
            Controls.Add(topBar);
            Controls.Add(statusBar);
        }

        public FoundationViewModel ViewModel => _viewModel;

        public BootstrapSidebar Navigation { get; }

        public BootstrapTabControl Workspace { get; }

        public string StatusText => _statusLabel.Text ?? string.Empty;

        public string CurrentUserText => _currentUserLabel.Text ?? string.Empty;

        public TabPage OpenWorkspace(string key, string title, Control content)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A workspace key is required.", nameof(key));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (_documents.TryGetValue(key, out var existing))
            {
                Workspace.SelectedTab = existing;
                return existing;
            }

            var page = new TabPage(title ?? string.Empty)
            {
                Name = key,
            };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            _documents.Add(key, page);
            Workspace.TabPages.Add(page);
            Workspace.SelectedTab = page;
            return page;
        }

        public void SetStatus(string status)
        {
            _statusLabel.Text = string.IsNullOrWhiteSpace(status) ? "Ready" : status;
        }

        public void SetCurrentUser(string? displayName)
        {
            _currentUserLabel.Text = string.IsNullOrWhiteSpace(displayName)
                ? "Not signed in"
                : displayName;
        }

        public void ToggleTheme()
        {
            var nextMode = BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Light
                ? BootstrapThemeMode.Dark
                : BootstrapThemeMode.Light;
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(nextMode);
        }

        private static BootstrapSidebar CreateNavigation()
        {
            var navigation = new BootstrapSidebar
            {
                Dock = DockStyle.Left,
                ExpandedWidth = 240,
                CollapsedWidth = 64,
            };
            navigation.Items.Add(
                new BootstrapSidebarItem
                {
                    Text = "Foundation",
                    Tag = "foundation",
                });
            return navigation;
        }

        private BootstrapToolStrip CreateTopBar()
        {
            var topBar = new BootstrapToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
            };
            var title = new ToolStripLabel("MyDmsVn");
            var themeButton = new ToolStripButton("Light / Dark")
            {
                Alignment = ToolStripItemAlignment.Right,
            };
            themeButton.Click += (_, _) => ToggleTheme();
            topBar.Items.Add(title);
            topBar.Items.Add(themeButton);
            return topBar;
        }
    }
}
