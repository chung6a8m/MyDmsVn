using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.HttpHost;

internal sealed class SalesSaveLineHttpRequest
{
    public SalesSaveLineHttpRequest(long saleLineId, int productId, decimal qty, decimal unitPrice, bool isGift)
    {
        SaleLineId = saleLineId;
        ProductId = productId;
        Qty = qty;
        UnitPrice = unitPrice;
        IsGift = isGift;
    }

    public long SaleLineId { get; }
    public int ProductId { get; }
    public decimal Qty { get; }
    public decimal UnitPrice { get; }
    public bool IsGift { get; }
}

internal sealed class SalesCreateDraftHttpRequest
{
    public SalesCreateDraftHttpRequest(DateTime saleDate, int customerId, int warehouseId, int employeeId, string? note, IReadOnlyList<SalesSaveLineHttpRequest> lines)
    {
        SaleDate = saleDate;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Note = note;
        Lines = lines;
    }

    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string? Note { get; }
    public IReadOnlyList<SalesSaveLineHttpRequest> Lines { get; }
}

internal sealed class SalesUpdateDraftHttpRequest
{
    public SalesUpdateDraftHttpRequest(long saleId, string expectedVersion, DateTime saleDate, int customerId, int warehouseId, int employeeId, string? note, IReadOnlyList<SalesSaveLineHttpRequest> lines)
    {
        SaleId = saleId;
        ExpectedVersion = expectedVersion;
        SaleDate = saleDate;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Note = note;
        Lines = lines;
    }

    public long SaleId { get; }
    public string ExpectedVersion { get; }
    public DateTime SaleDate { get; }
    public int CustomerId { get; }
    public int WarehouseId { get; }
    public int EmployeeId { get; }
    public string? Note { get; }
    public IReadOnlyList<SalesSaveLineHttpRequest> Lines { get; }
}

internal sealed class SalesPostHttpRequest
{
    public SalesPostHttpRequest(long saleId, string expectedVersion)
    {
        SaleId = saleId;
        ExpectedVersion = expectedVersion;
    }

    public long SaleId { get; }
    public string ExpectedVersion { get; }
}

internal sealed class SalesListHttpRequest
{
    public SalesListHttpRequest(DateTime? fromDate, DateTime? toDate, int? customerId, int? warehouseId, int? employeeId, string? status, int? productId, string? search, int pageSize, string? cursor)
    {
        FromDate = fromDate;
        ToDate = toDate;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        EmployeeId = employeeId;
        Status = status;
        ProductId = productId;
        Search = search;
        PageSize = pageSize;
        Cursor = cursor;
    }

    public DateTime? FromDate { get; }
    public DateTime? ToDate { get; }
    public int? CustomerId { get; }
    public int? WarehouseId { get; }
    public int? EmployeeId { get; }
    public string? Status { get; }
    public int? ProductId { get; }
    public string? Search { get; }
    public int PageSize { get; }
    public string? Cursor { get; }
}

internal sealed class SalesGetHttpRequest
{
    public SalesGetHttpRequest(long saleId) { SaleId = saleId; }
    public long SaleId { get; }
}

internal static class SalesHttpRequestMapper
{
    public static SaveSalesReceiptRequest ToApplication(SalesCreateDraftHttpRequest value) =>
        new SaveSalesReceiptRequest(value.SaleDate, value.CustomerId, value.WarehouseId, value.EmployeeId, value.Note, Lines(value.Lines));

    public static UpdateSalesReceiptDraftRequest ToApplication(SalesUpdateDraftHttpRequest value) =>
        new UpdateSalesReceiptDraftRequest(value.SaleId, value.ExpectedVersion, value.SaleDate, value.CustomerId, value.WarehouseId, value.EmployeeId, value.Note, Lines(value.Lines));

    public static PostSalesReceiptRequest ToApplication(SalesPostHttpRequest value) =>
        new PostSalesReceiptRequest(value.SaleId, value.ExpectedVersion);

    public static SalesReceiptListRequest ToApplication(SalesListHttpRequest value) =>
        new SalesReceiptListRequest(value.FromDate, value.ToDate, value.CustomerId, value.WarehouseId, value.EmployeeId, value.Status, value.ProductId, value.Search, value.PageSize, value.Cursor);

    private static IReadOnlyList<SaveSalesReceiptLineRequest> Lines(IEnumerable<SalesSaveLineHttpRequest> values) =>
        values.Select(value => new SaveSalesReceiptLineRequest(value.SaleLineId, value.ProductId, value.Qty, value.UnitPrice, value.IsGift)).ToArray();
}
