using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GoodsReceiptEditorViewModelTests
    {
        [Fact]
        public async Task Create_sends_current_lines_then_refreshes_the_persisted_aggregate()
        {
            var api = new EditorApiClient();
            using (var fixture = Create(api))
            {
                var editor = fixture.Editor;
                editor.Load(null);
                editor.ReceiptDate = new DateTime(2026, 10, 10);
                editor.SetWarehouse(3, "W3 - Main", true);
                editor.SetEmployee(5, "E5 - Operator", true);
                editor.Note = "draft";
                var line = editor.AddLine();
                line.SetProduct(7, "P7 - Historical", false);
                line.Quantity = 2.5m;
                line.UnitCost = 4m;

                var saved = await editor.SaveAsync(CancellationToken.None);

                Assert.True(saved);
                Assert.Equal(10m, editor.Total);
                Assert.NotNull(api.LastCreate);
                Assert.Equal(7, api.LastCreate!.Lines[0].ProductId);
                Assert.Equal(41, api.GetIds.Single());
                Assert.Equal("server refreshed", editor.Note);
                Assert.Equal("P7 - Historical", editor.Lines[0].ProductDisplayName);
                Assert.False(editor.Lines[0].ProductIsAvailable);
            }
        }

        [Fact]
        public async Task Update_reuses_opaque_version_and_conflict_preserves_user_input()
        {
            var api = new EditorApiClient
            {
                UpdateResponse = ApiResponse<GoodsReceiptDto>.Failure(
                    new ApiError(
                        ApiStatusCode.Conflict,
                        "GoodsReceipt.ConcurrencyConflict",
                        "Changed elsewhere.")),
            };
            using (var fixture = Create(api))
            {
                fixture.Editor.Load(Receipt("original", "AQIDBAUGBwg="));
                fixture.Editor.Note = "my unsaved edit";

                var saved = await fixture.Editor.SaveAsync(CancellationToken.None);

                Assert.False(saved);
                Assert.Equal("AQIDBAUGBwg=", api.LastUpdate!.ExpectedVersion);
                Assert.Equal("my unsaved edit", fixture.Editor.Note);
                Assert.Equal("Changed elsewhere.", fixture.Editor.ErrorMessage);
            }
        }

        [Fact]
        public async Task Concurrent_save_requests_issue_only_one_create_command()
        {
            var api = new EditorApiClient { ControlCreateResponse = true };
            using (var fixture = Create(api))
            {
                PopulateValidDraft(fixture.Editor);

                var first = fixture.Editor.SaveAsync(CancellationToken.None);
                var second = fixture.Editor.SaveAsync(CancellationToken.None);
                api.CompleteCreate();

                Assert.True(await first);
                Assert.False(await second);
                Assert.Equal(1, api.CreateCalls);
            }
        }

        [Fact]
        public async Task Successful_create_keeps_identity_when_follow_up_refresh_fails()
        {
            var api = new EditorApiClient
            {
                GetResponse = ApiResponse<GoodsReceiptDto>.Failure(
                    new ApiError(ApiStatusCode.InternalServerError, "Refresh.Failed", "Could not refresh.")),
            };
            using (var fixture = Create(api))
            {
                PopulateValidDraft(fixture.Editor);

                var saved = await fixture.Editor.SaveAsync(CancellationToken.None);

                Assert.True(saved);
                Assert.Equal(41, fixture.Editor.Id);
                Assert.Equal("AQEBAQEBAQE=", fixture.Editor.Version);
                Assert.Equal("Could not refresh.", fixture.Editor.ErrorMessage);

                api.GetResponse = ApiResponse<GoodsReceiptDto>.Success(
                    Receipt("server refreshed", "AwMDAwMDAwM="));
                Assert.True(await fixture.Editor.SaveAsync(CancellationToken.None));
                Assert.Equal(1, api.CreateCalls);
                Assert.NotNull(api.LastUpdate);
                Assert.Equal("AQEBAQEBAQE=", api.LastUpdate!.ExpectedVersion);
            }
        }

        [Fact]
        public async Task Immediate_validation_blocks_missing_fields_and_posted_receipts_are_read_only()
        {
            var api = new EditorApiClient();
            using (var fixture = Create(api))
            {
                fixture.Editor.Load(null);
                var saved = await fixture.Editor.SaveAsync(CancellationToken.None);
                Assert.False(saved);
                Assert.NotEmpty(((IEnumerable)fixture.Editor.GetErrors("warehouseId")).Cast<string>());
                Assert.Null(api.LastCreate);

                fixture.Editor.Load(Receipt("posted", "AQIDBAUGBwg=", "Posted"));
                Assert.True(fixture.Editor.IsReadOnly);
                Assert.False(await fixture.Editor.SaveAsync(CancellationToken.None));
                Assert.Null(api.LastUpdate);
            }
        }

        [Fact]
        public async Task Dirty_state_tracks_user_edits_and_resets_after_load_or_save()
        {
            var api = new EditorApiClient();
            using (var fixture = Create(api))
            {
                fixture.Editor.Load(Receipt("original", "AQIDBAUGBwg="));
                Assert.False(fixture.Editor.IsDirty);

                fixture.Editor.Note = "changed";
                Assert.True(fixture.Editor.IsDirty);

                fixture.Editor.Load(Receipt("reloaded", "AQIDBAUGBwg="));
                Assert.False(fixture.Editor.IsDirty);

                fixture.Editor.Lines[0].Quantity = 9m;
                Assert.True(fixture.Editor.IsDirty);
                Assert.True(await fixture.Editor.SaveAsync(CancellationToken.None));
                Assert.False(fixture.Editor.IsDirty);
            }
        }

        [Fact]
        public void Lookup_metadata_changes_do_not_dirty_a_posted_receipt()
        {
            using (var fixture = Create(new EditorApiClient()))
            {
                fixture.Editor.Load(Receipt("posted", "AQIDBAUGBwg=", "Posted"));

                fixture.Editor.Lines[0].SetProduct(7, "P7 - Renamed product", false);

                Assert.False(fixture.Editor.IsDirty);
            }
        }

        private static Fixture Create(EditorApiClient api)
        {
            var messenger = new WeakReferenceMessenger();
            var notifications = new DesktopNotificationCenter();
            var delay = new ImmediateDelay();
            var warehouse = new CatalogLookupViewModel(
                new LookupSource(CatalogKind.Warehouse), messenger, new ImmediateUiDispatcher(), notifications, delay);
            var employee = new CatalogLookupViewModel(
                new LookupSource(CatalogKind.Employee), messenger, new ImmediateUiDispatcher(), notifications, delay);
            var products = new GoodsReceiptProductOptionsViewModel(
                new LookupSource(CatalogKind.Product), messenger, new ImmediateUiDispatcher(), notifications, delay);
            return new Fixture(
                new GoodsReceiptEditorViewModel(api, warehouse, employee, products, notifications),
                warehouse,
                employee,
                products);
        }

        private static void PopulateValidDraft(GoodsReceiptEditorViewModel editor)
        {
            editor.Load(null);
            editor.ReceiptDate = new DateTime(2026, 10, 10);
            editor.SetWarehouse(3, "W3 - Main", true);
            editor.SetEmployee(5, "E5 - Operator", true);
            var line = editor.AddLine();
            line.SetProduct(7, "P7 - Product 7", true);
            line.Quantity = 1m;
            line.UnitCost = 2m;
        }

        private static GoodsReceiptDto Receipt(
            string note,
            string version,
            string status = "Draft") =>
            new GoodsReceiptDto(
                41,
                "GR0000000041",
                new DateTime(2026, 10, 10),
                3,
                5,
                status,
                note,
                status == "Posted" ? new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc) : (DateTime?)null,
                version,
                new[] { new GoodsReceiptLineDto(91, 1, 7, 2.5m, 4m, "P7 - Historical", false) },
                "W3 - Main",
                true,
                "E5 - Operator",
                true);

        private sealed class Fixture : IDisposable
        {
            private readonly IDisposable[] _owned;
            public Fixture(GoodsReceiptEditorViewModel editor, params IDisposable[] owned)
            {
                Editor = editor;
                _owned = owned;
            }
            public GoodsReceiptEditorViewModel Editor { get; }
            public void Dispose()
            {
                Editor.Dispose();
                foreach (var item in _owned) item.Dispose();
            }
        }

        private sealed class ImmediateDelay : IAsyncDelay
        {
            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class LookupSource : ICatalogLookupSource
        {
            public LookupSource(CatalogKind kind) => Kind = kind;
            public CatalogKind Kind { get; }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
                Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Array.Empty<CatalogLookupDto>()));
            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken token) =>
                Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Item", true)));
        }

        private sealed class EditorApiClient : IGoodsReceiptApiClient
        {
            private TaskCompletionSource<ApiResponse<GoodsReceiptDto>>? _createCompletion;

            public SaveGoodsReceiptRequest? LastCreate { get; private set; }
            public UpdateGoodsReceiptDraftRequest? LastUpdate { get; private set; }
            public IList<long> GetIds { get; } = new List<long>();
            public int CreateCalls { get; private set; }
            public bool ControlCreateResponse { get; set; }
            public ApiResponse<GoodsReceiptDto> GetResponse { get; set; } =
                ApiResponse<GoodsReceiptDto>.Success(Receipt("server refreshed", "AwMDAwMDAwM="));
            public ApiResponse<GoodsReceiptDto> UpdateResponse { get; set; } = ApiResponse<GoodsReceiptDto>.Success(Receipt("updated", "AgICAgICAgI="));

            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token)
            {
                CreateCalls++;
                LastCreate = request;
                var response = ApiResponse<GoodsReceiptDto>.Success(Receipt("created", "AQEBAQEBAQE="));
                if (!ControlCreateResponse)
                {
                    return Task.FromResult(response);
                }

                if (_createCompletion == null)
                {
                    _createCompletion = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
                return _createCompletion.Task;
            }

            public void CompleteCreate() =>
                _createCompletion!.TrySetResult(
                    ApiResponse<GoodsReceiptDto>.Success(Receipt("created", "AQEBAQEBAQE=")));
            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token)
            {
                LastUpdate = request;
                return Task.FromResult(UpdateResponse);
            }
            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
            {
                GetIds.Add(id);
                return Task.FromResult(GetResponse);
            }
            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
