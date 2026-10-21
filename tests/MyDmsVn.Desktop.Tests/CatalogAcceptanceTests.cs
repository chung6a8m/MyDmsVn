using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class CatalogAcceptanceTests
    {
        [Theory]
        [InlineData(CatalogKind.Product)]
        [InlineData(CatalogKind.Warehouse)]
        [InlineData(CatalogKind.Employee)]
        [InlineData(CatalogKind.Customer)]
        public void Catalog_buttons_create_update_deactivate_and_reactivate_all_entity_fields(CatalogKind kind)
        {
            Run(() =>
            {
                using (var scenario = new Scenario(kind))
                {
                    var control = scenario.Control;
                    control.NewButton.PerformClick();
                    Fill(control, "NEW", "Mặt hàng mới");
                    control.SaveButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal(1, scenario.State.Creates);
                    Assert.Equal("NEW", CellValue(control, 2, "Code"));
                    AssertEditorMatchesRequest(control, scenario.State.LastSave!);

                    Select(control, 2);
                    AssertEditorMatchesRequest(control, scenario.State.LastSave!);
                    Fill(control, "EDIT", "Đã cập nhật");
                    control.SaveButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal(1, scenario.State.Updates);
                    Assert.Equal(2, scenario.State.LastId);
                    Assert.Equal("EDIT", CellValue(control, 2, "Code"));
                    Assert.Equal("Đã cập nhật", CellValue(control, 2, "Name"));
                    AssertEditorMatchesRequest(control, scenario.State.LastSave!);

                    control.DeactivateButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal("Inactive", CellValue(control, 2, "Status"));
                    Assert.False(scenario.State.ActiveChanges[0]);
                    control.ActivateButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal("Active", CellValue(control, 2, "Status"));
                    Assert.True(scenario.State.ActiveChanges[1]);

                    control.NewButton.PerformClick();
                    Assert.All(control.EditorControls.Values, editor => Assert.Equal(string.Empty, editor.Text));
                }
            });
        }

        [Theory]
        [InlineData(CatalogKind.Product, "unit")]
        [InlineData(CatalogKind.Warehouse, "address")]
        [InlineData(CatalogKind.Employee, "phone")]
        [InlineData(CatalogKind.Customer, "taxCode")]
        public void Entity_field_validation_is_inline_preserves_input_and_clears_for_new_editor(CatalogKind kind, string field)
        {
            Run(() =>
            {
                using (var scenario = new Scenario(kind))
                {
                    Fill(scenario.Control, "DUP", "Giữ nội dung");
                    scenario.State.Error = new ApiError(ApiStatusCode.BadRequest, "Validation", "Correct the fields.",
                        new[] { new ApiErrorDetail("Validation.Field", "Field is invalid.", field) });
                    scenario.Control.SaveButton.PerformClick();
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal("Field is invalid.", scenario.Control.GetFieldError(field));
                    Assert.Equal("Giữ nội dung", scenario.Control.EditorControls["name"].Text);
                    Assert.Equal(2, scenario.Control.Grid.RowsCount);
                    Assert.True(scenario.Control.SaveButton.Enabled);
                    scenario.Control.NewButton.PerformClick();
                    Assert.Equal(string.Empty, scenario.Control.GetFieldError(field));
                }
            });
        }

        [Theory]
        [InlineData(CatalogKind.Product, ApiStatusCode.Forbidden)]
        [InlineData(CatalogKind.Warehouse, ApiStatusCode.Forbidden)]
        [InlineData(CatalogKind.Employee, ApiStatusCode.Forbidden)]
        [InlineData(CatalogKind.Customer, ApiStatusCode.Forbidden)]
        [InlineData(CatalogKind.Product, ApiStatusCode.Conflict)]
        [InlineData(CatalogKind.Warehouse, ApiStatusCode.Conflict)]
        [InlineData(CatalogKind.Employee, ApiStatusCode.Conflict)]
        [InlineData(CatalogKind.Customer, ApiStatusCode.Conflict)]
        [InlineData(CatalogKind.Product, ApiStatusCode.InternalServerError)]
        [InlineData(CatalogKind.Warehouse, ApiStatusCode.InternalServerError)]
        [InlineData(CatalogKind.Employee, ApiStatusCode.InternalServerError)]
        [InlineData(CatalogKind.Customer, ApiStatusCode.InternalServerError)]
        public void Rejected_mutations_keep_visible_rows_and_report_non_modal_error(CatalogKind kind, ApiStatusCode status)
        {
            Run(() =>
            {
                using (var scenario = new Scenario(kind))
                {
                    Select(scenario.Control, 1);
                    scenario.State.Error = new ApiError(status, "Rejected", "Operation rejected.");
                    scenario.Control.EditorControls["name"].Text = "Unsaved edit";
                    scenario.Control.SaveButton.PerformClick();
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal("Original", CellValue(scenario.Control, 1, "Name"));
                    Assert.Equal("Unsaved edit", scenario.Control.EditorControls["name"].Text);
                    Assert.Equal("Operation rejected.", scenario.Notifications.LastNotification!.Message);
                    scenario.Control.DeactivateButton.PerformClick();
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal("Active", CellValue(scenario.Control, 1, "Status"));
                    Assert.True(scenario.Control.SaveButton.Enabled);
                    Assert.Single(scenario.State.Requests);
                }
            });
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2147483648")]
        public void Employee_invalid_user_id_blocks_API_then_blank_explicitly_unlinks(string invalid)
        {
            Run(() =>
            {
                using (var scenario = new Scenario(CatalogKind.Employee))
                {
                    Select(scenario.Control, 1);
                    Assert.Equal("42", scenario.Control.EditorControls["userId"].Text);
                    scenario.Control.EditorControls["userId"].Text = invalid;
                    scenario.Control.SaveButton.PerformClick();
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal(0, scenario.State.Updates);
                    Assert.Contains("positive whole number", scenario.Control.GetFieldError("userId"));
                    scenario.Control.EditorControls["userId"].Text = string.Empty;
                    scenario.Control.SaveButton.PerformClick();
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal(1, scenario.State.Updates);
                    Assert.Null(((SaveEmployeeRequest)scenario.State.LastSave!).UserId);
                    Assert.Equal(string.Empty, scenario.Control.GetFieldError("userId"));
                }
            });
        }

        [Theory]
        [InlineData(CatalogKind.Product)]
        [InlineData(CatalogKind.Warehouse)]
        [InlineData(CatalogKind.Employee)]
        [InlineData(CatalogKind.Customer)]
        public void Paging_and_immediate_refresh_keep_page_but_changed_search_resets_it(CatalogKind kind)
        {
            Run(() =>
            {
                using (var scenario = new Scenario(kind, 30))
                {
                    var control = scenario.Control;
                    Assert.Equal("Page 1 of 2", control.PageLabel.Text);
                    Assert.False(control.PreviousPageButton.Enabled);
                    control.NextPageButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal("Page 2 of 2", control.PageLabel.Text);
                    Assert.Equal(6, control.Grid.RowsCount);
                    Assert.False(control.NextPageButton.Enabled);
                    control.RefreshButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal(2, scenario.State.Requests.Last().PageNumber);
                    Assert.Equal(3, scenario.State.Requests.Count);
                    Assert.Empty(scenario.Delay.Pending);
                    control.SearchBox.Text = "CODE1";
                    Assert.Equal(3, scenario.State.Requests.Count);
                    Assert.Equal(TimeSpan.FromMilliseconds(300), scenario.Delay.Interval);
                    control.RefreshButton.PerformClick();
                    Drain(control.LastOperation);
                    Assert.Equal(4, scenario.State.Requests.Count);
                    Assert.Equal(1, scenario.State.Requests.Last().PageNumber);
                    Assert.Equal("CODE1", scenario.State.Requests.Last().Search);
                    Assert.True(scenario.Delay.Pending[0].Task.IsCanceled);
                    Assert.Equal("Page 1 of 1", control.PageLabel.Text);
                }
            });
        }

        [Fact]
        public void Rapid_control_search_queries_only_latest_text_after_controlled_debounce()
        {
            Run(() =>
            {
                using (var scenario = new Scenario(CatalogKind.Product))
                {
                    scenario.Control.SearchBox.Text = "C";
                    var first = scenario.Control.LastOperation;
                    scenario.Control.SearchBox.Text = "CO";
                    var second = scenario.Control.LastOperation;
                    scenario.Control.SearchBox.Text = "CODE1";
                    Assert.Single(scenario.State.Requests);
                    Assert.True(scenario.Delay.Pending[0].Task.IsCanceled);
                    Assert.True(scenario.Delay.Pending[1].Task.IsCanceled);
                    scenario.Delay.ReleaseLatest();
                    Drain(scenario.Control.LastOperation);
                    Drain(first);
                    Drain(second);
                    Assert.Equal(2, scenario.State.Requests.Count);
                    Assert.Equal("CODE1", scenario.State.Requests.Last().Search);
                    Assert.Equal("CODE1", CellValue(scenario.Control, 1, "Code"));
                }
            });
        }

        [Fact]
        public void Search_cancels_inflight_list_and_late_old_response_cannot_replace_grid()
        {
            Run(() =>
            {
                using (var scenario = new Scenario(CatalogKind.Product))
                {
                    scenario.State.ControlLists = true;
                    scenario.Control.RefreshButton.PerformClick();
                    var old = scenario.Control.LastOperation;
                    var oldResponse = scenario.State.PendingLists[0];
                    scenario.Control.SearchBox.Text = "fresh";
                    Assert.True(oldResponse.Token.IsCancellationRequested);
                    oldResponse.Complete("STALE");
                    Drain(old);
                    Assert.Equal("CODE1", CellValue(scenario.Control, 1, "Code"));
                    scenario.Delay.ReleaseLatest();
                    Pump(() => scenario.State.PendingLists.Count == 2);
                    scenario.State.PendingLists[1].Complete("FRESH");
                    Drain(scenario.Control.LastOperation);
                    Assert.Equal("FRESH", CellValue(scenario.Control, 1, "Code"));
                }
            });
        }

        [Fact]
        public void Disposal_cancels_pending_control_search_and_discards_late_list_completion()
        {
            Run(() =>
            {
                using (var scenario = new Scenario(CatalogKind.Product))
                {
                    scenario.State.ControlLists = true;
                    scenario.Control.RefreshButton.PerformClick();
                    var pending = scenario.Control.LastOperation;
                    var response = scenario.State.PendingLists[0];
                    scenario.Control.SearchBox.Text = "late";
                    var search = scenario.Control.LastOperation;
                    scenario.Control.Dispose();
                    Assert.True(response.Token.IsCancellationRequested);
                    Assert.True(scenario.Delay.Pending[0].Task.IsCanceled);
                    response.Complete("MUST NOT APPLY");
                    Drain(pending);
                    Drain(search);
                    Assert.Equal(2, scenario.State.Requests.Count);
                    Assert.Null(scenario.Notifications.LastNotification);
                    Assert.True(scenario.Control.IsDisposed);
                }
            });
        }

        internal static IDisposable CreateAccessibilityScenario(CatalogKind kind, out CatalogControl control)
        {
            var scenario = new Scenario(kind);
            control = scenario.Control;
            return scenario;
        }

        private static object CellValue(CatalogControl control, int row, string heading)
        {
            // Products intentionally omits the ID column; assert the public field by
            // its unique heading without changing the expected row or business value.
            var column = Assert.Single(Enumerable.Range(0, control.Grid.ColumnsCount),
                index => Equals(control.Grid[0, index].Value, heading));
            return control.Grid[row, column].Value;
        }
        private static void Run(Action action) => StaTest.Run(token =>
        {
            using (var guard = new WinFormsTestGuard()) action();
        }, TimeSpan.FromSeconds(15));

        private static void Pump(Func<bool> condition)
        {
            var clock = Stopwatch.StartNew();
            while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(3))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Yield();
            }
            Assert.True(condition(), "Expected catalog UI operation did not finish within three seconds.");
        }

        private static void Drain(Task operation)
        {
            Pump(() => operation.IsCompleted);
            operation.GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();
        }

        private static void Select(CatalogControl control, int row)
        {
            Assert.True(control.Grid.Selection.Focus(new SourceGrid.Position(row, 0), true));
            typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(control.Grid, new object[] { new KeyEventArgs(Keys.Enter) });
            Drain(control.LastOperation);
        }

        private static void Fill(CatalogControl control, string code, string name)
        {
            foreach (var field in control.EditorControls)
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Value.AccessibleName));
                field.Value.Text = field.Key == "userId" ? "84" : code + " " + field.Key;
            }
            control.EditorControls["code"].Text = code;
            control.EditorControls["name"].Text = name;
        }

        private static void AssertEditorMatchesRequest(CatalogControl control, object request)
        {
            foreach (var field in control.EditorControls)
            {
                var property = request.GetType().GetProperty(field.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(property);
                Assert.Equal(property!.GetValue(request)?.ToString() ?? string.Empty, field.Value.Text);
            }
        }

        private sealed class Scenario : IDisposable
        {
            private readonly Form _host = new Form { Width = 1000, Height = 700 };
            public readonly State State = new State();
            public readonly ControlledDelay Delay = new ControlledDelay();
            public readonly DesktopNotificationCenter Notifications = new DesktopNotificationCenter();
            public CatalogControl Control { get; }
            public Scenario(CatalogKind kind, int count = 1)
            {
                State.SeedCount = count;
                var messenger = new WeakReferenceMessenger();
                var dispatcher = new ImmediateUiDispatcher();
                switch (kind)
                {
                    case CatalogKind.Product: Control = new CatalogControl(new ProductCatalogViewModel(new ProductClient(State), messenger, dispatcher, Notifications, Delay)); break;
                    case CatalogKind.Warehouse: Control = new CatalogControl(new WarehouseCatalogViewModel(new WarehouseClient(State), messenger, dispatcher, Notifications, Delay)); break;
                    case CatalogKind.Employee: Control = new CatalogControl(new EmployeeCatalogViewModel(new EmployeeClient(State), messenger, dispatcher, Notifications, Delay)); break;
                    case CatalogKind.Customer: Control = new CatalogControl(new CustomerCatalogViewModel(new CustomerClient(State), messenger, dispatcher, Notifications, Delay)); break;
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
                _host.Controls.Add(Control);
                _host.Show();
                System.Windows.Forms.Application.DoEvents();
                Drain(Control.ActivateAsync(CancellationToken.None));
            }
            public void Dispose()
            {
                Control.Dispose();
                foreach (var pending in State.PendingLists) pending.Complete("Cleanup");
                foreach (var pending in State.PendingLists) Drain(pending.Task);
                Drain(Control.LastOperation);
                _host.Dispose();
            }
        }

        private sealed class ControlledDelay : IAsyncDelay
        {
            public readonly List<TaskCompletionSource<bool>> Pending = new List<TaskCompletionSource<bool>>();
            public TimeSpan Interval;
            public Task DelayAsync(TimeSpan interval, CancellationToken token)
            {
                Interval = interval;
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => completion.TrySetCanceled());
                Pending.Add(completion);
                return completion.Task;
            }
            public void ReleaseLatest() => Pending.Last().TrySetResult(true);
        }

        private sealed class PendingList
        {
            public CancellationToken Token;
            public Task Task = System.Threading.Tasks.Task.CompletedTask;
            public Action<string> Complete = null!;
        }

        private sealed class State
        {
            public int SeedCount;
            public int Creates;
            public int Updates;
            public int LastId;
            public object? LastSave;
            public ApiError? Error;
            public bool ControlLists;
            public readonly List<bool> ActiveChanges = new List<bool>();
            public readonly List<CatalogListRequest> Requests = new List<CatalogListRequest>();
            public readonly List<PendingList> PendingLists = new List<PendingList>();
        }

        private abstract class Client<TDto, TRequest> where TRequest : class
        {
            protected readonly State State;
            private readonly SortedDictionary<int, TDto> _items = new SortedDictionary<int, TDto>();
            private readonly Func<int, string, string, bool, TRequest?, TDto> _make;
            protected Client(State state, Func<int, string, string, bool, TRequest?, TDto> make)
            {
                State = state;
                _make = make;
                for (var id = 1; id <= state.SeedCount; id++) _items[id] = make(id, "CODE" + id, "Original", true, default);
            }
            public Task<ApiResponse<PagedResult<TDto>>> ListAsync(CatalogListRequest request, CancellationToken token)
            {
                State.Requests.Add(request);
                if (State.ControlLists)
                {
                    var completion = new TaskCompletionSource<ApiResponse<PagedResult<TDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    State.PendingLists.Add(new PendingList { Token = token, Task = completion.Task, Complete = code => completion.TrySetResult(ApiResponse<PagedResult<TDto>>.Success(
                        new PagedResult<TDto>(new[] { _make(1, code, code, true, default) }, request.PageNumber, request.PageSize, 1))) });
                    return completion.Task;
                }
                var filtered = _items.Values.Where(item => string.IsNullOrEmpty(request.Search) ||
                    ((string)typeof(TDto).GetProperty("Code")!.GetValue(item)!).Contains(request.Search!)).ToArray();
                return Task.FromResult(ApiResponse<PagedResult<TDto>>.Success(new PagedResult<TDto>(
                    filtered.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToArray(), request.PageNumber, request.PageSize, filtered.Length)));
            }
            public Task<ApiResponse<TDto>> GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<TDto>.Success(_items[id]));
            public Task<ApiResponse<TDto>> CreateAsync(TRequest request, CancellationToken token)
            {
                State.Creates++;
                return Save(_items.Count + 1, request);
            }
            public Task<ApiResponse<TDto>> UpdateAsync(int id, TRequest request, CancellationToken token)
            {
                State.Updates++;
                return Save(id, request);
            }
            private Task<ApiResponse<TDto>> Save(int id, TRequest request)
            {
                State.LastId = id;
                State.LastSave = request;
                if (State.Error != null) return Task.FromResult(ApiResponse<TDto>.Failure(State.Error));
                var code = (string)typeof(TRequest).GetProperty("Code")!.GetValue(request)!;
                var name = (string)typeof(TRequest).GetProperty("Name")!.GetValue(request)!;
                _items[id] = _make(id, code, name, true, request);
                return Task.FromResult(ApiResponse<TDto>.Success(_items[id]));
            }
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token)
            {
                if (State.Error != null) return Task.FromResult(ApiResponse<UnitResponse>.Failure(State.Error));
                State.ActiveChanges.Add(active);
                var item = _items[id];
                _items[id] = _make(id, (string)typeof(TDto).GetProperty("Code")!.GetValue(item)!,
                    (string)typeof(TDto).GetProperty("Name")!.GetValue(item)!, active, (TRequest?)State.LastSave);
                return Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value));
            }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class ProductClient : Client<ProductDto, SaveProductRequest>, IProductApiClient
        {
            public ProductClient(State state) : base(state, (id, code, name, active, request) => new ProductDto(id, code, name, request?.Unit ?? "pcs", active)) { }
        }
        private sealed class WarehouseClient : Client<WarehouseDto, SaveWarehouseRequest>, IWarehouseApiClient
        {
            public WarehouseClient(State state) : base(state, (id, code, name, active, request) => new WarehouseDto(id, code, name, request?.Address ?? "Original address", active)) { }
        }
        private sealed class EmployeeClient : Client<EmployeeDto, SaveEmployeeRequest>, IEmployeeApiClient
        {
            public EmployeeClient(State state) : base(state, (id, code, name, active, request) => new EmployeeDto(id, code, name, request?.Phone ?? "0123", request == null ? 42 : request.UserId, active)) { }
        }
        private sealed class CustomerClient : Client<CustomerDto, SaveCustomerRequest>, ICustomerApiClient
        {
            public CustomerClient(State state) : base(state, (id, code, name, active, request) => new CustomerDto(id, code, name, request?.Address ?? "Original address", request?.Phone ?? "0123", request?.TaxCode ?? "TAX", active)) { }
        }
    }
}
