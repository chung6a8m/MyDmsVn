using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class WarehouseCatalogViewModel : CatalogViewModel<WarehouseDto>
    {
        private readonly IWarehouseApiClient _client;
        private string _code = string.Empty;
        private string _name = string.Empty;
        private string? _address;

        public WarehouseCatalogViewModel(IWarehouseApiClient client, IMessenger messenger, IUiDispatcher dispatcher, IDesktopNotificationService notifications, IAsyncDelay delay, TimeSpan? debounceInterval = null)
            : base(CatalogKind.Warehouse, messenger, dispatcher, notifications, delay, debounceInterval) => _client = client ?? throw new ArgumentNullException(nameof(client));
        public string Code { get => _code; set => SetProperty(ref _code, value ?? string.Empty); }
        public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
        public string? Address { get => _address; set => SetProperty(ref _address, value); }
        protected override Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken) => _client.ListAsync(request, cancellationToken);
        protected override Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken cancellationToken) => _client.GetAsync(id, cancellationToken);
        protected override Task<ApiResponse<WarehouseDto>> CreateAsync(CancellationToken cancellationToken) => _client.CreateAsync(new SaveWarehouseRequest(Code, Name, Address), cancellationToken);
        protected override Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, CancellationToken cancellationToken) => _client.UpdateAsync(id, new SaveWarehouseRequest(Code, Name, Address), cancellationToken);
        protected override Task<ApiResponse<UnitResponse>> SetActiveCoreAsync(int id, bool isActive, CancellationToken cancellationToken) => _client.SetActiveAsync(id, isActive, cancellationToken);
        protected override int GetId(WarehouseDto item) => item.Id;
        protected override void PopulateEditor(WarehouseDto item) { Code = item.Code; Name = item.Name; Address = item.Address; }
        protected override void ClearEditor() { Code = string.Empty; Name = string.Empty; Address = null; }
    }
}
