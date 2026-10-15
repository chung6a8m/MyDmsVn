using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class LoginForm : Form
    {
        private readonly LoginViewModel _viewModel;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Label _errorSummary;
        private readonly ProgressBar _busyIndicator;
        private readonly CheckBox _showPassword;
        private bool _disposed;

        public LoginForm(LoginViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            Text = "MyDmsVn — Đăng nhập";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(420, 300);
            MinimumSize = new Size(420, 300);

            UsernameTextBox = new TextBox { Dock = DockStyle.Fill, TabIndex = 0 };
            PasswordTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                TabIndex = 1,
                UseSystemPasswordChar = true,
            };
            _showPassword = new CheckBox
            {
                Text = "Hiện mật khẩu",
                AutoSize = true,
                TabIndex = 2,
            };
            _errorSummary = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.Firebrick,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _busyIndicator = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
            };
            LoginButton = new Button
            {
                Text = "Đăng nhập",
                AutoSize = true,
                TabIndex = 3,
            };
            CancelButtonControl = new Button
            {
                Text = "Thoát",
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                TabIndex = 4,
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
            };
            buttons.Controls.Add(LoginButton);
            buttons.Controls.Add(CancelButtonControl);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = 9,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = "Đăng nhập",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
            });
            layout.Controls.Add(new Label { Text = "Tên đăng nhập", AutoSize = true });
            layout.Controls.Add(UsernameTextBox);
            layout.Controls.Add(new Label { Text = "Mật khẩu", AutoSize = true });
            layout.Controls.Add(PasswordTextBox);
            layout.Controls.Add(_showPassword);
            layout.Controls.Add(_busyIndicator);
            layout.Controls.Add(_errorSummary);
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            AcceptButton = LoginButton;
            CancelButton = CancelButtonControl;
            LoginButton.Click += OnLoginClick;
            _showPassword.CheckedChanged += OnShowPasswordChanged;
            UsernameTextBox.TextChanged += OnUsernameChanged;
            PasswordTextBox.TextChanged += OnPasswordChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyViewModelState();
        }

        public TextBox UsernameTextBox { get; }

        public TextBox PasswordTextBox { get; }

        public Button LoginButton { get; }

        public Button CancelButtonControl { get; }

        public LoginViewModel ViewModel => _viewModel;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposed)
                {
                    base.Dispose(disposing);
                    return;
                }

                _disposed = true;
                _lifetime.Cancel();
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                LoginButton.Click -= OnLoginClick;
                _showPassword.CheckedChanged -= OnShowPasswordChanged;
                UsernameTextBox.TextChanged -= OnUsernameChanged;
                PasswordTextBox.TextChanged -= OnPasswordChanged;
                _viewModel.Dispose();
                _lifetime.Dispose();
            }

            base.Dispose(disposing);
        }

        private async void OnLoginClick(object? sender, EventArgs eventArgs)
        {
            await _viewModel.LoginAsync(_lifetime.Token);
            if (_viewModel.IsAuthenticated && !IsDisposed)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void OnShowPasswordChanged(object? sender, EventArgs eventArgs)
        {
            PasswordTextBox.UseSystemPasswordChar = !_showPassword.Checked;
        }

        private void OnUsernameChanged(object? sender, EventArgs eventArgs) =>
            _viewModel.Username = UsernameTextBox.Text;

        private void OnPasswordChanged(object? sender, EventArgs eventArgs) =>
            _viewModel.Password = PasswordTextBox.Text;

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (_disposed || IsDisposed || Disposing)
            {
                return;
            }

            if (InvokeRequired && IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(ApplyViewModelState));
                }
                catch (InvalidOperationException) when (_disposed || IsDisposed || Disposing || !IsHandleCreated)
                {
                    // The form closed between the handle check and BeginInvoke.
                }

                return;
            }

            ApplyViewModelState();
        }

        private void ApplyViewModelState()
        {
            if (PasswordTextBox.Text != _viewModel.Password)
            {
                PasswordTextBox.Text = _viewModel.Password;
            }

            _busyIndicator.Visible = _viewModel.IsBusy;
            LoginButton.Enabled = !_viewModel.IsBusy;
            UsernameTextBox.Enabled = !_viewModel.IsBusy;
            PasswordTextBox.Enabled = !_viewModel.IsBusy;
            _errorSummary.Text = _viewModel.ErrorMessage ?? string.Empty;
        }
    }
}
