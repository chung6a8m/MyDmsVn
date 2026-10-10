using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class ProductCatalogViewModel : CatalogViewModel<ProductDto>
    {
        private readonly IProductApiClient _client;
        private string _code = string.Empty;
        private string _name = string.Empty;
        private string _unit = string.Empty;

        public ProductCatalogViewModel(IProductApiClient client, IMessenger messenger, IUiDispatcher dispatcher, IDesktopNotificationService notifications, IAsyncDelay delay, TimeSpan? debounceInterval = null)
            : base(CatalogKind.Product, messenger, dispatcher, notifications, delay, debounceInterval) =>
            _client = client ?? throw new ArgumentNullException(nameof(client));

        public string Code { get => _code; set => SetProperty(ref _code, value ?? string.Empty); }
        public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
        public string Unit { get => _unit; set => SetProperty(ref _unit, value ?? string.Empty); }

        protected override Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken) => _client.ListAsync(request, cancellationToken);
        protected override Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken cancellationToken) => _client.GetAsync(id, cancellationToken);
        protected override Task<ApiResponse<ProductDto>> CreateAsync(CancellationToken cancellationToken) => _client.CreateAsync(new SaveProductRequest(Code, Name, Unit), cancellationToken);
        protected override Task<ApiResponse<ProductDto>> UpdateAsync(int id, CancellationToken cancellationToken) => _client.UpdateAsync(id, new SaveProductRequest(Code, Name, Unit), cancellationToken);
        protected override Task<ApiResponse<UnitResponse>> SetActiveCoreAsync(int id, bool isActive, CancellationToken cancellationToken) => _client.SetActiveAsync(id, isActive, cancellationToken);
        protected override int GetId(ProductDto item) => item.Id;
        protected override void PopulateEditor(ProductDto item) { Code = item.Code; Name = item.Name; Unit = item.Unit; }
        protected override void ClearEditor() { Code = string.Empty; Name = string.Empty; Unit = string.Empty; }
    }
}
