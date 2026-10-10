using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
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
        private readonly ICatalogControlFactory? _catalogControlFactory;
        private readonly int _uiThreadId;
        private long? _workspaceSessionVersion;

        public FoundationShellForm(FoundationViewModel viewModel)
            : this(viewModel, null, null, null, true)
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
                null,
                true)
        {
        }

        public FoundationShellForm(
            FoundationViewModel viewModel,
            IDesktopSession session,
            IDesktopNotificationService notifications,
            ICatalogControlFactory catalogControlFactory)
            : this(
                viewModel,
                session ?? throw new ArgumentNullException(nameof(session)),
                notifications ?? throw new ArgumentNullException(nameof(notifications)),
                catalogControlFactory ?? throw new ArgumentNullException(nameof(catalogControlFactory)),
                true)
        {
        }

        private FoundationShellForm(
            FoundationViewModel viewModel,
            IDesktopSession? session,
            IDesktopNotificationService? notifications,
            ICatalogControlFactory? catalogControlFactory,
            bool initialize)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _session = session;
            _notifications = notifications;
            _catalogControlFactory = catalogControlFactory;
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;

            Text = "MyDmsVn";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            Size = new Size(1200, 760);
            AutoScaleMode = AutoScaleMode.Dpi;

            Navigation = CreateNavigation();
            Navigation.SelectedItemChanged += OnNavigationSelectedItemChanged;
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
                ApplySessionState();
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

        public long WorkspaceSessionVersion => _session?.Version ?? 0;

        public TabPage? OpenCatalog(CatalogKind kind)
        {
            if (_catalogControlFactory == null)
            {
                return null;
            }

            var content = _catalogControlFactory.Create(kind);
            var page = OpenWorkspace(
                "catalog-" + kind.ToString().ToLowerInvariant(),
                GetCatalogTitle(kind),
                content,
                WorkspaceSessionVersion);
            if (page != null && page.Controls.Contains(content) && content is CatalogControl catalog)
            {
                _ = catalog.ActivateAsync(CancellationToken.None);
            }

            return page;
        }

        public TabPage? OpenWorkspace(
            string key,
            string title,
            Control content,
            long sessionVersion)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A workspace key is required.", nameof(key));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!IsWorkspaceRequestCurrent(sessionVersion))
            {
                content.Dispose();
                return null;
            }

            if (_documents.TryGetValue(key, out var existing))
            {
                if (!existing.Controls.Contains(content))
                {
                    content.Dispose();
                }

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

            if (!IsWorkspaceRequestCurrent(sessionVersion))
            {
                CloseAllWorkspaces();
                return null;
            }

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

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            ApplyViewModelState();
            if (_session != null)
            {
                ApplySessionState();
            }

            var lastNotification = _notifications?.LastNotification;
            if (lastNotification != null)
            {
                SetStatus(lastNotification.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                Navigation.SelectedItemChanged -= OnNavigationSelectedItemChanged;
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
            DispatchToUi(ApplySessionState);
        }

        private void OnNotificationPublished(object? sender, DesktopNotification notification)
        {
            DispatchToUi(() => SetStatus(notification.Message));
        }

        private void OnNavigationSelectedItemChanged(object? sender, EventArgs eventArgs)
        {
            if (Navigation.SelectedItem?.Tag is CatalogKind kind)
            {
                OpenCatalog(kind);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            DispatchToUi(ApplyViewModelState);
        }

        private void DispatchToUi(Action action)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                action();
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(
                    (Action)(() =>
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            action();
                        }
                    }));
            }
            catch (InvalidOperationException)
            {
            }
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

        private void ApplySessionState()
        {
            var isAuthenticated = _session?.IsAuthenticated ?? true;
            var sessionVersion = _session?.Version;
            var sessionChanged = _workspaceSessionVersion.HasValue &&
                sessionVersion.HasValue &&
                _workspaceSessionVersion.Value != sessionVersion.Value;
            Navigation.Enabled = isAuthenticated;
            Workspace.Enabled = isAuthenticated;
            if (!isAuthenticated || sessionChanged)
            {
                CloseAllWorkspaces();
            }

            _workspaceSessionVersion = sessionVersion;
            SetCurrentUser(_session?.CurrentUser?.DisplayName);
        }

        private void CloseAllWorkspaces()
        {
            var pages = new TabPage[_documents.Count];
            _documents.Values.CopyTo(pages, 0);
            Workspace.TabPages.Clear();
            _documents.Clear();
            foreach (var page in pages)
            {
                page.Dispose();
            }
        }

        private bool IsWorkspaceRequestCurrent(long sessionVersion)
        {
            return _session == null ||
                (_session.IsAuthenticated && _session.Version == sessionVersion);
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
            navigation.Items.Add(new BootstrapSidebarItem { Text = "Products", Tag = CatalogKind.Product });
            navigation.Items.Add(new BootstrapSidebarItem { Text = "Warehouses", Tag = CatalogKind.Warehouse });
            navigation.Items.Add(new BootstrapSidebarItem { Text = "Employees", Tag = CatalogKind.Employee });
            navigation.Items.Add(new BootstrapSidebarItem { Text = "Customers", Tag = CatalogKind.Customer });
            return navigation;
        }

        private static string GetCatalogTitle(CatalogKind kind)
        {
            switch (kind)
            {
                case CatalogKind.Product:
                    return "Products";
                case CatalogKind.Warehouse:
                    return "Warehouses";
                case CatalogKind.Employee:
                    return "Employees";
                case CatalogKind.Customer:
                    return "Customers";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
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
