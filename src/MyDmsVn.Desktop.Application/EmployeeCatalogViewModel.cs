using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class EmployeeCatalogViewModel : CatalogViewModel<EmployeeDto>
    {
        private readonly IEmployeeApiClient _client;
        private string _code = string.Empty;
        private string _name = string.Empty;
        private string? _phone;
        private int? _userId;
        private string _userIdText = string.Empty;
        private bool _userIdInputInvalid;

        public EmployeeCatalogViewModel(IEmployeeApiClient client, IMessenger messenger, IUiDispatcher dispatcher, IDesktopNotificationService notifications, IAsyncDelay delay, TimeSpan? debounceInterval = null)
            : base(CatalogKind.Employee, messenger, dispatcher, notifications, delay, debounceInterval) => _client = client ?? throw new ArgumentNullException(nameof(client));
        public string Code { get => _code; set => SetProperty(ref _code, value ?? string.Empty); }
        public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
        public string? Phone { get => _phone; set => SetProperty(ref _phone, value); }
        public int? UserId { get => _userId; private set => SetProperty(ref _userId, value); }
        public string UserIdText
        {
            get => _userIdText;
            set
            {
                var text = value ?? string.Empty;
                if (!SetProperty(ref _userIdText, text))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    _userIdInputInvalid = false;
                    UserId = null;
                }
                else if (int.TryParse(text, out var parsed) && parsed > 0)
                {
                    _userIdInputInvalid = false;
                    UserId = parsed;
                }
                else
                {
                    _userIdInputInvalid = true;
                }
            }
        }
        protected override Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken) => _client.ListAsync(request, cancellationToken);
        protected override Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken cancellationToken) => _client.GetAsync(id, cancellationToken);
        protected override Task<ApiResponse<EmployeeDto>> CreateAsync(CancellationToken cancellationToken) => _userIdInputInvalid ? InvalidUserId() : _client.CreateAsync(new SaveEmployeeRequest(Code, Name, Phone, UserId), cancellationToken);
        protected override Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, CancellationToken cancellationToken) => _userIdInputInvalid ? InvalidUserId() : _client.UpdateAsync(id, new SaveEmployeeRequest(Code, Name, Phone, UserId), cancellationToken);
        protected override Task<ApiResponse<UnitResponse>> SetActiveCoreAsync(int id, bool isActive, CancellationToken cancellationToken) => _client.SetActiveAsync(id, isActive, cancellationToken);
        protected override int GetId(EmployeeDto item) => item.Id;
        protected override void PopulateEditor(EmployeeDto item) { Code = item.Code; Name = item.Name; Phone = item.Phone; UserIdText = item.UserId?.ToString() ?? string.Empty; }
        protected override void ClearEditor() { Code = string.Empty; Name = string.Empty; Phone = null; UserIdText = string.Empty; }

        private static Task<ApiResponse<EmployeeDto>> InvalidUserId() =>
            Task.FromResult(
                ApiResponse<EmployeeDto>.Failure(
                    new ApiError(
                        ApiStatusCode.BadRequest,
                        "ValidationError",
                        "Correct the fields.",
                        new[]
                        {
                            new ApiErrorDetail(
                                "Validation.Integer",
                                "User ID must be a positive whole number.",
                                "userId"),
                        })));
    }
}
