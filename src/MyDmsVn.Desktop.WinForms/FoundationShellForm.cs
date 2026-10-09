using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly ToolStripProgressBar _busyIndicator;
        private readonly IDesktopSession? _session;
        private readonly IDesktopNotificationService? _notifications;

        public FoundationShellForm(FoundationViewModel viewModel)
            : this(viewModel, null, null, true)
        {
        }

        public FoundationShellForm(
            FoundationViewModel viewModel,
            IDesktopSession session,
            IDesktopNotificationService notifications)
            : this(
                viewModel,
                session ?? throw new ArgumentNullException(nameof(session)),
                notifications ?? throw new ArgumentNullException(nameof(notifications)),
                true)
        {
        }

        private FoundationShellForm(
            FoundationViewModel viewModel,
            IDesktopSession? session,
            IDesktopNotificationService? notifications,
            bool initialize)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _session = session;
            _notifications = notifications;

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
            _busyIndicator = new ToolStripProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
            };
            statusBar.Items.Add(_statusLabel);
            statusBar.Items.Add(_busyIndicator);
            statusBar.Items.Add(_currentUserLabel);

            Controls.Add(Workspace);
            Controls.Add(Navigation);
            Controls.Add(topBar);
            Controls.Add(statusBar);

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyViewModelState();

            if (_session != null)
            {
                _session.SessionChanged += OnSessionChanged;
                SetCurrentUser(_session.CurrentUser?.DisplayName);
            }

            if (_notifications != null)
            {
                _notifications.NotificationPublished += OnNotificationPublished;
            }
        }

        public FoundationViewModel ViewModel => _viewModel;

        public BootstrapSidebar Navigation { get; }

        public BootstrapTabControl Workspace { get; }

        public string StatusText => _statusLabel.Text ?? string.Empty;

        public string CurrentUserText => _currentUserLabel.Text ?? string.Empty;

        public bool BusyIndicatorVisible => _busyIndicator.Available;

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

        public void SignOut()
        {
            _session?.SignOut();
            PublishSessionMessage(
                DesktopNotificationKind.Information,
                "You have been signed out.");
        }

        public void HandleSessionExpired()
        {
            _session?.SignOut();
            PublishSessionMessage(
                DesktopNotificationKind.Warning,
                "Your session has expired. Please sign in again.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                if (_session != null)
                {
                    _session.SessionChanged -= OnSessionChanged;
                }

                if (_notifications != null)
                {
                    _notifications.NotificationPublished -= OnNotificationPublished;
                }
            }

            base.Dispose(disposing);
        }

        private void OnSessionChanged(object? sender, EventArgs eventArgs)
        {
            SetCurrentUser(_session?.CurrentUser?.DisplayName);
        }

        private void OnNotificationPublished(object? sender, DesktopNotification notification)
        {
            SetStatus(notification.Message);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)ApplyViewModelState);
                return;
            }

            ApplyViewModelState();
        }

        private void ApplyViewModelState()
        {
            _busyIndicator.Available = _viewModel.IsBusy;
            if (!string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
            {
                SetStatus(_viewModel.ErrorMessage!);
            }
            else if (_viewModel.IsEmpty)
            {
                SetStatus("No data.");
            }
            else if (_viewModel.IsReady)
            {
                SetStatus(string.IsNullOrWhiteSpace(_viewModel.Runtime)
                    ? "Ready"
                    : $"Ready ({_viewModel.Runtime})");
            }
        }

        private void PublishSessionMessage(DesktopNotificationKind kind, string message)
        {
            if (_notifications == null)
            {
                SetStatus(message);
                return;
            }

            _notifications.Publish(new DesktopNotification(kind, message));
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
            var signOutButton = new ToolStripButton("Sign out")
            {
                Alignment = ToolStripItemAlignment.Right,
            };
            signOutButton.Click += (_, _) => SignOut();
            topBar.Items.Add(title);
            topBar.Items.Add(signOutButton);
            topBar.Items.Add(themeButton);
            return topBar;
        }
    }
}
