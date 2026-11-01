using MyDmsVn.Server.Application.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;

namespace MyDmsVn.Server.Application.Sales;

public sealed class CreateSalesReceiptDraftCommand : AuthorizedActorApplicationRequest<SalesReceiptDto>
{
    public CreateSalesReceiptDraftCommand(SaveSalesReceiptRequest request, WebOperationContext? operation = null)
    { Request = request ?? throw new ArgumentNullException(nameof(request)); Operation=operation; }
    public WebOperationContext? Operation { get; }
    public SaveSalesReceiptRequest Request { get; }
    public override string PermissionKey => PermissionKeys.SalesReceiptsWrite;
}

public sealed class UpdateSalesReceiptDraftCommand : AuthorizedActorApplicationRequest<SalesReceiptDto>
{
    public UpdateSalesReceiptDraftCommand(UpdateSalesReceiptDraftRequest request, WebOperationContext? operation = null)
    { Request = request ?? throw new ArgumentNullException(nameof(request)); Operation=operation; }
    public WebOperationContext? Operation { get; }
    public UpdateSalesReceiptDraftRequest Request { get; }
    public override string PermissionKey => PermissionKeys.SalesReceiptsWrite;
}

internal sealed class CreateSalesReceiptDraftCommandValidator : AbstractValidator<CreateSalesReceiptDraftCommand>
{
    public CreateSalesReceiptDraftCommandValidator() => RuleFor(command => command).Custom((command, context) =>
    {
        var body = command.Request;
        foreach (var error in SalesReceiptValidation.Body(body.SaleDate, body.CustomerId, body.WarehouseId,
            body.EmployeeId, body.Note, body.Lines, create: true)) context.AddFailure(error);
    });
}

internal sealed class UpdateSalesReceiptDraftCommandValidator : AbstractValidator<UpdateSalesReceiptDraftCommand>
{
    public UpdateSalesReceiptDraftCommandValidator()
    {
        RuleFor(command => command.Request.SaleId).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
        RuleFor(command => command.Request.ExpectedVersion).Must(SalesReceiptVersion.IsValidToken).WithErrorCode("Validation.InvalidVersion");
        RuleFor(command => command).Custom((command, context) =>
        {
            var body = command.Request;
            foreach (var error in SalesReceiptValidation.Body(body.SaleDate, body.CustomerId, body.WarehouseId,
                body.EmployeeId, body.Note, body.Lines, create: false)) context.AddFailure(error);
        });
    }
}

internal static class SalesReceiptVersion
{
    public static bool IsValidToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return Convert.FromBase64String(value).Length == 8; }
        catch (FormatException) { return false; }
    }
}

internal static class SalesReceiptValidation
{
    public static List<ValidationFailure> Body(DateTime date, int customer, int warehouse, int employee, string? note,
        IReadOnlyList<SaveSalesReceiptLineRequest> lines, bool create)
    {
        var errors = new List<ValidationFailure>();
        void Add(string field, string code, string message) => errors.Add(new ValidationFailure("Request." + field, message) { ErrorCode = code });
        if (date == default) Add("SaleDate", "Validation.Required", "Sale date is required.");
        if (customer <= 0) Add("CustomerId", "Validation.GreaterThan", "Customer is required.");
        if (warehouse <= 0) Add("WarehouseId", "Validation.GreaterThan", "Warehouse is required.");
        if (employee <= 0) Add("EmployeeId", "Validation.GreaterThan", "Employee is required.");
        if (note?.Length > SalesContractLimits.MaximumNoteLength) Add("Note", "Validation.MaximumLength", "Note must not exceed 1000 characters.");
        if (note?.IndexOf('\0') >= 0) Add("Note", "Validation.InvalidCharacter", "Note cannot contain NUL.");
        if (lines.Count == 0) Add("Lines", "Validation.Required", "At least one line is required.");
        if (lines.Count > SalesContractLimits.MaximumLines)
        {
            Add("Lines", "Validation.MaximumLength", "At most 200 lines are supported.");
            return errors;
        }
        var ids = new HashSet<long>();
        var amounts = new List<decimal>();
        var validNumbers = true;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var field = "Lines[" + index + "]";
            if (line == null) { Add(field, "Validation.Required", "Line is required."); validNumbers = false; continue; }
            if (line.ProductId <= 0) Add(field + ".ProductId", "Validation.GreaterThan", "Product is required.");
            if (line.SaleLineId < 0 || (create && line.SaleLineId != 0)) Add(field + ".SaleLineId", "SalesReceipt.InvalidLineIdentity", "New lines require identity zero.");
            if (line.SaleLineId > 0 && !ids.Add(line.SaleLineId)) Add(field + ".SaleLineId", "SalesReceipt.DuplicateLineIdentity", "A saved line may appear only once.");
            var validQty = line.Qty > 0 && line.Qty <= SalesContractLimits.MaximumQuantity && decimal.Round(line.Qty, 4) == line.Qty;
            var validPrice = line.UnitPrice >= 0 && line.UnitPrice <= SalesContractLimits.MaximumUnitPrice && decimal.Round(line.UnitPrice, 4) == line.UnitPrice;
            if (!validQty) Add(field + ".Qty", "Validation.PrecisionScale", "Quantity must be positive and fit decimal(18,4).");
            if (!validPrice) Add(field + ".UnitPrice", "Validation.PrecisionScale", "Price must be nonnegative and fit decimal(19,4).");
            if (!validQty || !validPrice) { validNumbers = false; continue; }
            try { amounts.Add(InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift)); }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Add(field, "SalesReceipt.AmountOutOfRange", "Line amount exceeds the supported range."); validNumbers = false; }
        }
        if (validNumbers)
        {
            try
            {
                InventoryValuationMath.SumAmounts(amounts);
                foreach (var group in lines.GroupBy(line => line.ProductId)) InventoryValuationMath.SumQuantities(group.Select(line => line.Qty));
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Add("Lines", "SalesReceipt.AggregateOutOfRange", "Document amount or Product quantity exceeds the supported range."); }
        }
        return errors;
    }

    public static List<Error> References(SalesReceiptReferenceSnapshot references, IReadOnlyList<SaveSalesReceiptLineRequest> lines)
    {
        var errors = new List<Error>();
        void Add(CatalogReferenceState state, string name, string field)
        {
            if (state != CatalogReferenceState.Active) errors.Add(Field("SalesReceipt." + name + (state == CatalogReferenceState.Missing ? "NotFound" : "Inactive"), "The required reference must exist and be active.", field));
        }
        Add(references.Customer.State, "Customer", "Request.CustomerId");
        Add(references.Warehouse.State, "Warehouse", "Request.WarehouseId");
        Add(references.Employee.State, "Employee", "Request.EmployeeId");
        for (var index = 0; index < lines.Count; index++)
        {
            references.Products.TryGetValue(lines[index].ProductId, out var product);
            Add(product?.State ?? CatalogReferenceState.Missing, "Product", $"Request.Lines[{index}].ProductId");
            if (product?.State == CatalogReferenceState.Active && !ValidUnit(product.Unit))
                errors.Add(Field("SalesReceipt.InvalidUnit", "Product Unit is invalid.", $"Request.Lines[{index}].ProductId"));
        }
        return errors;
    }

    public static bool ValidUnit(string? unit) => !string.IsNullOrWhiteSpace(unit) && unit!.Length <= 32 && unit.IndexOf('\0') < 0;
    public static Error Field(string code, string message, string field) => Error.Validation(code, message, new Dictionary<string, object> { ["Field"] = field });
    public static bool CanonicalLines(long saleId, IReadOnlyList<SalesReceiptLine> lines) =>
        lines.Count >= 1 && lines.Count <= SalesContractLimits.MaximumLines && lines.Select(line => line.SaleLineId).Distinct().Count() == lines.Count &&
        lines.Select((line, index) => line.SaleId == saleId && line.SaleLineId > 0 && line.LineNo == index + 1 && !line.PostedUnitCost.HasValue && !line.CostAmount.HasValue).All(valid => valid);
}

internal sealed class CreateSalesReceiptDraftCommandHandler : IRequestHandler<CreateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>
{
    private readonly IUnitOfWorkFactory _factory;
    public CreateSalesReceiptDraftCommandHandler(IUnitOfWorkFactory factory) => _factory = factory;
    public async Task<ErrorOr<SalesReceiptDto>> Handle(CreateSalesReceiptDraftCommand command, CancellationToken cancellationToken)
    {
        var body = command.Request;
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        var replay = await WebOperationExecution.ReplayAsync<SalesReceiptDto>(uow, command.Operation, command.AuthorizedUserId, WebOperationKind.SalesCreate, null, cancellationToken).ConfigureAwait(false);
        if (replay != null) { uow.Commit(); return replay; }
        var repository = uow.Repository<ISalesReceiptWriteRepository>();
        var references = await repository.GetReferenceSnapshotAsync(body.CustomerId, body.WarehouseId, body.EmployeeId,
            body.Lines.Select(line => line.ProductId).Distinct().ToArray(), cancellationToken).ConfigureAwait(false);
        var errors = SalesReceiptValidation.References(references, body.Lines);
        if (errors.Count > 0) return ErrorOrFactory.From<SalesReceiptDto>(errors);
        var header = new SalesReceipt
        {
            SaleDate = body.SaleDate,
            CustomerId = body.CustomerId,
            WarehouseId = body.WarehouseId,
            EmployeeId = body.EmployeeId,
            Note = body.Note,
            CreatedByUserId = command.AuthorizedUserId
        };
        var lines = CreateLines(body.Lines, references, command.AuthorizedUserId);
        var persisted = await repository.InsertDraftAsync(header, lines, cancellationToken).ConfigureAwait(false);
        var result = ToDraftDto(persisted, lines, references);
        await WebOperationExecution.CompleteAsync(uow, command.Operation, command.AuthorizedUserId, result.SaleId, result, cancellationToken).ConfigureAwait(false);
        uow.Commit();
        return result;
    }

    internal static List<SalesReceiptLine> CreateLines(IReadOnlyList<SaveSalesReceiptLineRequest> body, SalesReceiptReferenceSnapshot references, int actor) =>
        body.Select((line, index) => new SalesReceiptLine
        {
            SaleLineId = line.SaleLineId,
            LineNo = index + 1,
            ProductId = line.ProductId,
            Unit = references.Products[line.ProductId].Unit!,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            IsGift = line.IsGift,
            LineAmount = InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift),
            CreatedByUserId = actor
        }).ToList();

    internal static SalesReceiptDto ToDraftDto(SalesReceipt header, IReadOnlyList<SalesReceiptLine> lines, SalesReceiptReferenceSnapshot references) =>
        new SalesReceiptDto(header.SaleId, header.SaleNumber, header.SaleDate, header.CustomerId, header.WarehouseId, header.EmployeeId,
            header.Status, header.Note, null, Convert.ToBase64String(header.Version), lines.Select(line => new SalesReceiptLineDto(
                line.SaleLineId, line.LineNo, line.ProductId, line.Unit, line.Qty, line.UnitPrice, line.IsGift, line.LineAmount, null, null,
                references.Products[line.ProductId].DisplayName, references.Products[line.ProductId].State == CatalogReferenceState.Active)).ToArray(),
            InventoryValuationMath.SumAmounts(lines.Select(line => line.LineAmount)), null, null,
            references.Customer.DisplayName, references.Customer.State == CatalogReferenceState.Active,
            references.Warehouse.DisplayName, references.Warehouse.State == CatalogReferenceState.Active,
            references.Employee.DisplayName, references.Employee.State == CatalogReferenceState.Active);
}

internal sealed class UpdateSalesReceiptDraftCommandHandler : IRequestHandler<UpdateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUtcClock _clock;
    public UpdateSalesReceiptDraftCommandHandler(IUnitOfWorkFactory factory, IUtcClock clock) { _factory = factory; _clock = clock; }
    public async Task<ErrorOr<SalesReceiptDto>> Handle(UpdateSalesReceiptDraftCommand command, CancellationToken cancellationToken)
    {
        var body = command.Request;
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        var replay = await WebOperationExecution.ReplayAsync<SalesReceiptDto>(uow, command.Operation, command.AuthorizedUserId, WebOperationKind.SalesUpdate, command.Request.SaleId, cancellationToken).ConfigureAwait(false);
        if (replay != null) { uow.Commit(); return replay; }
        var repository = uow.Repository<ISalesReceiptWriteRepository>();
        var header = await repository.LockHeaderAsync(body.SaleId, cancellationToken).ConfigureAwait(false);
        if (header == null) return Error.NotFound("SalesReceipt.NotFound", "The sales receipt was not found.");
        if (header.Status == SalesReceiptStatuses.Posted) return Error.Conflict("SalesReceipt.AlreadyPosted", "The sales receipt is already posted.");
        if (!header.Version.SequenceEqual(Convert.FromBase64String(body.ExpectedVersion)))
            return Error.Conflict("SalesReceipt.ConcurrencyConflict", "The sales receipt was changed by another operation.");
        var stored = await repository.LoadLinesAsync(header.SaleId, cancellationToken).ConfigureAwait(false);
        if (header.Status != SalesReceiptStatuses.Draft || !SalesReceiptValidation.CanonicalLines(header.SaleId, stored))
            return SalesReceiptValidation.Field("SalesReceipt.InvalidStoredLines", "The stored Draft lines are invalid.", "Request.Lines");
        var byId = stored.ToDictionary(line => line.SaleLineId);
        var errors = new List<Error>();
        for (var index = 0; index < body.Lines.Count; index++)
            if (body.Lines[index].SaleLineId > 0 && !byId.ContainsKey(body.Lines[index].SaleLineId))
                errors.Add(SalesReceiptValidation.Field("SalesReceipt.InvalidLineOwnership", "The saved line does not belong to this Draft.", $"Request.Lines[{index}].SaleLineId"));
        if (errors.Count > 0) return ErrorOrFactory.From<SalesReceiptDto>(errors);
        var references = await repository.GetReferenceSnapshotAsync(body.CustomerId, body.WarehouseId, body.EmployeeId,
            body.Lines.Select(line => line.ProductId).Distinct().ToArray(), cancellationToken).ConfigureAwait(false);
        errors = SalesReceiptValidation.References(references, body.Lines);
        if (errors.Count > 0) return ErrorOrFactory.From<SalesReceiptDto>(errors);
        var stamp = _clock.UtcNow;
        var lines = CreateSalesReceiptDraftCommandHandler.CreateLines(body.Lines, references, command.AuthorizedUserId);
        foreach (var line in lines)
        {
            line.SaleId = header.SaleId;
            if (line.SaleLineId == 0) continue;
            var previous = byId[line.SaleLineId];
            line.CreatedAtUtc = previous.CreatedAtUtc; line.CreatedByUserId = previous.CreatedByUserId;
            line.UpdatedAtUtc = stamp; line.UpdatedByUserId = command.AuthorizedUserId;
        }
        header.SaleDate = body.SaleDate; header.CustomerId = body.CustomerId; header.WarehouseId = body.WarehouseId;
        header.EmployeeId = body.EmployeeId; header.Note = body.Note; header.UpdatedAtUtc = stamp; header.UpdatedByUserId = command.AuthorizedUserId;
        var persisted = await repository.ReplaceDraftAsync(header, lines, cancellationToken).ConfigureAwait(false);
        var result = CreateSalesReceiptDraftCommandHandler.ToDraftDto(persisted, lines, references);
        await WebOperationExecution.CompleteAsync(uow, command.Operation, command.AuthorizedUserId, result.SaleId, result, cancellationToken).ConfigureAwait(false);
        uow.Commit();
        return result;
    }
}
