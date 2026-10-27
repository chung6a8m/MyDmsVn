using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptQueryTests
{
    [SqlServerFact]
    public Task Each_filter_excludes_only_its_own_variant_and_combinations_use_AND() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        for (var i = 0; i < 9; i++) SalesDraftData.Success(await data.Create(provider));
        await NativeSql.Execute(oracle, null, @"INSERT dbo.Customers(Code,Name) VALUES(N'C2',N'Other'); INSERT dbo.Warehouses(Code,Name) VALUES(N'W2',N'Other'); INSERT dbo.Employees(Code,Name) VALUES(N'E2',N'Other'); INSERT dbo.Products(Code,Name,Unit) VALUES(N'P2',N'Other',N'EA');
UPDATE dbo.SalesReceipts SET Note=N'needle';
UPDATE dbo.SalesReceipts SET SaleDate='20261021' WHERE SaleId=2;
UPDATE dbo.SalesReceipts SET SaleDate='20261023' WHERE SaleId=3;
UPDATE dbo.SalesReceipts SET CustomerId=2 WHERE SaleId=4;
UPDATE dbo.SalesReceipts SET WarehouseId=2 WHERE SaleId=5;
UPDATE dbo.SalesReceipts SET EmployeeId=2 WHERE SaleId=6;
UPDATE dbo.SalesReceipts SET Status='Posted',PostedAtUtc=SYSUTCDATETIME(),PostedByUserId=CreatedByUserId WHERE SaleId=7;
UPDATE dbo.SalesReceiptLines SET ProductId=2 WHERE SaleId=8;
UPDATE dbo.SalesReceipts SET Note=N'other' WHERE SaleId=9;");
        foreach (var mask in new[] { 0, 1, 2, 4, 8, 16, 32, 64, 128, 3, 17, 90, 255 })
        {
            var request = new SalesReceiptListRequest((mask & 1) != 0 ? new DateTime(2026, 10, 22) : (DateTime?)null, (mask & 2) != 0 ? new DateTime(2026, 10, 22) : (DateTime?)null,
                (mask & 4) != 0 ? 1 : (int?)null, (mask & 8) != 0 ? 1 : (int?)null, (mask & 16) != 0 ? 1 : (int?)null, (mask & 32) != 0 ? "Draft" : null, (mask & 64) != 0 ? 1 : (int?)null, (mask & 128) != 0 ? "needle" : null);
            var page = await provider.GetRequiredService<ISalesReceiptQueryService>().ListAsync(request, CancellationToken.None);
            var expected = new[] { (1L, 0), (2L, 1), (3L, 2), (4L, 4), (5L, 8), (6L, 16), (7L, 32), (8L, 64), (9L, 128) }.Where(row => (row.Item2 & mask) == 0).Select(row => row.Item1).Reverse();
            Assert.Equal(expected, page.Items.Select(row => row.SaleId)); Assert.All(page.Items, row => Assert.Equal(75m, row.TotalAmount));
        }
    });

    [SqlServerFact]
    public Task Posted_gift_duplicate_summary_does_not_revalue_frozen_costs() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = SalesPostData.Provider(data, database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(oracle, data, 20, 300);
        Assert.False((await SalesPostData.Post(provider, saved)).IsError);
        var queries = provider.GetRequiredService<ISalesReceiptQueryService>(); var before = (await queries.GetByIdAsync(saved.SaleId, CancellationToken.None))!;
        Assert.Equal(90m, before.TotalCost); Assert.Equal(new[] { 45m, 30m, 15m }, before.Lines.Select(line => line.CostAmount!.Value));
        await NativeSql.Execute(oracle, null, "UPDATE dbo.StockBalances SET InventoryValue=999; UPDATE dbo.Customers SET IsActive=0; UPDATE dbo.Products SET IsActive=0");
        var page = await queries.ListAsync(new SalesReceiptListRequest(null, null, null, null, null, "Posted", data.Product, null), CancellationToken.None);
        var row = Assert.Single(page.Items); Assert.Equal(3, row.LineCount); Assert.Equal(75m, row.TotalAmount);
        var after = (await queries.GetByIdAsync(saved.SaleId, CancellationToken.None))!;
        Assert.Equal(before.Version, after.Version); Assert.Equal(before.TotalCost, after.TotalCost); Assert.Equal(before.GrossProfit, after.GrossProfit);
        Assert.Equal(before.Lines.Select(line => line.CostAmount), after.Lines.Select(line => line.CostAmount)); Assert.True(after.Lines[1].IsGift); Assert.Equal(25m, after.Lines[1].UnitPrice);
    });

    [SqlServerFact]
    public Task Inclusive_filters_historical_labels_and_literal_Unicode_search_combine() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        var one = SalesDraftData.Success(await data.Create(provider)); var two = SalesDraftData.Success(await data.Create(provider));
        await oracle.ExecuteAsync("UPDATE dbo.SalesReceipts SET SaleDate='20261021',Note=N'Đ%_[' WHERE SaleId=@id; UPDATE dbo.Customers SET Code=N'C_%[',Name=N'Khách Đ'; UPDATE dbo.Warehouses SET IsActive=0; UPDATE dbo.Employees SET IsActive=0; UPDATE dbo.Products SET IsActive=0", new { id = one.SaleId });
        var queries = provider.GetRequiredService<ISalesReceiptQueryService>();
        foreach (var search in new[] { "Đ%_[", "%", "_", "[", "Khách", "C_%[", one.SaleNumber })
        {
            var page = await queries.ListAsync(new SalesReceiptListRequest(new DateTime(2026, 10, 21), new DateTime(2026, 10, 21), data.Customer, data.Warehouse, data.Employee, "Draft", data.Product, " " + search + " "), CancellationToken.None);
            var row = Assert.Single(page.Items); Assert.Equal(one.SaleId, row.SaleId); Assert.Equal(3, row.LineCount); Assert.Equal(75m, row.TotalAmount); Assert.Equal("C_%[ - Khách Đ", row.CustomerDisplayName);
        }
        foreach (var request in new[]{new SalesReceiptListRequest(null,null,int.MaxValue,null,null,null,null,null),new SalesReceiptListRequest(null,null,null,int.MaxValue,null,null,null,null),
            new SalesReceiptListRequest(null,null,null,null,int.MaxValue,null,null,null),new SalesReceiptListRequest(null,null,null,null,null,"Posted",null,null),
            new SalesReceiptListRequest(null,null,null,null,null,null,int.MaxValue,null),new SalesReceiptListRequest(null,null,null,null,null,null,null,"Warehouse")})
            Assert.Empty((await queries.ListAsync(request, CancellationToken.None)).Items);
        // Date is independent of identity order; later SaleId remains first despite earlier business date.
        await oracle.ExecuteAsync("UPDATE dbo.SalesReceipts SET SaleDate='00010101' WHERE SaleId=@id", new { id = two.SaleId });
        var both = await queries.ListAsync(new SalesReceiptListRequest(DateTime.MinValue, DateTime.MaxValue, null, null, null, null, null, null), CancellationToken.None);
        Assert.Equal(new[] { two.SaleId, one.SaleId }, both.Items.Select(r => r.SaleId));
    });

    [SqlServerFact]
    public Task Literal_wildcards_do_not_match_unrelated_text() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        var one = SalesDraftData.Success(await data.Create(provider)); var two = SalesDraftData.Success(await data.Create(provider));
        await oracle.ExecuteAsync("UPDATE dbo.SalesReceipts SET Note=N'A%_[Đ' WHERE SaleId=@id", new { id = one.SaleId });
        foreach (var text in new[] { "%", "_", "[", "Đ" })
        {
            var page = await provider.GetRequiredService<ISalesReceiptQueryService>().ListAsync(new SalesReceiptListRequest(null, null, null, null, null, null, null, text), CancellationToken.None);
            Assert.Equal(one.SaleId, Assert.Single(page.Items).SaleId);
        }
    });

    [SqlServerFact]
    public Task Removed_or_nonmatching_anchors_and_live_inserts_follow_key_comparisons() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        for (var i = 0; i < 6; i++) SalesDraftData.Success(await data.Create(provider));
        var queries = provider.GetRequiredService<ISalesReceiptQueryService>();
        SalesReceiptListRequest Request(string? cursor = null) => new(new DateTime(2026, 10, 22), new DateTime(2026, 10, 22), null, null, null, "Draft", null, null, 2, cursor);
        var first = await queries.ListAsync(Request(), CancellationToken.None); Assert.Equal(new long[] { 6, 5 }, first.Items.Select(r => r.SaleId));
        await NativeSql.Execute(oracle, null, "DELETE dbo.SalesReceiptLines WHERE SaleId=5; DELETE dbo.SalesReceipts WHERE SaleId=5");
        SalesDraftData.Success(await data.Create(provider)); // Id7 appears only upon a fresh/previous request.
        var next = await queries.ListAsync(Request(first.NextCursor), CancellationToken.None); Assert.Equal(new long[] { 4, 3 }, next.Items.Select(r => r.SaleId));
        await NativeSql.Execute(oracle, null, "UPDATE dbo.SalesReceipts SET SaleDate='20261021' WHERE SaleId=3; UPDATE dbo.SalesReceipts SET Status='Posted',PostedAtUtc=SYSUTCDATETIME(),PostedByUserId=CreatedByUserId WHERE SaleId=2");
        var last = await queries.ListAsync(Request(next.NextCursor), CancellationToken.None); Assert.Equal(1, Assert.Single(last.Items).SaleId); Assert.Null(last.NextCursor);
        var previous = await queries.ListAsync(Request(last.PreviousCursor), CancellationToken.None); Assert.Equal(new long[] { 6, 4 }, previous.Items.Select(r => r.SaleId));
        var refreshed = await queries.ListAsync(Request(), CancellationToken.None); Assert.Equal(new long[] { 7, 6 }, refreshed.Items.Select(r => r.SaleId));
        await NativeSql.Execute(oracle, null, "DELETE dbo.SalesReceiptLines; DELETE dbo.SalesReceipts");
        var empty = await queries.ListAsync(Request(last.PreviousCursor), CancellationToken.None); Assert.Empty(empty.Items); Assert.Null(empty.NextCursor); Assert.Null(empty.PreviousCursor);
    });

    [SqlServerFact]
    public Task Corrupt_document_total_overflow_fails_closed_without_mutation() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        await data.Create(provider);
        await NativeSql.Execute(oracle, null, "UPDATE dbo.SalesReceiptLines SET LineAmount=999999999999999999999999.9999 WHERE IsGift=0");
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<ISalesReceiptQueryService>().ListAsync(Request(50), CancellationToken.None));
        Assert.Equal(3, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines"));
    });

    [SqlServerTheory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(11)]
    [InlineData(50)]
    public Task Both_directions_cover_exact_full_partial_and_empty_pages(int size) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        var ids = new long[11]; for (var i = 0; i < ids.Length; i++) ids[i] = SalesDraftData.Success(await data.Create(provider)).SaleId;
        var queries = provider.GetRequiredService<ISalesReceiptQueryService>();
        using var navigation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var maximumSteps = (ids.Length + size - 1) / size - 1;
        var page = await queries.ListAsync(Request(size), navigation.Token); Assert.Null(page.PreviousCursor); Assert.NotEmpty(page.Items);
        var seen = page.Items.Select(r => r.SaleId).ToList();
        var nextSteps = 0;
        while (page.NextCursor != null)
        {
            Assert.True(++nextSteps <= maximumSteps, "Next exceeded the fixture page budget.");
            var boundary = page.Items[page.Items.Count - 1].SaleId;
            page = await queries.ListAsync(Request(size, page.NextCursor), navigation.Token);
            Assert.NotEmpty(page.Items);
            Assert.All(page.Items, row => Assert.True(row.SaleId < boundary, "Next must advance below the retained boundary."));
            seen.AddRange(page.Items.Select(r => r.SaleId)); Assert.True(seen.Count <= ids.Length);
        }
        Assert.Equal(ids.Reverse(), seen);
        var previousSteps = 0;
        while (page.PreviousCursor != null)
        {
            Assert.True(++previousSteps <= maximumSteps, "Previous exceeded the fixture page budget.");
            var boundary = page.Items[0].SaleId;
            page = await queries.ListAsync(Request(size, page.PreviousCursor), navigation.Token);
            Assert.NotEmpty(page.Items);
            Assert.All(page.Items, row => Assert.True(row.SaleId > boundary, "Previous must advance above the retained boundary."));
        }
        Assert.Equal(ids.Reverse().Take(size), page.Items.Select(r => r.SaleId)); Assert.Null(page.PreviousCursor);
        await NativeSql.Execute(oracle, null, "DELETE dbo.SalesReceiptLines; DELETE dbo.SalesReceipts");
        var empty = await queries.ListAsync(Request(size), navigation.Token); Assert.Empty(empty.Items); Assert.Null(empty.NextCursor); Assert.Null(empty.PreviousCursor);
    });

    [SqlServerFact]
    public Task Previous_trims_before_reverse_and_anchors_only_retained_rows() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        for (var i = 0; i < 11; i++) SalesDraftData.Success(await data.Create(provider));
        var queries = provider.GetRequiredService<ISalesReceiptQueryService>();
        var first = await queries.ListAsync(Request(3), CancellationToken.None);
        var second = await queries.ListAsync(Request(3, first.NextCursor), CancellationToken.None);
        var third = await queries.ListAsync(Request(3, second.NextCursor), CancellationToken.None);
        Assert.Equal(new long[] { 5, 4, 3 }, third.Items.Select(r => r.SaleId));
        var previous = await queries.ListAsync(Request(3, third.PreviousCursor), CancellationToken.None);
        Assert.Equal(new long[] { 8, 7, 6 }, previous.Items.Select(r => r.SaleId));
        Assert.Equal(6, Anchor(previous.NextCursor!)); Assert.Equal(8, Anchor(previous.PreviousCursor!));
        var custom = ReplaceAnchor(third.PreviousCursor!, 7);
        var boundary = await queries.ListAsync(Request(3, custom), CancellationToken.None);
        Assert.Equal(new long[] { 10, 9, 8 }, boundary.Items.Select(r => r.SaleId)); Assert.Equal(8, Anchor(boundary.NextCursor!)); Assert.Equal(10, Anchor(boundary.PreviousCursor!));
    });

    private static SalesReceiptListRequest Request(int size, string? cursor = null) => new(null, null, null, null, null, null, null, null, size, cursor);
    internal static long Anchor(string cursor) => (long)ReadCursor(cursor)["anchor"]!;
    internal static Newtonsoft.Json.Linq.JObject ReadCursor(string cursor) => Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/') + new string('=', (4 - cursor.Length % 4) % 4))));
    internal static string ReplaceAnchor(string cursor, long anchor)
    { var json = ReadCursor(cursor); json["anchor"] = anchor; return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None))).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

    [SqlServerFact]
    public Task Product_membership_keeps_duplicate_header_unique_and_all_line_totals() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        var other = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'OTHER',N'Other',N'EA')");
        await data.Update(provider, saved, saved.Lines.Select(l => new SaveSalesReceiptLineRequest(l.SaleLineId, l.ProductId, l.Qty, l.UnitPrice, l.IsGift))
            .Concat(new[] { new SaveSalesReceiptLineRequest(0, other, 2, 10, false) }).ToArray());
        var page = await provider.GetRequiredService<ISalesReceiptQueryService>().ListAsync(new SalesReceiptListRequest(null, null, null, null, null, null, data.Product, null), CancellationToken.None);
        var row = Assert.Single(page.Items); Assert.Equal(saved.SaleId, row.SaleId); Assert.Equal(4, row.LineCount); Assert.Equal(95m, row.TotalAmount);
        Assert.Null(page.NextCursor); Assert.Null(page.PreviousCursor);
    });
}
