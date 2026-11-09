using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

public enum WebOperationReconciliationOutcome { Unknown, Committed, Superseded }

// Support evidence only. No response, current/original version, document ID or line ID escapes.
public sealed class WebOperationBusinessReconciliation
{
    internal WebOperationBusinessReconciliation(WebOperationReconciliationOutcome outcome, int lines = 0, int lineage = 0)
    { Outcome = outcome; VerifiedLineCount = lines; VerifiedLineageCount = lineage; }
    public WebOperationReconciliationOutcome Outcome { get; }
    public int VerifiedLineCount { get; }
    public int VerifiedLineageCount { get; }
}

// Explicit maintenance construction only; never registered in a runtime container.
// The caller owns and ROLLS BACK the read transaction. All query identifiers are fixed.
public sealed class SqlWebOperationReconciliationMaintenance
{
    private readonly ISqlExecutionContext _context;
    private static WebOperationBusinessReconciliation Unknown() => new WebOperationBusinessReconciliation(WebOperationReconciliationOutcome.Unknown);
    public SqlWebOperationReconciliationMaintenance(ISqlExecutionContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<WebOperationBusinessReconciliation> ReconcileAsync(int actor, Guid epoch, Guid operation, byte[] fingerprint, CancellationToken token)
    {
        if (actor <= 0 || epoch == Guid.Empty || operation == Guid.Empty || fingerprint == null || fingerprint.Length != 32)
            throw new ArgumentException("An exact operation identity is required.");
        if (_context.Transaction == null) throw new InvalidOperationException("An explicit read transaction is required.");
        var expected = (byte[])fingerprint.Clone();
        using (var authority = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0);"))
            if (!Equals(await authority.ExecuteScalarAsync(token).ConfigureAwait(false), 1)) return Unknown();

        // Reconciliation may inspect a stored epoch after an explicit deployment rotation.
        // The client independently locks/verifies the current epoch; the original row matches stored epoch.
        using (var fence = SqlRead.Command(_context.Connection, _context.Transaction,
            "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @r;",
            SqlRead.Text("@key", "MyDmsVn.WebOperation:" + actor.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + operation.ToString("N"))))
            if (Convert.ToInt32(await fence.ExecuteScalarAsync(token).ConfigureAwait(false)) < 0) return Unknown();

        JournalRow? original = null;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            JournalColumns + " FROM dbo.WebOperationJournal WITH(HOLDLOCK) WHERE ActorUserId=@actor AND OperationId=@operation;",
            SqlRead.P("@actor", SqlDbType.Int, actor), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operation)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            if (await reader.ReadAsync(token).ConfigureAwait(false)) original = ReadJournal(reader);
        if (original == null || !original.Valid || original.Epoch != epoch || !original.Fingerprint.SequenceEqual(expected) || !original.Unavailable) return Unknown();

        var sales = original.Kind <= 3;
        // Freeze the established shared writer protocol while reading source and full stock-key
        // histories. HOLDLOCK also protects source/line ranges from concurrent Draft/admin edits.
        await InventoryValuationCoordinator.AcquireGateAsync(_context, true, token).ConfigureAwait(false);
        Header? header = null;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction, sales ? SalesHeader : ReceiptHeader,
            SqlRead.P("@target", SqlDbType.BigInt, original.Target)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            if (await reader.ReadAsync(token).ConfigureAwait(false) && reader.GetString(1) == "Posted" && !reader.IsDBNull(2) && !reader.IsDBNull(3))
                header = new Header { Warehouse = reader.GetInt32(0), Posted = reader.GetDateTime(2), Actor = reader.GetInt32(3) };
        if (header == null || header.Actor != actor) return Unknown();

        var lineage = new List<JournalRow>();
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            JournalColumns + @" FROM dbo.WebOperationJournal WITH(HOLDLOCK)
WHERE OperationKind BETWEEN @first AND @last AND (RequestTargetId=@target OR ResultDocumentId=@target)
ORDER BY CompletedAtUtc,OperationId;",
            SqlRead.P("@first", SqlDbType.TinyInt, sales ? 1 : 4), SqlRead.P("@last", SqlDbType.TinyInt, sales ? 3 : 6), SqlRead.P("@target", SqlDbType.BigInt, original.Target)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (lineage.Count >= 1024) return Unknown();
                lineage.Add(ReadJournal(reader));
            }

        // Reject ambiguous ownership/reuse anywhere in the inspected target history, not just a
        // conveniently selected later row. Equal timestamps are not ordered by a GUID tie-break.
        var prior = DateTime.MinValue;
        var terminal = false;
        for (var i = 0; i < lineage.Count; i++)
        {
            var row = lineage[i];
            if (!row.Valid || row.Actor != actor || row.Epoch != epoch || row.Target != original.Target ||
                row.Completed <= prior || terminal || (row.IsCreate && i != 0)) return Unknown();
            prior = row.Completed;
            terminal = row.IsPost;
        }
        if (lineage.Count == 0 || !terminal) return Unknown();
        var last = lineage[lineage.Count - 1];
        if (original.IsPost && last.Operation != operation) return Unknown();
        if (!original.IsPost && last.Completed <= original.Completed) return Unknown();

        var lines = new List<Line>();
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction, sales ? SalesLines : ReceiptLines,
            SqlRead.P("@target", SqlDbType.BigInt, original.Target)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (lines.Count >= 200) return Unknown();
                lines.Add(new Line { Id = reader.GetInt64(0), Number = reader.GetInt32(1), Product = reader.GetInt32(2) });
            }
        if (lines.Count == 0 || lines.Where((line, i) => line.Id <= 0 || line.Number != i + 1 || line.Product <= 0).Any() ||
            (!sales && lines.Select(l => l.Product).Distinct().Count() != lines.Count)) return Unknown();

        // Check the exact target's entire ledger, including extraneous rows on other stock keys.
        var matched = new HashSet<long>();
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DocumentLineId,WarehouseId,ProductId,PostedAtUtc,PostedByUserId FROM dbo.StockLedger WITH(HOLDLOCK) WHERE DocumentType=@type AND DocumentId=@target;",
            SqlRead.Text("@type", sales ? "SalesReceipt" : "GoodsReceipt"), SqlRead.P("@target", SqlDbType.BigInt, original.Target)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var id = reader.GetInt64(0);
                if (!matched.Add(id) || reader.GetInt32(1) != header.Warehouse || reader.GetDateTime(3) != header.Posted || reader.GetInt32(4) != actor ||
                    !lines.Any(line => line.Id == id && line.Product == reader.GetInt32(2))) return Unknown();
            }
        if (matched.Count != lines.Count) return Unknown();
        foreach (var product in lines.Select(line => line.Product).Distinct().OrderBy(id => id))
            if (!await VerifyStockKey(header.Warehouse, product, token).ConfigureAwait(false)) return Unknown();
        return new WebOperationBusinessReconciliation(original.IsPost ? WebOperationReconciliationOutcome.Committed : WebOperationReconciliationOutcome.Superseded,
            lines.Count, lineage.Count);
    }

    private async Task<bool> VerifyStockKey(int warehouse, int product, CancellationToken token)
    {
        var movements = new List<Movement>();
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction, History,
            SqlRead.P("@warehouse", SqlDbType.Int, warehouse), SqlRead.P("@product", SqlDbType.Int, product)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (movements.Count >= 100000 || reader.GetInt32(8) != 1 || reader.IsDBNull(4) || reader.IsDBNull(5) || reader.IsDBNull(6) || reader.IsDBNull(7)) return false;
                movements.Add(new Movement { Ledger = reader.GetInt64(0), Type = reader.GetString(1), Document = reader.GetInt64(2),
                    Quantity = reader.GetDecimal(3), Value = reader.GetDecimal(4), Number = reader.GetInt32(5), Price = reader.GetDecimal(6),
                    Cost = reader.GetDecimal(7) });
            }
        BigInteger quantity = 0, value = 0;
        long previous = 0;
        // Ledger allocation is serialized per stock key by the existing writer protocol. A
        // document's movements must be contiguous on that key; a reused source is ambiguous.
        foreach (var group in movements.GroupBy(m => new { m.Type, m.Document }).OrderBy(g => g.Min(m => m.Ledger)))
        {
            var rows = group.OrderBy(m => m.Number).ToArray();
            if (rows.Min(m => m.Ledger) <= previous || rows.Select(m => m.Number).Distinct().Count() != rows.Length) return false;
            previous = rows.Max(m => m.Ledger);
            if (group.Key.Type == "GoodsReceipt")
            {
                foreach (var row in rows)
                {
                    if (row.Quantity <= 0 || row.Price < 0 || Units(row.Value, 4) != Round(Units(row.Quantity, 4) * Units(row.Price, 4), 10000)) return false;
                    quantity += Units(row.Quantity, 4); value += Units(row.Value, 4);
                }
            }
            else if (group.Key.Type == "SalesReceipt")
            {
                if (quantity <= 0 || rows.Any(m => m.Quantity >= 0)) return false;
                var issued = rows.Aggregate(BigInteger.Zero, (sum, m) => sum - Units(m.Quantity, 4));
                if (issued > quantity) return false;
                var cost = issued == quantity ? value : Round(value * issued, quantity);
                var unitCost = Round(value * BigInteger.Pow(10, 8), quantity);
                BigInteger cumulative = 0, allocated = 0;
                foreach (var row in rows)
                {
                    cumulative -= Units(row.Quantity, 4);
                    var next = cumulative == issued ? cost : Round(cost * cumulative, issued);
                    if (Units(row.Price, 8) != unitCost || Units(row.Cost, 4) != next - allocated || Units(row.Value, 4) != -Units(row.Cost, 4)) return false;
                    allocated = next;
                }
                quantity -= issued; value -= cost;
            }
            else return false;
            if (quantity < 0 || value < 0 || (quantity.IsZero && !value.IsZero)) return false;
        }
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT Quantity,InventoryValue FROM dbo.StockBalances WITH(HOLDLOCK) WHERE WarehouseId=@warehouse AND ProductId=@product;",
            SqlRead.P("@warehouse", SqlDbType.Int, warehouse), SqlRead.P("@product", SqlDbType.Int, product)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            return await reader.ReadAsync(token).ConfigureAwait(false) && Units(reader.GetDecimal(0), 4) == quantity && Units(reader.GetDecimal(1), 4) == value;
    }

    private static BigInteger Units(decimal value, int scale)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        var storedScale = (bits[3] >> 16) & 255;
        if (storedScale <= scale) coefficient *= BigInteger.Pow(10, scale - storedScale);
        else
        {
            coefficient = BigInteger.DivRem(coefficient, BigInteger.Pow(10, storedScale - scale), out var remainder);
            if (!remainder.IsZero) throw new InvalidOperationException("Unsupported fixed-point source.");
        }
        return bits[3] < 0 ? -coefficient : coefficient;
    }
    private static BigInteger Round(BigInteger numerator, BigInteger denominator) => (numerator * 2 + denominator) / (denominator * 2);

    private const string JournalColumns = @"SELECT ActorUserId,OperationId,DeploymentEpoch,OperationKind,RequestTargetId,ResultDocumentId,RequestFingerprint,CompletedAtUtc,
CASE WHEN ContractVersion=1 AND CodecVersion=1 AND CreatedAtUtc<=CompletedAtUtc AND ResultDocumentId>0
AND ((OperationKind IN(1,4) AND RequestTargetId IS NULL) OR (OperationKind IN(2,3,5,6) AND RequestTargetId=ResultDocumentId))
AND ResultExpiresAtUtc=DATEADD(day,7,CompletedAtUtc)
AND ((Disposition COLLATE Latin1_General_100_BIN2='COMMITTED' AND DATALENGTH(Disposition)=9 AND DATALENGTH(ResultBytes) BETWEEN 1 AND 1048576)
OR (Disposition COLLATE Latin1_General_100_BIN2='EXPIRED' AND DATALENGTH(Disposition)=7 AND ResultBytes IS NULL AND ResultExpiresAtUtc<=SYSUTCDATETIME())) THEN 1 ELSE 0 END,
CASE WHEN ResultBytes IS NULL OR ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 1 ELSE 0 END";
    private static JournalRow ReadJournal(IDataRecord r) => new JournalRow { Actor = r.GetInt32(0), Operation = r.GetGuid(1), Epoch = r.GetGuid(2), Kind = r.GetByte(3),
        Target = r.IsDBNull(5) ? 0 : r.GetInt64(5), Fingerprint = (byte[])r[6], Completed = r.GetDateTime(7), Valid = r.GetInt32(8) == 1, Unavailable = r.GetInt32(9) == 1 };
    private sealed class JournalRow
    {
        public int Actor; public Guid Operation; public Guid Epoch; public byte Kind; public long Target; public byte[] Fingerprint = null!;
        public DateTime Completed; public bool Valid; public bool Unavailable;
        public bool IsCreate => Kind == 1 || Kind == 4;
        public bool IsPost => Kind == 3 || Kind == 6;
    }
    private sealed class Header { public int Warehouse; public DateTime Posted; public int Actor; }
    private sealed class Line { public long Id; public int Number; public int Product; }
    private sealed class Movement { public long Ledger; public string Type = ""; public long Document; public decimal Quantity; public decimal Value; public int Number; public decimal Price; public decimal Cost; }
    private const string SalesHeader = "SELECT WarehouseId,Status,PostedAtUtc,PostedByUserId FROM dbo.SalesReceipts WITH(HOLDLOCK) WHERE SaleId=@target;";
    private const string ReceiptHeader = "SELECT WarehouseId,Status,PostedAtUtc,PostedByUserId FROM dbo.GoodsReceipts WITH(HOLDLOCK) WHERE ReceiptId=@target;";
    private const string SalesLines = "SELECT SaleLineId,[LineNo],ProductId FROM dbo.SalesReceiptLines WITH(HOLDLOCK) WHERE SaleId=@target ORDER BY [LineNo];";
    private const string ReceiptLines = "SELECT ReceiptLineId,[LineNo],ProductId FROM dbo.GoodsReceiptLines WITH(HOLDLOCK) WHERE ReceiptId=@target ORDER BY [LineNo];";
    private const string History = @"
SELECT l.LedgerId,l.DocumentType,l.DocumentId,l.QuantityChange,v.ValueChange,
CASE WHEN l.DocumentType='GoodsReceipt' THEN r.[LineNo] ELSE s.[LineNo] END,
CASE WHEN l.DocumentType='GoodsReceipt' THEN CONVERT(decimal(28,8),r.UnitCost) ELSE s.PostedUnitCost END,
CASE WHEN l.DocumentType='GoodsReceipt' THEN CONVERT(decimal(28,4),0) ELSE s.CostAmount END,
CASE WHEN l.DocumentType='GoodsReceipt' AND DATALENGTH(l.DocumentType)=12 AND r.ReceiptId=l.DocumentId AND r.ProductId=l.ProductId AND r.Quantity=l.QuantityChange
AND rh.Status='Posted' AND DATALENGTH(rh.Status)=6 AND rh.WarehouseId=l.WarehouseId AND rh.PostedAtUtc=l.PostedAtUtc AND rh.PostedByUserId=l.PostedByUserId THEN 1
WHEN l.DocumentType='SalesReceipt' AND DATALENGTH(l.DocumentType)=12 AND s.SaleId=l.DocumentId AND s.ProductId=l.ProductId AND -s.Qty=l.QuantityChange
AND sh.Status='Posted' AND DATALENGTH(sh.Status)=6 AND sh.WarehouseId=l.WarehouseId AND sh.PostedAtUtc=l.PostedAtUtc AND sh.PostedByUserId=l.PostedByUserId
AND s.CostAmount>=0 AND s.PostedUnitCost>=0 AND (s.IsGift=0 OR s.LineAmount=0) THEN 1 ELSE 0 END
FROM dbo.StockLedger l WITH(HOLDLOCK)
LEFT JOIN dbo.StockValuationEntries v WITH(HOLDLOCK) ON v.LedgerId=l.LedgerId
LEFT JOIN dbo.GoodsReceiptLines r WITH(HOLDLOCK) ON l.DocumentType='GoodsReceipt' AND r.ReceiptLineId=l.DocumentLineId
LEFT JOIN dbo.GoodsReceipts rh WITH(HOLDLOCK) ON rh.ReceiptId=r.ReceiptId
LEFT JOIN dbo.SalesReceiptLines s WITH(HOLDLOCK) ON l.DocumentType='SalesReceipt' AND s.SaleLineId=l.DocumentLineId
LEFT JOIN dbo.SalesReceipts sh WITH(HOLDLOCK) ON sh.SaleId=s.SaleId
WHERE l.WarehouseId=@warehouse AND l.ProductId=@product ORDER BY l.LedgerId;";
}
