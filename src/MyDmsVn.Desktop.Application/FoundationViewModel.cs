using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class FoundationViewModel : DesktopViewModelBase
    {
        private readonly IFoundationApiClient _apiClient;
        private bool _isReady;
        private string? _runtime;

        public FoundationViewModel(IFoundationApiClient apiClient)
            : this(apiClient, new DesktopNotificationCenter())
        {
        }

        public FoundationViewModel(
            IFoundationApiClient apiClient,
            IDesktopNotificationService notifications)
            : base(notifications)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            InitializeCommand = new AsyncRelayCommand(InitializeFromCommandAsync);
            CancelCommand = new RelayCommand(CancelCurrentOperation);
        }

        public bool IsReady
        {
            get => _isReady;
            private set => SetProperty(ref _isReady, value);
        }

        public string? Runtime
        {
            get => _runtime;
            private set => SetProperty(ref _runtime, value);
        }

        public IAsyncRelayCommand InitializeCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return ExecuteBusyAsync(
                async operationCancellation =>
                {
                    var response = await _apiClient.GetStatusAsync(
                        new FoundationStatusRequest("Desktop"),
                        operationCancellation);
                    if (response.IsSuccess)
                    {
                        IsReady = response.Data!.IsReady;
                        Runtime = response.Data.Runtime;
                        IsEmpty = !IsReady;
                        return;
                    }

                    IsReady = false;
                    Runtime = null;
                    IsEmpty = true;
                    ApplyApiError(response.Error!);
                },
                cancellationToken);
        }

        private Task InitializeFromCommandAsync(CancellationToken cancellationToken)
        {
            return InitializeAsync(cancellationToken);
        }
    }
}
