using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using FluentValidation;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;

namespace MyDmsVn.Server.Application.Sales;

public sealed class PostSalesReceiptCommand : AuthorizedActorApplicationRequest<PostSalesReceiptResponse>, ISecurityAuditedRequest
{
    public PostSalesReceiptCommand(PostSalesReceiptRequest request) => Request = request ?? throw new ArgumentNullException(nameof(request));
    public PostSalesReceiptRequest Request { get; }
    public override string PermissionKey => PermissionKeys.SalesReceiptsPost;
    public SecurityAuditAction AuditAction => SecurityAuditAction.SalesReceiptPost;
    public string AuditDocumentType => "SalesReceipt";
    public string AuditDocumentId => Request.SaleId.ToString(CultureInfo.InvariantCulture);
}

internal sealed class PostSalesReceiptCommandValidator : AbstractValidator<PostSalesReceiptCommand>
{
    public PostSalesReceiptCommandValidator()
    {
        RuleFor(command => command.Request.SaleId).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
        RuleFor(command => command.Request.ExpectedVersion).Must(SalesReceiptVersion.IsValidToken).WithErrorCode("Validation.InvalidVersion");
    }
}

internal sealed class PostSalesReceiptCommandHandler : IRequestHandler<PostSalesReceiptCommand, ErrorOr<PostSalesReceiptResponse>>
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUtcClock _clock;
    private readonly ISecurityAuditWriter _audit;
    public PostSalesReceiptCommandHandler(IUnitOfWorkFactory factory, IUtcClock clock, ISecurityAuditWriter audit)
    { _factory = factory; _clock = clock; _audit = audit; }

    public async Task<ErrorOr<PostSalesReceiptResponse>> Handle(PostSalesReceiptCommand command, CancellationToken token)
    {
        using var uow = await _factory.CreateAsync(token).ConfigureAwait(false);
        uow.BeginTransaction();
        var repository = uow.Repository<ISalesReceiptWriteRepository>();
        await repository.AcquirePostingGateAsync(token).ConfigureAwait(false);
        var header = await repository.LockHeaderAsync(command.Request.SaleId, token).ConfigureAwait(false);
        if (header == null) return Error.NotFound("SalesReceipt.NotFound", "The sales receipt was not found.");
        if (header.Status == SalesReceiptStatuses.Posted) return Error.Conflict("SalesReceipt.AlreadyPosted", "The sales receipt is already posted.");
        var expected = Convert.FromBase64String(command.Request.ExpectedVersion);
        if (!header.Version.SequenceEqual(expected)) return Conflict();
        var snapshot = await repository.LockPostSnapshotAsync(header, token).ConfigureAwait(false);
        var lines = snapshot.Lines;
        if (header.Status != SalesReceiptStatuses.Draft || !SalesReceiptValidation.CanonicalLines(header.SaleId, lines))
            return SalesReceiptValidation.Field("SalesReceipt.InvalidStoredLines", "The stored Draft lines are invalid.", "Request.Lines");
        var requests = lines.Select(line => new SaveSalesReceiptLineRequest(line.SaleLineId, line.ProductId, line.Qty, line.UnitPrice, line.IsGift)).ToArray();
        var errors = SalesReceiptValidation.References(snapshot.References, requests);
        if (errors.Count > 0) return ErrorOrFactory.From<PostSalesReceiptResponse>(errors);
        for (var index = 0; index < lines.Count; index++)
            if (!string.Equals(lines[index].Unit, snapshot.References.Products[lines[index].ProductId].Unit, StringComparison.Ordinal))
                return SalesReceiptValidation.Field("SalesReceipt.UnitChanged", "Product Unit changed. Reload, review and save the Draft before posting.", $"Request.Lines[{index}].ProductId");

        var balances = await repository.LockValuationBalancesAsync(header.WarehouseId, lines.Select(line => line.ProductId).ToArray(), token).ConfigureAwait(false);
        var costs = new Dictionary<int, InventoryIssueCost>();
        try
        {
            foreach (var line in lines)
                if (line.LineAmount != InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift))
                    return SalesReceiptValidation.Field("SalesReceipt.InvalidStoredAmount", "The stored line amount is invalid.", "Request.Lines");
            var revenue = InventoryValuationMath.SumAmounts(lines.Select(line => line.LineAmount));
            foreach (var group in lines.GroupBy(line => line.ProductId).OrderBy(group => group.Key))
            {
                var state = balances[group.Key];
                var demand = InventoryValuationMath.SumQuantities(group.Select(line => line.Qty));
                if (state.Quantity <= 0 || demand > state.Quantity)
                    return SalesReceiptValidation.Field("SalesReceipt.InsufficientStock", "Available stock does not cover all lines for a Product.", "Request.Lines");
                var cost = InventoryValuationMath.CalculateIssue(state.Quantity, state.InventoryValue,
                    group.Select(line => new InventoryIssueLine(line.LineNo, line.Qty)).ToArray());
                _ = new StockValuationState(state.Quantity - cost.IssueQuantity, state.InventoryValue - cost.CostAmount);
                costs.Add(group.Key, cost);
            }
            var totalCost = InventoryValuationMath.SumAmounts(costs.Values.Select(cost => cost.CostAmount));
            InventoryValuationMath.SumSignedAmounts(new[] { revenue, -totalCost });
        }
        catch (Exception error) when (error is ArgumentException || error is OverflowException)
        {
            return SalesReceiptValidation.Field("SalesReceipt.ValuationOutOfRange", "Sales value, quantity or cost exceeds the supported range.", "Request.Lines");
        }
        var stamp = _clock.UtcNow;
        var posted = await repository.TryMarkPostedAsync(header.SaleId, expected, stamp, command.AuthorizedUserId, token).ConfigureAwait(false);
        if (posted == null) return Conflict();
        await repository.ApplyPostedInventoryAsync(posted, lines, costs, stamp, command.AuthorizedUserId, token).ConfigureAwait(false);
        if (posted.SaleId != header.SaleId || posted.Status != SalesReceiptStatuses.Posted ||
            !posted.PostedAtUtc.HasValue || posted.Version == null || posted.Version.Length != 8)
            throw new InvalidOperationException("The authoritative posted header is incomplete.");
        var response = new PostSalesReceiptResponse(posted.SaleId, posted.Status, posted.PostedAtUtc!.Value, Convert.ToBase64String(posted.Version));
        uow.Commit();
        await _audit.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.SalesReceiptPost, SecurityAuditOutcome.Succeeded,
            command.AuthorizedUserId, stamp, "SalesReceipt", posted.SaleId.ToString(CultureInfo.InvariantCulture)),
            businessOperationCommitted: true, CancellationToken.None).ConfigureAwait(false);
        return response;
    }
    private static Error Conflict() => Error.Conflict("SalesReceipt.ConcurrencyConflict", "The sales receipt was changed by another operation.");
}
