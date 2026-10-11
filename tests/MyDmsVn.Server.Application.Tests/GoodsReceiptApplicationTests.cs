using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Inventory;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class GoodsReceiptApplicationTests
{
    [Fact]
    public void Goods_receipt_requests_declare_separate_read_and_write_permissions()
    {
        var save = ValidSaveRequest();

        Assert.Equal(
            PermissionKeys.InventoryGoodsReceiptsWrite,
            ((IAuthorizedRequest)new CreateGoodsReceiptDraftCommand(save)).PermissionKey);
        Assert.Equal(
            PermissionKeys.InventoryGoodsReceiptsWrite,
            ((IAuthorizedRequest)new UpdateGoodsReceiptDraftCommand(
                new UpdateGoodsReceiptDraftRequest(1, VersionToken(1), save.ReceiptDate,
                    save.WarehouseId, save.EmployeeId, save.Note, save.Lines))).PermissionKey);
        Assert.Equal(
            PermissionKeys.InventoryGoodsReceiptsRead,
            ((IAuthorizedRequest)new GetGoodsReceiptByIdQuery(1)).PermissionKey);
        Assert.Equal(
            PermissionKeys.InventoryGoodsReceiptsRead,
            ((IAuthorizedRequest)new ListGoodsReceiptsQuery(
                new GoodsReceiptListRequest(1, 25, null, null))).PermissionKey);
        Assert.Equal(
            PermissionKeys.InventoryGoodsReceiptsPost,
            ((IAuthorizedRequest)new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(1, VersionToken(1)))).PermissionKey);
    }

    [Fact]
    public async Task Invalid_aggregate_is_rejected_before_a_unit_of_work_is_opened()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);
        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(
                new SaveGoodsReceiptRequest(
                    new DateTime(2026, 10, 10),
                    0,
                    0,
                    new string('n', 1001),
                    new[]
                    {
                        new SaveGoodsReceiptLineRequest(7, 0m, -1m),
                        new SaveGoodsReceiptLineRequest(7, 1m, 0m),
                    })),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => Field(error) == "Request.WarehouseId");
        Assert.Contains(result.Errors, error => Field(error) == "Request.EmployeeId");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Note");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[0].Quantity");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[0].UnitCost");
        Assert.True(
            result.Errors.Any(error =>
                error.Code == "GoodsReceipt.DuplicateProduct" && Field(error) == "Request.Lines[1].ProductId"),
            string.Join(" | ", result.Errors.Select(error => error.Code + ":" + Field(error))));
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Fact]
    public async Task Invalid_expected_version_is_rejected_before_a_unit_of_work_is_opened()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);
        var save = ValidSaveRequest();

        var result = await provider.GetRequiredService<ISender>().Send(
            new UpdateGoodsReceiptDraftCommand(
                new UpdateGoodsReceiptDraftRequest(
                    1,
                    "not-base64",
                    save.ReceiptDate,
                    save.WarehouseId,
                    save.EmployeeId,
                    save.Note,
                    save.Lines)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error =>
            error.Code == "Validation.InvalidVersion" && Field(error) == "Request.ExpectedVersion");
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Line_values_outside_sql_decimal_shape_are_rejected_before_a_unit_of_work_is_opened(
        bool update)
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);
        var lines = new[]
        {
            new SaveGoodsReceiptLineRequest(7, 0.00001m, 1m),
            new SaveGoodsReceiptLineRequest(8, 100000000000000m, 1.23456m),
            new SaveGoodsReceiptLineRequest(9, 1m, 1000000000000000m),
        };
        var save = new SaveGoodsReceiptRequest(
            new DateTime(2026, 10, 10),
            3,
            5,
            null,
            lines);

        ErrorOr<GoodsReceiptDto> result;
        if (update)
        {
            result = await provider.GetRequiredService<ISender>().Send(
                new UpdateGoodsReceiptDraftCommand(
                    new UpdateGoodsReceiptDraftRequest(
                        41,
                        VersionToken(1),
                        save.ReceiptDate,
                        save.WarehouseId,
                        save.EmployeeId,
                        save.Note,
                        save.Lines)),
                CancellationToken.None);
        }
        else
        {
            result = await provider.GetRequiredService<ISender>().Send(
                new CreateGoodsReceiptDraftCommand(save),
                CancellationToken.None);
        }

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[0].Quantity");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[1].Quantity");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[1].UnitCost");
        Assert.Contains(result.Errors, error => Field(error) == "Request.Lines[2].UnitCost");
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_line_entry_is_rejected_before_a_unit_of_work_is_opened(bool update)
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);
        var save = new SaveGoodsReceiptRequest(
            new DateTime(2026, 10, 10),
            3,
            5,
            null,
            new SaveGoodsReceiptLineRequest[] { null! });

        ErrorOr<GoodsReceiptDto> result;
        if (update)
        {
            result = await provider.GetRequiredService<ISender>().Send(
                new UpdateGoodsReceiptDraftCommand(
                    new UpdateGoodsReceiptDraftRequest(
                        41,
                        VersionToken(1),
                        save.ReceiptDate,
                        save.WarehouseId,
                        save.EmployeeId,
                        save.Note,
                        save.Lines)),
                CancellationToken.None);
        }
        else
        {
            result = await provider.GetRequiredService<ISender>().Send(
                new CreateGoodsReceiptDraftCommand(save),
                CancellationToken.None);
        }

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error =>
            error.Code == "Validation.Required" && Field(error) == "Request.Lines[0]");
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Fact]
    public async Task More_than_200_lines_are_rejected_before_a_unit_of_work_is_opened()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);
        var lines = Enumerable.Range(1, 201)
            .Select(productId => new SaveGoodsReceiptLineRequest(productId, 1m, 1m));

        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(
                new SaveGoodsReceiptRequest(
                    new DateTime(2026, 10, 10),
                    3,
                    5,
                    null,
                    lines)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error =>
            error.Code == "Validation.MaximumLength" && Field(error) == "Request.Lines");
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Fact]
    public async Task Create_draft_commits_one_aggregate_transaction_and_returns_server_values()
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            Inserted = Receipt(41, "GR0000000041", 9),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork);

        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(ValidSaveRequest()),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(unitOfWork.BeganTransaction);
        Assert.True(unitOfWork.Committed);
        Assert.Equal(42, repository.LastHeader!.CreatedByUserId);
        Assert.Equal(new[] { 1, 2 }, repository.LastLines.Select(line => line.LineNumber));
        Assert.Equal("GR0000000041", result.Value.ReceiptNumber);
        Assert.Equal(VersionToken(9), result.Value.Version);
    }

    [Theory]
    [InlineData(GoodsReceiptStatuses.Posted, 1, "GoodsReceipt.AlreadyPosted")]
    [InlineData(GoodsReceiptStatuses.Draft, 2, "GoodsReceipt.ConcurrencyConflict")]
    public async Task Update_draft_maps_locked_header_conflicts_without_changing_lines(
        string status,
        byte versionByte,
        string expectedError)
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            Locked = Receipt(41, "GR0000000041", versionByte, status),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork);
        var save = ValidSaveRequest();

        var result = await provider.GetRequiredService<ISender>().Send(
            new UpdateGoodsReceiptDraftCommand(
                new UpdateGoodsReceiptDraftRequest(
                    41,
                    VersionToken(1),
                    save.ReceiptDate,
                    save.WarehouseId,
                    save.EmployeeId,
                    save.Note,
                    save.Lines)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(expectedError, result.FirstError.Code);
        Assert.True(unitOfWork.BeganTransaction);
        Assert.False(unitOfWork.Committed);
        Assert.Equal(0, repository.ReplaceCalls);
    }

    [Fact]
    public async Task Inactive_reference_returns_field_validation_and_rolls_back_the_aggregate()
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            References = new GoodsReceiptReferenceSnapshot(
                CatalogReferenceState.Inactive,
                CatalogReferenceState.Active,
                new Dictionary<int, CatalogReferenceState>
                {
                    [7] = CatalogReferenceState.Inactive,
                    [8] = CatalogReferenceState.Active,
                }),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork);

        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(ValidSaveRequest()),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error =>
            error.Code == "GoodsReceipt.WarehouseInactive" && Field(error) == "Request.WarehouseId");
        Assert.Contains(result.Errors, error =>
            error.Code == "GoodsReceipt.ProductInactive" && Field(error) == "Request.Lines[0].ProductId");
        Assert.False(unitOfWork.Committed);
        Assert.Equal(0, repository.InsertCalls);
    }

    [Fact]
    public async Task Explicit_write_deny_never_opens_a_unit_of_work()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork, allow: false);

        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(ValidSaveRequest()),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Fact]
    public async Task Explicit_deny_blocks_update_list_and_get_before_storage_access()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        var queries = new FakeGoodsReceiptQueryService();
        using var provider = CreateProvider(unitOfWork, allow: false, queryService: queries);
        var sender = provider.GetRequiredService<ISender>();
        var save = ValidSaveRequest();

        var update = await sender.Send(
            new UpdateGoodsReceiptDraftCommand(
                new UpdateGoodsReceiptDraftRequest(
                    41,
                    VersionToken(1),
                    save.ReceiptDate,
                    save.WarehouseId,
                    save.EmployeeId,
                    save.Note,
                    save.Lines)),
            CancellationToken.None);
        var list = await sender.Send(
            new ListGoodsReceiptsQuery(new GoodsReceiptListRequest(1, 25, null, null)),
            CancellationToken.None);
        var get = await sender.Send(new GetGoodsReceiptByIdQuery(41), CancellationToken.None);

        Assert.True(update.IsError);
        Assert.Equal(ErrorType.Forbidden, update.FirstError.Type);
        Assert.True(list.IsError);
        Assert.Equal(ErrorType.Forbidden, list.FirstError.Type);
        Assert.True(get.IsError);
        Assert.Equal(ErrorType.Forbidden, get.FirstError.Type);
        Assert.False(unitOfWork.BeganTransaction);
        Assert.Equal(0, queries.ListCalls);
        Assert.Equal(0, queries.GetCalls);
    }

    [Fact]
    public async Task Post_only_user_cannot_write_or_read_goods_receipts()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        var queries = new FakeGoodsReceiptQueryService();
        using var provider = CreateProvider(
            unitOfWork,
            queryService: queries,
            permissionStore: new PostOnlyPermissionStore());
        var sender = provider.GetRequiredService<ISender>();

        var create = await sender.Send(
            new CreateGoodsReceiptDraftCommand(ValidSaveRequest()),
            CancellationToken.None);
        var list = await sender.Send(
            new ListGoodsReceiptsQuery(new GoodsReceiptListRequest(1, 25, null, null)),
            CancellationToken.None);
        var get = await sender.Send(new GetGoodsReceiptByIdQuery(41), CancellationToken.None);

        Assert.True(create.IsError);
        Assert.Equal(ErrorType.Forbidden, create.FirstError.Type);
        Assert.True(list.IsError);
        Assert.Equal(ErrorType.Forbidden, list.FirstError.Type);
        Assert.True(get.IsError);
        Assert.Equal(ErrorType.Forbidden, get.FirstError.Type);
        Assert.False(unitOfWork.BeganTransaction);
        Assert.Equal(0, queries.ListCalls);
        Assert.Equal(0, queries.GetCalls);
    }

    [Theory]
    [InlineData(0, 25, null, null)]
    [InlineData(1, 0, null, null)]
    [InlineData(1, 201, null, null)]
    [InlineData(1, 25, "Invalid", null)]
    [InlineData(1, 25, null, "bad\0search")]
    public async Task Invalid_list_request_is_rejected_before_query_execution(
        int pageNumber,
        int pageSize,
        string? status,
        string? search)
    {
        var queries = new FakeGoodsReceiptQueryService();
        using var provider = CreateProvider(
            new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository()),
            queryService: queries);

        var result = await provider.GetRequiredService<ISender>().Send(
            new ListGoodsReceiptsQuery(
                new GoodsReceiptListRequest(pageNumber, pageSize, null, status, search)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task Search_longer_than_128_characters_is_rejected_before_query_execution()
    {
        var queries = new FakeGoodsReceiptQueryService();
        using var provider = CreateProvider(
            new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository()),
            queryService: queries);

        var result = await provider.GetRequiredService<ISender>().Send(
            new ListGoodsReceiptsQuery(
                new GoodsReceiptListRequest(1, 25, null, null, new string('s', 129))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task List_delegates_once_and_get_maps_missing_receipt()
    {
        var queries = new FakeGoodsReceiptQueryService
        {
            Page = new PagedResult<GoodsReceiptDto>(Array.Empty<GoodsReceiptDto>(), 1, 25, 0),
        };
        using var provider = CreateProvider(
            new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository()),
            queryService: queries);
        var sender = provider.GetRequiredService<ISender>();
        var request = new GoodsReceiptListRequest(1, 25, 3, "Draft", "GR");

        var list = await sender.Send(new ListGoodsReceiptsQuery(request), CancellationToken.None);
        var missing = await sender.Send(new GetGoodsReceiptByIdQuery(404), CancellationToken.None);

        Assert.False(list.IsError);
        Assert.Same(queries.Page, list.Value);
        Assert.Equal(1, queries.ListCalls);
        Assert.Same(request, queries.LastRequest);
        Assert.True(missing.IsError);
        Assert.Equal("GoodsReceipt.NotFound", missing.FirstError.Code);
        Assert.Equal(1, queries.GetCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_get_id_is_rejected_before_query_execution(long id)
    {
        var queries = new FakeGoodsReceiptQueryService();
        using var provider = CreateProvider(
            new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository()),
            queryService: queries);

        var result = await provider.GetRequiredService<ISender>().Send(
            new GetGoodsReceiptByIdQuery(id),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, queries.GetCalls);
    }

    [Theory]
    [InlineData(0, "AQID")]
    [InlineData(41, "not-base64")]
    public async Task Invalid_post_is_rejected_before_a_unit_of_work_is_opened(
        long receiptId,
        string expectedVersion)
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork);

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(receiptId, expectedVersion)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(unitOfWork.BeganTransaction);
    }

    [Fact]
    public async Task Post_locks_revalidates_and_mutates_the_aggregate_in_one_transaction()
    {
        var repository = new FakeGoodsReceiptWriteRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork, permissionStore: new PostOnlyPermissionStore());

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(unitOfWork.Committed);
        Assert.Equal(
            new[] { "header", "snapshot", "mark", "ledger", "balance" },
            repository.Operations);
        Assert.Equal(42, repository.PostedByUserId);
        Assert.Equal(new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc), repository.PostedAtUtc);
        Assert.Equal(2.5m, repository.LastBalanceDeltas[7]);
        Assert.Equal(1m, repository.LastBalanceDeltas[8]);
        Assert.Equal(GoodsReceiptStatuses.Posted, result.Value.Status);
    }

    [Theory]
    [InlineData(GoodsReceiptStatuses.Posted, 1, "GoodsReceipt.AlreadyPosted")]
    [InlineData(GoodsReceiptStatuses.Draft, 2, "GoodsReceipt.ConcurrencyConflict")]
    public async Task Post_maps_locked_header_conflicts_before_loading_lines(
        string status,
        byte versionByte,
        string expectedError)
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            Locked = Receipt(41, "GR0000000041", versionByte, status),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork, permissionStore: new PostOnlyPermissionStore());

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(expectedError, result.FirstError.Code);
        Assert.Equal(new[] { "header" }, repository.Operations);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Post_rejects_inactive_references_before_status_ledger_or_balance_changes()
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            PostSnapshot = new GoodsReceiptPostSnapshot(
                ValidPostLines(),
                new GoodsReceiptReferenceSnapshot(
                    CatalogReferenceState.Inactive,
                    CatalogReferenceState.Inactive,
                    new Dictionary<int, CatalogReferenceState>
                    {
                        [7] = CatalogReferenceState.Active,
                        [8] = CatalogReferenceState.Inactive,
                    })),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork, permissionStore: new PostOnlyPermissionStore());

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.WarehouseInactive");
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.EmployeeInactive");
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.ProductInactive");
        Assert.Equal(new[] { "header", "snapshot" }, repository.Operations);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Post_revalidates_line_invariants_before_mutation()
    {
        var repository = new FakeGoodsReceiptWriteRepository
        {
            PostSnapshot = new GoodsReceiptPostSnapshot(
                new[]
                {
                    new GoodsReceiptLine { Id = 701, ProductId = 7, Quantity = 0m, UnitCost = -1m },
                    new GoodsReceiptLine { Id = 702, ProductId = 7, Quantity = 1m, UnitCost = 0m },
                },
                ActivePostReferences()),
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork, permissionStore: new PostOnlyPermissionStore());

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.InvalidQuantity");
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.InvalidUnitCost");
        Assert.Contains(result.Errors, error => error.Code == "GoodsReceipt.DuplicateProduct");
        Assert.Equal(new[] { "header", "snapshot" }, repository.Operations);
    }

    [Fact]
    public async Task Post_conditional_transition_failure_returns_concurrency_conflict()
    {
        var repository = new FakeGoodsReceiptWriteRepository { TryMarkPosted = false };
        var unitOfWork = new FakeUnitOfWork(repository);
        using var provider = CreateProvider(unitOfWork, permissionStore: new PostOnlyPermissionStore());

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("GoodsReceipt.ConcurrencyConflict", result.FirstError.Code);
        Assert.Equal(new[] { "header", "snapshot", "mark" }, repository.Operations);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Explicit_post_deny_never_opens_a_unit_of_work()
    {
        var unitOfWork = new FakeUnitOfWork(new FakeGoodsReceiptWriteRepository());
        using var provider = CreateProvider(unitOfWork, allow: false);

        var result = await provider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(41, VersionToken(1))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.False(unitOfWork.BeganTransaction);
    }

    private static SaveGoodsReceiptRequest ValidSaveRequest() =>
        new SaveGoodsReceiptRequest(
            new DateTime(2026, 10, 10),
            3,
            5,
            "Receipt note",
            new[]
            {
                new SaveGoodsReceiptLineRequest(7, 2.5m, 19.99m),
                new SaveGoodsReceiptLineRequest(8, 1m, 0m),
            });

    private static GoodsReceipt Receipt(
        long id,
        string number,
        byte versionByte,
        string status = GoodsReceiptStatuses.Draft) =>
        new GoodsReceipt
        {
            Id = id,
            ReceiptNumber = number,
            ReceiptDate = new DateTime(2026, 10, 10),
            WarehouseId = 3,
            EmployeeId = 5,
            Status = status,
            CreatedAtUtc = new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc),
            CreatedByUserId = 42,
            Version = Enumerable.Repeat(versionByte, 8).ToArray(),
        };

    private static IReadOnlyList<GoodsReceiptLine> ValidPostLines() =>
        new[]
        {
            new GoodsReceiptLine { Id = 701, ReceiptId = 41, LineNumber = 1, ProductId = 7, Quantity = 2.5m, UnitCost = 19.99m },
            new GoodsReceiptLine { Id = 702, ReceiptId = 41, LineNumber = 2, ProductId = 8, Quantity = 1m, UnitCost = 0m },
        };

    private static GoodsReceiptReferenceSnapshot ActivePostReferences() =>
        new GoodsReceiptReferenceSnapshot(
            CatalogReferenceState.Active,
            CatalogReferenceState.Active,
            new Dictionary<int, CatalogReferenceState>
            {
                [7] = CatalogReferenceState.Active,
                [8] = CatalogReferenceState.Active,
            });

    private static string VersionToken(byte value) =>
        Convert.ToBase64String(Enumerable.Repeat(value, 8).ToArray());

    private static string? Field(Error error) =>
        error.Metadata != null && error.Metadata.TryGetValue("Field", out var value)
            ? value as string
            : null;

    private static ServiceProvider CreateProvider(
        FakeUnitOfWork unitOfWork,
        bool allow = true,
        IGoodsReceiptQueryService? queryService = null,
        IPermissionStore? permissionStore = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkFactory>(new FakeUnitOfWorkFactory(unitOfWork));
        services.AddSingleton(queryService ?? new FakeGoodsReceiptQueryService());
        services.AddSingleton<ICurrentUserAccessor>(
            new StubCurrentUserAccessor(
                CurrentUser.Authenticated(42, "operator", "Operator", isActive: true)));
        services.AddSingleton(permissionStore ?? new PermissionStore(allow));
        services.AddSingleton<IUtcClock>(new FixedUtcClock());
        services.AddServerApplication();
        return services.BuildServiceProvider();
    }

    private sealed class FakeGoodsReceiptQueryService : IGoodsReceiptQueryService
    {
        public PagedResult<GoodsReceiptDto> Page { get; set; } =
            new PagedResult<GoodsReceiptDto>(Array.Empty<GoodsReceiptDto>(), 1, 25, 0);

        public GoodsReceiptListRequest? LastRequest { get; private set; }
        public int ListCalls { get; private set; }
        public int GetCalls { get; private set; }

        public Task<PagedResult<GoodsReceiptDto>> ListAsync(
            GoodsReceiptListRequest request,
            CancellationToken cancellationToken)
        {
            ListCalls++;
            LastRequest = request;
            return Task.FromResult(Page);
        }

        public Task<GoodsReceiptDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult<GoodsReceiptDto?>(null);
        }
    }

    private sealed class FixedUtcClock : IUtcClock
    {
        public DateTime UtcNow { get; } =
            new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc);
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public StubCurrentUserAccessor(CurrentUser current) => Current = current;

        public CurrentUser Current { get; }
    }

    private sealed class PermissionStore : IPermissionStore
    {
        private readonly bool _allow;

        public PermissionStore(bool allow) => _allow = allow;

        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(_allow, hasRoleGrant: true));
    }

    private sealed class PostOnlyPermissionStore : IPermissionStore
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new PermissionSnapshot(
                    directDecision: null,
                    hasRoleGrant: permissionKey == PermissionKeys.InventoryGoodsReceiptsPost));
    }

    private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly IUnitOfWork _unitOfWork;

        public FakeUnitOfWorkFactory(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public IUnitOfWork Create() => _unitOfWork;

        public Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_unitOfWork);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly IGoodsReceiptWriteRepository _repository;

        public FakeUnitOfWork(IGoodsReceiptWriteRepository repository) => _repository = repository;

        public UnitOfWorkState State { get; private set; } = UnitOfWorkState.Created;
        public bool BeganTransaction { get; private set; }
        public bool Committed { get; private set; }

        public void BeginTransaction()
        {
            BeganTransaction = true;
            State = UnitOfWorkState.ActiveTransaction;
        }

        public TRepository Repository<TRepository>() where TRepository : class =>
            (TRepository)_repository;

        public void Commit()
        {
            Committed = true;
            State = UnitOfWorkState.Committed;
        }

        public void Rollback() => State = UnitOfWorkState.RolledBack;

        public void Dispose() => State = UnitOfWorkState.Disposed;
    }

    private sealed class FakeGoodsReceiptWriteRepository : IGoodsReceiptWriteRepository
    {
        public GoodsReceipt Inserted { get; set; } = Receipt(41, "GR0000000041", 1);
        public GoodsReceipt? Locked { get; set; } = Receipt(41, "GR0000000041", 1);
        public GoodsReceiptReferenceSnapshot References { get; set; } =
            new GoodsReceiptReferenceSnapshot(
                CatalogReferenceState.Active,
                CatalogReferenceState.Active,
                new Dictionary<int, CatalogReferenceState>
                {
                    [7] = CatalogReferenceState.Active,
                    [8] = CatalogReferenceState.Active,
                });
        public GoodsReceiptPostSnapshot PostSnapshot { get; set; } =
            new GoodsReceiptPostSnapshot(ValidPostLines(), ActivePostReferences());
        public bool TryMarkPosted { get; set; } = true;
        public List<string> Operations { get; } = new List<string>();
        public DateTime PostedAtUtc { get; private set; }
        public int PostedByUserId { get; private set; }
        public IReadOnlyDictionary<int, decimal> LastBalanceDeltas { get; private set; } =
            new Dictionary<int, decimal>();

        public GoodsReceipt? LastHeader { get; private set; }
        public IReadOnlyList<GoodsReceiptLine> LastLines { get; private set; } = Array.Empty<GoodsReceiptLine>();
        public int InsertCalls { get; private set; }
        public int ReplaceCalls { get; private set; }

        public Task<GoodsReceiptReferenceSnapshot> GetReferenceSnapshotAsync(
            int warehouseId,
            int employeeId,
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken) => Task.FromResult(References);

        public Task<GoodsReceipt> InsertDraftAsync(
            GoodsReceipt header,
            IReadOnlyList<GoodsReceiptLine> lines,
            CancellationToken cancellationToken)
        {
            InsertCalls++;
            LastHeader = header;
            LastLines = lines;
            return Task.FromResult(Inserted);
        }

        public Task<GoodsReceipt?> LockHeaderAsync(long receiptId, CancellationToken cancellationToken)
        {
            Operations.Add("header");
            return Task.FromResult(Locked);
        }

        public Task<GoodsReceiptPostSnapshot> LockPostSnapshotAsync(
            GoodsReceipt header,
            CancellationToken cancellationToken)
        {
            Operations.Add("snapshot");
            return Task.FromResult(PostSnapshot);
        }

        public Task<bool> TryMarkPostedAsync(
            long receiptId,
            byte[] expectedVersion,
            DateTime postedAtUtc,
            int postedByUserId,
            CancellationToken cancellationToken)
        {
            Operations.Add("mark");
            PostedAtUtc = postedAtUtc;
            PostedByUserId = postedByUserId;
            return Task.FromResult(TryMarkPosted);
        }

        public Task AppendStockLedgerAsync(
            long receiptId,
            int warehouseId,
            IReadOnlyList<GoodsReceiptLine> lines,
            DateTime postedAtUtc,
            int postedByUserId,
            CancellationToken cancellationToken)
        {
            Operations.Add("ledger");
            return Task.CompletedTask;
        }

        public Task ApplyStockBalanceDeltasAsync(
            int warehouseId,
            IReadOnlyDictionary<int, decimal> deltasByProductId,
            CancellationToken cancellationToken)
        {
            Operations.Add("balance");
            LastBalanceDeltas = deltasByProductId;
            return Task.CompletedTask;
        }

        public Task<GoodsReceipt> ReplaceDraftAsync(
            GoodsReceipt header,
            IReadOnlyList<GoodsReceiptLine> lines,
            CancellationToken cancellationToken)
        {
            ReplaceCalls++;
            LastHeader = header;
            LastLines = lines;
            return Task.FromResult(Receipt(header.Id, "GR0000000041", 3));
        }
    }
}
