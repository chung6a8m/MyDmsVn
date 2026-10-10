using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public abstract class DesktopViewModelBase : ObservableObject, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _fieldErrors =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private readonly IDesktopNotificationService _notifications;
        private CancellationTokenSource? _activeOperation;
        private string? _errorMessage;
        private bool _isBusy;
        private bool _isEmpty;

        protected DesktopViewModelBase(IDesktopNotificationService notifications)
        {
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            protected set => SetProperty(ref _isEmpty, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasErrors => _fieldErrors.Count > 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return _fieldErrors.Values.SelectMany(messages => messages).ToArray();
            }

            var field = propertyName!;
            return _fieldErrors.TryGetValue(field, out var errors)
                ? errors
                : Array.Empty<string>();
        }

        public void CancelCurrentOperation()
        {
            CancelBusyOperation();
        }

        protected void CancelBusyOperation() => _activeOperation?.Cancel();

        protected async Task ExecuteBusyAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken,
            Func<bool>? publishCancellation = null)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (IsBusy)
            {
                throw new InvalidOperationException("Another operation is already running.");
            }

            using (var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                _activeOperation = linkedCancellation;
                IsBusy = true;
                ErrorMessage = null;
                ClearErrors();
                try
                {
                    await operation(linkedCancellation.Token);
                }
                catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                {
                    if (publishCancellation == null || publishCancellation())
                    {
                        _notifications.Publish(
                            new DesktopNotification(
                                DesktopNotificationKind.Information,
                                "Operation canceled."));
                    }
                }
                finally
                {
                    if (ReferenceEquals(_activeOperation, linkedCancellation))
                    {
                        _activeOperation = null;
                    }

                    if (publishCancellation == null || publishCancellation())
                    {
                        IsBusy = false;
                    }
                }
            }
        }

        protected void ApplyApiError(ApiError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            ClearErrors();
            foreach (var group in error.Details
                .Where(detail => !string.IsNullOrWhiteSpace(detail.Field))
                .GroupBy(detail => detail.Field!, StringComparer.Ordinal))
            {
                _fieldErrors[group.Key] = group.Select(detail => detail.Message).ToArray();
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(group.Key));
            }

            OnPropertyChanged(nameof(HasErrors));
            ErrorMessage = error.Message;
            _notifications.Publish(
                new DesktopNotification(DesktopNotificationKind.Error, error.Message));
        }

        protected void PublishNotification(DesktopNotificationKind kind, string message)
        {
            _notifications.Publish(new DesktopNotification(kind, message));
        }

        private void ClearErrors()
        {
            if (_fieldErrors.Count == 0)
            {
                return;
            }

            var fields = _fieldErrors.Keys.ToArray();
            _fieldErrors.Clear();
            foreach (var field in fields)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }
}
