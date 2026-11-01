using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Operations;

public enum WebOperationKind : byte
{
    SalesCreate = 1,
    SalesUpdate = 2,
    SalesPost = 3,
    ReceiptCreate = 4,
    ReceiptUpdate = 5,
    ReceiptPost = 6
}

// Server-only metadata. Actor is supplied by the authorized Application command, never this context.
public sealed class WebOperationContext
{
    private readonly byte[] _fingerprint;
    public WebOperationContext(Guid operationId, Guid deploymentEpoch, Guid sessionId, WebOperationKind kind,
        long? requestTargetId, byte[] fingerprint, IWebOperationResultCodec codec)
    {
        if (operationId == Guid.Empty || operationId.ToString("D")[14] != '4' || "89ab".IndexOf(operationId.ToString("D")[19]) < 0 || deploymentEpoch == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Complete operation, deployment and session identities are required.");
        if (!Enum.IsDefined(typeof(WebOperationKind), kind) || ((kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate) ? requestTargetId.HasValue : !requestTargetId.HasValue || requestTargetId <= 0))
            throw new ArgumentException("Operation kind and target are inconsistent.");
        if (fingerprint == null || fingerprint.Length != 32)
            throw new ArgumentException("A SHA256 fingerprint is required.");
        OperationId = operationId;
        DeploymentEpoch = deploymentEpoch;
        SessionId = sessionId;
        Kind = kind;
        RequestTargetId = requestTargetId;
        _fingerprint = (byte[])fingerprint.Clone();
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }
    public Guid OperationId { get; }
    public Guid DeploymentEpoch { get; }
    public Guid SessionId { get; }
    public WebOperationKind Kind { get; }
    public long? RequestTargetId { get; }
    public byte[] Fingerprint => (byte[])_fingerprint.Clone();
    public IWebOperationResultCodec Codec { get; }
    public static string Permission(WebOperationKind kind) => kind switch
    {
        WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => PermissionKeys.SalesReceiptsWrite,
        WebOperationKind.SalesPost => PermissionKeys.SalesReceiptsPost,
        WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => PermissionKeys.InventoryGoodsReceiptsWrite,
        WebOperationKind.ReceiptPost => PermissionKeys.InventoryGoodsReceiptsPost,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

// HTTP owns the allowlisted, pure, version1 wire codec in B04. No I/O or later Get is allowed.
public interface IWebOperationResultCodec
{
    byte[] Encode<T>(WebOperationKind kind, T originalResult);
    T Decode<T>(WebOperationKind kind, byte[] bytes);
}

public interface IWebOperationJournal
{
    Task<byte[]?> AdmitAsync(WebOperationContext context, int actorUserId, CancellationToken token);
    Task CompleteAsync(WebOperationContext context, int actorUserId, long resultDocumentId, byte[] bytes, CancellationToken token);
}

public sealed class WebOperationException : Exception
{
    public WebOperationException(string code) : base(code) { Code = code; }
    public string Code { get; }
}

internal static class WebOperationExecution
{
    internal const int MaximumResultBytes = 1048576;
    public static async Task<T?> ReplayAsync<T>(IUnitOfWork uow, WebOperationContext? context, int actor,
        WebOperationKind kind, long? target, CancellationToken token) where T : class
    {
        if (context == null)
            return null;
        if (context.Kind != kind || context.RequestTargetId != target)
            throw new WebOperationException("Idempotency.KeyConflict");
        var bytes = await uow.Repository<IWebOperationJournal>().AdmitAsync(context, actor, token).ConfigureAwait(false);
        if (bytes == null)
            return null;
        try
        {
            return context.Codec.Decode<T>(kind, bytes) ?? throw new WebOperationException("Operation.ResultUnavailable");
        }
        catch { throw new WebOperationException("Operation.ResultUnavailable"); }
    }
    public static async Task CompleteAsync<T>(IUnitOfWork uow, WebOperationContext? context, int actor, long resultId, T result, CancellationToken token)
    {
        if (context == null)
            return;
        var bytes = context.Codec.Encode(context.Kind, result);
        if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumResultBytes)
            throw new WebOperationException("Operation.ResultUnavailable");
        // Reject invalid UTF8 even when a codec implementation is faulty.
        new System.Text.UTF8Encoding(false, true).GetString(bytes);
        await uow.Repository<IWebOperationJournal>().CompleteAsync(context, actor, resultId, bytes, token).ConfigureAwait(false);
    }
}
