using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class CustomerCatalogViewModel : CatalogViewModel<CustomerDto>
    {
        private readonly ICustomerApiClient _client;
        private string _code = string.Empty;
        private string _name = string.Empty;
        private string? _address;
        private string? _phone;
        private string? _taxCode;

        public CustomerCatalogViewModel(ICustomerApiClient client, IMessenger messenger, IUiDispatcher dispatcher, IDesktopNotificationService notifications, IAsyncDelay delay, TimeSpan? debounceInterval = null)
            : base(CatalogKind.Customer, messenger, dispatcher, notifications, delay, debounceInterval) => _client = client ?? throw new ArgumentNullException(nameof(client));
        public string Code { get => _code; set => SetProperty(ref _code, value ?? string.Empty); }
        public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
        public string? Address { get => _address; set => SetProperty(ref _address, value); }
        public string? Phone { get => _phone; set => SetProperty(ref _phone, value); }
        public string? TaxCode { get => _taxCode; set => SetProperty(ref _taxCode, value); }
        protected override Task<ApiResponse<PagedResult<CustomerDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken) => _client.ListAsync(request, cancellationToken);
        protected override Task<ApiResponse<CustomerDto>> GetAsync(int id, CancellationToken cancellationToken) => _client.GetAsync(id, cancellationToken);
        protected override Task<ApiResponse<CustomerDto>> CreateAsync(CancellationToken cancellationToken) => _client.CreateAsync(new SaveCustomerRequest(Code, Name, Address, Phone, TaxCode), cancellationToken);
        protected override Task<ApiResponse<CustomerDto>> UpdateAsync(int id, CancellationToken cancellationToken) => _client.UpdateAsync(id, new SaveCustomerRequest(Code, Name, Address, Phone, TaxCode), cancellationToken);
        protected override Task<ApiResponse<UnitResponse>> SetActiveCoreAsync(int id, bool isActive, CancellationToken cancellationToken) => _client.SetActiveAsync(id, isActive, cancellationToken);
        protected override int GetId(CustomerDto item) => item.Id;
        protected override void PopulateEditor(CustomerDto item) { Code = item.Code; Name = item.Name; Address = item.Address; Phone = item.Phone; TaxCode = item.TaxCode; }
        protected override void ClearEditor() { Code = string.Empty; Name = string.Empty; Address = null; Phone = null; TaxCode = null; }
    }
}
