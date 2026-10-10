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
            public SaveGoodsReceiptRequest? LastCreate { get; private set; }
            public UpdateGoodsReceiptDraftRequest? LastUpdate { get; private set; }
            public IList<long> GetIds { get; } = new List<long>();
            public ApiResponse<GoodsReceiptDto> UpdateResponse { get; set; } = ApiResponse<GoodsReceiptDto>.Success(Receipt("updated", "AgICAgICAgI="));

            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token)
            {
                LastCreate = request;
                return Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt("created", "AQEBAQEBAQE=")));
            }
            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token)
            {
                LastUpdate = request;
                return Task.FromResult(UpdateResponse);
            }
            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
            {
                GetIds.Add(id);
                return Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt("server refreshed", "AwMDAwMDAwM=")));
            }
            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
