using MyDmsVn.Server.Application.Operations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Inventory;

namespace MyDmsVn.Server.Application.Inventory;

public sealed class CreateGoodsReceiptDraftCommand : AuthorizedActorApplicationRequest<GoodsReceiptDto>
{
    public CreateGoodsReceiptDraftCommand(SaveGoodsReceiptRequest request, WebOperationContext? operation = null)
    { Request = request ?? throw new ArgumentNullException(nameof(request)); Operation=operation; }
    public WebOperationContext? Operation { get; }

    public SaveGoodsReceiptRequest Request { get; }

    public override string PermissionKey => PermissionKeys.InventoryGoodsReceiptsWrite;
}

public sealed class UpdateGoodsReceiptDraftCommand : AuthorizedActorApplicationRequest<GoodsReceiptDto>
{
    public UpdateGoodsReceiptDraftCommand(UpdateGoodsReceiptDraftRequest request, WebOperationContext? operation = null)
    { Request = request ?? throw new ArgumentNullException(nameof(request)); Operation=operation; }
    public WebOperationContext? Operation { get; }

    public UpdateGoodsReceiptDraftRequest Request { get; }

    public override string PermissionKey => PermissionKeys.InventoryGoodsReceiptsWrite;
}

public sealed class PostGoodsReceiptCommand
    : AuthorizedActorApplicationRequest<PostGoodsReceiptResponse>, ISecurityAuditedRequest
{
    public PostGoodsReceiptCommand(PostGoodsReceiptRequest request, WebOperationContext? operation = null)
    { Request = request ?? throw new ArgumentNullException(nameof(request)); Operation=operation; }
    public WebOperationContext? Operation { get; }

    public PostGoodsReceiptRequest Request { get; }

    public override string PermissionKey => PermissionKeys.InventoryGoodsReceiptsPost;

    public SecurityAuditAction AuditAction => SecurityAuditAction.GoodsReceiptPost;
    public string AuditDocumentType => "GoodsReceipt";
    public string AuditDocumentId => Request.ReceiptId.ToString(CultureInfo.InvariantCulture);
}

internal sealed class SaveGoodsReceiptRequestValidator : AbstractValidator<SaveGoodsReceiptRequest>
{
    public SaveGoodsReceiptRequestValidator()
    {
        RuleFor(request => request.ReceiptDate)
            .NotEqual(default(DateTime))
            .WithErrorCode("Validation.Required");
        RuleFor(request => request.WarehouseId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(request => request.EmployeeId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(request => request.Note)
            .MaximumLength(1000)
            .WithErrorCode("Validation.MaximumLength")
            .Must(value => value == null || value.IndexOf('\0') < 0)
            .WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Lines)
            .NotEmpty()
            .WithErrorCode("Validation.Required")
            .Must(lines => lines.Count <= GoodsReceiptValidation.MaximumLinesPerReceipt)
            .WithErrorCode("Validation.MaximumLength");
        RuleForEach(request => request.Lines)
            .NotNull()
            .WithErrorCode("Validation.Required");
        RuleForEach(request => request.Lines)
            .SetValidator(new SaveGoodsReceiptLineRequestValidator());
        RuleFor(request => request).Custom(
            (request, context) => AddDuplicateProductFailures(request.Lines, context));
    }

    private static void AddDuplicateProductFailures(
        IReadOnlyList<SaveGoodsReceiptLineRequest> lines,
        ValidationContext<SaveGoodsReceiptRequest> context)
    {
        var seen = new HashSet<int>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line != null && !seen.Add(line.ProductId))
            {
                context.AddFailure(new ValidationFailure(
                    $"Request.Lines[{index}].ProductId",
                    "A product can appear only once in a goods receipt.")
                {
                    ErrorCode = "GoodsReceipt.DuplicateProduct",
                });
            }
        }
    }
}

internal sealed class SaveGoodsReceiptLineRequestValidator
    : AbstractValidator<SaveGoodsReceiptLineRequest>
{
    public SaveGoodsReceiptLineRequestValidator()
    {
        RuleFor(line => line.ProductId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(line => line.Quantity)
            .GreaterThan(0m)
            .WithErrorCode("Validation.GreaterThan")
            .LessThanOrEqualTo(GoodsReceiptValidation.MaximumQuantity)
            .WithErrorCode("Validation.PrecisionScale")
            .Must(GoodsReceiptValidation.HasAtMostFourFractionalDigits)
            .WithErrorCode("Validation.PrecisionScale");
        RuleFor(line => line.UnitCost)
            .GreaterThanOrEqualTo(0m)
            .WithErrorCode("Validation.GreaterThanOrEqual")
            .LessThanOrEqualTo(GoodsReceiptValidation.MaximumUnitCost)
            .WithErrorCode("Validation.PrecisionScale")
            .Must(GoodsReceiptValidation.HasAtMostFourFractionalDigits)
            .WithErrorCode("Validation.PrecisionScale");
    }
}

internal sealed class CreateGoodsReceiptDraftCommandValidator
    : AbstractValidator<CreateGoodsReceiptDraftCommand>
{
    public CreateGoodsReceiptDraftCommandValidator() =>
        RuleFor(command => command.Request).SetValidator(new SaveGoodsReceiptRequestValidator());
}

internal sealed class UpdateGoodsReceiptDraftCommandValidator
    : AbstractValidator<UpdateGoodsReceiptDraftCommand>
{
    public UpdateGoodsReceiptDraftCommandValidator()
    {
        RuleFor(command => command.Request.ReceiptId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(command => command.Request.ExpectedVersion)
            .Must(GoodsReceiptVersion.IsValidToken)
            .WithErrorCode("Validation.InvalidVersion");
        RuleFor(command => command.Request).SetValidator(new UpdateDraftBodyValidator());
    }

    private sealed class UpdateDraftBodyValidator : AbstractValidator<UpdateGoodsReceiptDraftRequest>
    {
        public UpdateDraftBodyValidator()
        {
            Include(new SaveShapeValidator());
        }

        private sealed class SaveShapeValidator : AbstractValidator<UpdateGoodsReceiptDraftRequest>
        {
            public SaveShapeValidator()
            {
                RuleFor(request => request.ReceiptDate)
                    .NotEqual(default(DateTime))
                    .WithErrorCode("Validation.Required");
                RuleFor(request => request.WarehouseId)
                    .GreaterThan(0)
                    .WithErrorCode("Validation.GreaterThan");
                RuleFor(request => request.EmployeeId)
                    .GreaterThan(0)
                    .WithErrorCode("Validation.GreaterThan");
                RuleFor(request => request.Note)
                    .MaximumLength(1000)
                    .WithErrorCode("Validation.MaximumLength")
                    .Must(value => value == null || value.IndexOf('\0') < 0)
                    .WithErrorCode("Validation.InvalidCharacter");
                RuleFor(request => request.Lines)
                    .NotEmpty()
                    .WithErrorCode("Validation.Required")
                    .Must(lines => lines.Count <= GoodsReceiptValidation.MaximumLinesPerReceipt)
                    .WithErrorCode("Validation.MaximumLength");
                RuleForEach(request => request.Lines)
                    .NotNull()
                    .WithErrorCode("Validation.Required");
                RuleForEach(request => request.Lines)
                    .SetValidator(new SaveGoodsReceiptLineRequestValidator());
                RuleFor(request => request).Custom((request, context) =>
                {
                    var seen = new HashSet<int>();
                    for (var index = 0; index < request.Lines.Count; index++)
                    {
                        var line = request.Lines[index];
                        if (line != null && !seen.Add(line.ProductId))
                        {
                            context.AddFailure(new ValidationFailure(
                                $"Request.Lines[{index}].ProductId",
                                "A product can appear only once in a goods receipt.")
                            {
                                ErrorCode = "GoodsReceipt.DuplicateProduct",
                            });
                        }
                    }
                });
            }
        }
    }
}

internal sealed class PostGoodsReceiptCommandValidator
    : AbstractValidator<PostGoodsReceiptCommand>
{
    public PostGoodsReceiptCommandValidator()
    {
        RuleFor(command => command.Request.ReceiptId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(command => command.Request.ExpectedVersion)
            .Must(GoodsReceiptVersion.IsValidToken)
            .WithErrorCode("Validation.InvalidVersion");
    }
}

internal static class GoodsReceiptValidation
{
    public const int MaximumLinesPerReceipt = 200;
    public const decimal MaximumQuantity = GoodsReceiptContractLimits.MaximumQuantity;
    public const decimal MaximumUnitCost = GoodsReceiptContractLimits.MaximumUnitCost;

    public static bool HasAtMostFourFractionalDigits(decimal value) =>
        decimal.Round(value, GoodsReceiptContractLimits.DecimalPlaces) == value;
}

internal static class GoodsReceiptVersion
{
    public static bool IsValidToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed class CreateGoodsReceiptDraftCommandHandler
    : IRequestHandler<CreateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public CreateGoodsReceiptDraftCommandHandler(IUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    public async Task<ErrorOr<GoodsReceiptDto>> Handle(
        CreateGoodsReceiptDraftCommand command,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        var replay = await WebOperationExecution.ReplayAsync<GoodsReceiptDto>(unitOfWork, command.Operation, command.AuthorizedUserId, WebOperationKind.ReceiptCreate, null, cancellationToken).ConfigureAwait(false);
        if (replay != null) { unitOfWork.Commit(); return replay; }
        var repository = unitOfWork.Repository<IGoodsReceiptWriteRepository>();
        var referenceSnapshot = await repository.GetReferenceSnapshotAsync(
            command.Request.WarehouseId,
            command.Request.EmployeeId,
            command.Request.Lines.Select(line => line.ProductId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        var referenceErrors = GoodsReceiptReferenceErrors.Create(referenceSnapshot, command.Request.Lines);
        if (referenceErrors.Count > 0)
        {
            return ErrorOrFactory.From<GoodsReceiptDto>(referenceErrors);
        }

        var header = CreateHeader(command.Request, command.AuthorizedUserId);
        var lines = CreateLines(command.Request.Lines, command.AuthorizedUserId);
        var persisted = await repository.InsertDraftAsync(header, lines, cancellationToken).ConfigureAwait(false);
        var response = ToDto(persisted, lines);
        await WebOperationExecution.CompleteAsync(unitOfWork, command.Operation, command.AuthorizedUserId, response.Id, response, cancellationToken).ConfigureAwait(false);
        unitOfWork.Commit();
        return response;
    }

    private static GoodsReceipt CreateHeader(SaveGoodsReceiptRequest request, int userId) =>
        new GoodsReceipt
        {
            ReceiptDate = request.ReceiptDate.Date,
            WarehouseId = request.WarehouseId,
            EmployeeId = request.EmployeeId,
            Status = GoodsReceiptStatuses.Draft,
            Note = request.Note,
            CreatedByUserId = userId,
        };

    internal static List<GoodsReceiptLine> CreateLines(
        IReadOnlyList<SaveGoodsReceiptLineRequest> requests,
        int userId)
    {
        return requests.Select((line, index) => new GoodsReceiptLine
        {
            LineNumber = index + 1,
            ProductId = line.ProductId,
            Quantity = line.Quantity,
            UnitCost = line.UnitCost,
            CreatedByUserId = userId,
        }).ToList();
    }

    internal static GoodsReceiptDto ToDto(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines)
    {
        return new GoodsReceiptDto(
            header.Id,
            header.ReceiptNumber,
            header.ReceiptDate,
            header.WarehouseId,
            header.EmployeeId,
            header.Status,
            header.Note,
            header.PostedAtUtc,
            Convert.ToBase64String(header.Version),
            lines.Select(line => new GoodsReceiptLineDto(
                line.Id,
                line.LineNumber,
                line.ProductId,
                line.Quantity,
                line.UnitCost)));
    }
}

internal sealed class UpdateGoodsReceiptDraftCommandHandler
    : IRequestHandler<UpdateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IUtcClock _clock;

    public UpdateGoodsReceiptDraftCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        IUtcClock clock)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _clock = clock;
    }

    public async Task<ErrorOr<GoodsReceiptDto>> Handle(
        UpdateGoodsReceiptDraftCommand command,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        var replay = await WebOperationExecution.ReplayAsync<GoodsReceiptDto>(unitOfWork, command.Operation, command.AuthorizedUserId, WebOperationKind.ReceiptUpdate, command.Request.ReceiptId, cancellationToken).ConfigureAwait(false);
        if (replay != null) { unitOfWork.Commit(); return replay; }
        var repository = unitOfWork.Repository<IGoodsReceiptWriteRepository>();

        var locked = await repository.LockHeaderAsync(
            command.Request.ReceiptId,
            cancellationToken).ConfigureAwait(false);
        if (locked == null)
        {
            return Error.NotFound("GoodsReceipt.NotFound", "The goods receipt was not found.");
        }

        if (string.Equals(locked.Status, GoodsReceiptStatuses.Posted, StringComparison.Ordinal))
        {
            return Error.Conflict("GoodsReceipt.AlreadyPosted", "The goods receipt is already posted.");
        }

        var expectedVersion = Convert.FromBase64String(command.Request.ExpectedVersion);
        if (!locked.Version.SequenceEqual(expectedVersion))
        {
            return Error.Conflict(
                "GoodsReceipt.ConcurrencyConflict",
                "The goods receipt was changed by another operation.");
        }

        var referenceSnapshot = await repository.GetReferenceSnapshotAsync(
            command.Request.WarehouseId,
            command.Request.EmployeeId,
            command.Request.Lines.Select(line => line.ProductId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        var referenceErrors = GoodsReceiptReferenceErrors.Create(referenceSnapshot, command.Request.Lines);
        if (referenceErrors.Count > 0)
        {
            return ErrorOrFactory.From<GoodsReceiptDto>(referenceErrors);
        }

        locked.ReceiptDate = command.Request.ReceiptDate.Date;
        locked.WarehouseId = command.Request.WarehouseId;
        locked.EmployeeId = command.Request.EmployeeId;
        locked.Note = command.Request.Note;
        locked.UpdatedAtUtc = _clock.UtcNow;
        locked.UpdatedByUserId = command.AuthorizedUserId;
        var lines = CreateGoodsReceiptDraftCommandHandler.CreateLines(
            command.Request.Lines,
            command.AuthorizedUserId);
        foreach (var line in lines)
        {
            line.ReceiptId = locked.Id;
        }

        var persisted = await repository.ReplaceDraftAsync(locked, lines, cancellationToken).ConfigureAwait(false);
        var response = CreateGoodsReceiptDraftCommandHandler.ToDto(persisted, lines);
        await WebOperationExecution.CompleteAsync(unitOfWork, command.Operation, command.AuthorizedUserId, response.Id, response, cancellationToken).ConfigureAwait(false);
        unitOfWork.Commit();
        return response;
    }
}

internal sealed class PostGoodsReceiptCommandHandler
    : IRequestHandler<PostGoodsReceiptCommand, ErrorOr<PostGoodsReceiptResponse>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IUtcClock _clock;
    private readonly ISecurityAuditWriter _auditWriter;

    public PostGoodsReceiptCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        IUtcClock clock,
        ISecurityAuditWriter auditWriter)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<ErrorOr<PostGoodsReceiptResponse>> Handle(
        PostGoodsReceiptCommand command,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        var replay = await WebOperationExecution.ReplayAsync<PostGoodsReceiptResponse>(unitOfWork, command.Operation, command.AuthorizedUserId, WebOperationKind.ReceiptPost, command.Request.ReceiptId, cancellationToken).ConfigureAwait(false);
        if (replay != null) { unitOfWork.Commit(); return replay; }
        var repository = unitOfWork.Repository<IGoodsReceiptWriteRepository>();
        await repository.AcquirePostingGateAsync(cancellationToken).ConfigureAwait(false);
        var header = await repository
            .LockHeaderAsync(command.Request.ReceiptId, cancellationToken)
            .ConfigureAwait(false);
        if (header == null)
        {
            return Error.NotFound("GoodsReceipt.NotFound", "The goods receipt was not found.");
        }

        if (string.Equals(header.Status, GoodsReceiptStatuses.Posted, StringComparison.Ordinal))
        {
            return Error.Conflict("GoodsReceipt.AlreadyPosted", "The goods receipt is already posted.");
        }

        var expectedVersion = Convert.FromBase64String(command.Request.ExpectedVersion);
        if (!header.Version.SequenceEqual(expectedVersion))
        {
            return ConcurrencyConflict();
        }

        var snapshot = await repository
            .LockPostSnapshotAsync(header, cancellationToken)
            .ConfigureAwait(false);
        var validationErrors = ValidateSnapshot(snapshot);
        if (validationErrors.Count > 0)
        {
            return ErrorOrFactory.From<PostGoodsReceiptResponse>(validationErrors);
        }

        var balances = await repository.LockValuationBalancesAsync(header.WarehouseId,
            snapshot.Lines.Select(line => line.ProductId).ToArray(), cancellationToken).ConfigureAwait(false);
        try
        {
            var values = snapshot.Lines.ToDictionary(line => line.Id,
                line => InventoryValuationMath.CalculateReceiptValue(line.Quantity, line.UnitCost));
            InventoryValuationMath.SumAmounts(values.Values);
            foreach (var group in snapshot.Lines.GroupBy(line => line.ProductId))
            {
                var balance = balances[group.Key];
                var quantity = InventoryValuationMath.SumQuantities(group.Select(line => line.Quantity).Prepend(balance.Quantity));
                var value = InventoryValuationMath.SumAmounts(group.Select(line => values[line.Id]).Prepend(balance.InventoryValue));
                _ = new StockValuationState(quantity, value);
            }
        }
        catch (Exception error) when (error is OverflowException || error is ArgumentException)
        {
            return Error.Validation("GoodsReceipt.ValuationOutOfRange", "Receipt value or resulting stock exceeds the supported range.");
        }

        var postedAtUtc = _clock.UtcNow;
        var marked = await repository.TryMarkPostedAsync(
            header.Id,
            expectedVersion,
            postedAtUtc,
            command.AuthorizedUserId,
            cancellationToken).ConfigureAwait(false);
        if (!marked)
        {
            return ConcurrencyConflict();
        }

        await repository.ApplyPostedInventoryAsync(header, snapshot.Lines, postedAtUtc,
            command.AuthorizedUserId, cancellationToken).ConfigureAwait(false);

        var response = new PostGoodsReceiptResponse(header.Id, GoodsReceiptStatuses.Posted, postedAtUtc);
        await WebOperationExecution.CompleteAsync(unitOfWork, command.Operation, command.AuthorizedUserId, response.ReceiptId, response, cancellationToken).ConfigureAwait(false);
        unitOfWork.Commit();
        await _auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditAction.GoodsReceiptPost,
                SecurityAuditOutcome.Succeeded,
                command.AuthorizedUserId,
                postedAtUtc,
                "GoodsReceipt",
                header.Id.ToString(CultureInfo.InvariantCulture)),
            businessOperationCommitted: true,
            CancellationToken.None).ConfigureAwait(false);
        return response;
    }

    private static List<Error> ValidateSnapshot(GoodsReceiptPostSnapshot snapshot)
    {
        var errors = GoodsReceiptReferenceErrors.Create(snapshot.References, snapshot.Lines);
        if (snapshot.Lines.Count == 0 || snapshot.Lines.Count > GoodsReceiptValidation.MaximumLinesPerReceipt)
        {
            errors.Add(PostValidationError(
                "GoodsReceipt.InvalidLineCount",
                "The goods receipt must contain between 1 and 200 lines.",
                "Request.Lines"));
        }

        var productIds = new HashSet<int>();
        for (var index = 0; index < snapshot.Lines.Count; index++)
        {
            var line = snapshot.Lines[index];
            if (line.Id <= 0 || line.ProductId <= 0)
            {
                errors.Add(PostValidationError(
                    "GoodsReceipt.InvalidLine",
                    "The goods receipt contains an invalid line identity.",
                    $"Request.Lines[{index}]"));
            }

            if (line.Quantity <= 0m ||
                line.Quantity > GoodsReceiptValidation.MaximumQuantity ||
                !GoodsReceiptValidation.HasAtMostFourFractionalDigits(line.Quantity))
            {
                errors.Add(PostValidationError(
                    "GoodsReceipt.InvalidQuantity",
                    "The goods receipt contains an invalid quantity.",
                    $"Request.Lines[{index}].Quantity"));
            }

            if (line.UnitCost < 0m ||
                line.UnitCost > GoodsReceiptValidation.MaximumUnitCost ||
                !GoodsReceiptValidation.HasAtMostFourFractionalDigits(line.UnitCost))
            {
                errors.Add(PostValidationError(
                    "GoodsReceipt.InvalidUnitCost",
                    "The goods receipt contains an invalid unit cost.",
                    $"Request.Lines[{index}].UnitCost"));
            }

            if (!productIds.Add(line.ProductId))
            {
                errors.Add(PostValidationError(
                    "GoodsReceipt.DuplicateProduct",
                    "A product can appear only once in a goods receipt.",
                    $"Request.Lines[{index}].ProductId"));
            }
        }

        return errors;
    }

    private static Error PostValidationError(string code, string description, string field) =>
        Error.Validation(
            code,
            description,
            new Dictionary<string, object> { ["Field"] = field });

    private static Error ConcurrencyConflict() =>
        Error.Conflict(
            "GoodsReceipt.ConcurrencyConflict",
            "The goods receipt was changed by another operation.");
}

internal static class GoodsReceiptReferenceErrors
{
    public static List<Error> Create(
        GoodsReceiptReferenceSnapshot snapshot,
        IReadOnlyList<SaveGoodsReceiptLineRequest> lines)
    {
        var errors = new List<Error>();
        AddReferenceError(errors, snapshot.Warehouse, "GoodsReceipt.Warehouse", "Request.WarehouseId");
        AddReferenceError(errors, snapshot.Employee, "GoodsReceipt.Employee", "Request.EmployeeId");
        for (var index = 0; index < lines.Count; index++)
        {
            var productId = lines[index].ProductId;
            var state = snapshot.Products.TryGetValue(productId, out var found)
                ? found
                : CatalogReferenceState.Missing;
            AddReferenceError(
                errors,
                state,
                "GoodsReceipt.Product",
                $"Request.Lines[{index}].ProductId");
        }

        return errors;
    }

    public static List<Error> Create(
        GoodsReceiptReferenceSnapshot snapshot,
        IReadOnlyList<GoodsReceiptLine> lines)
    {
        var errors = new List<Error>();
        AddReferenceError(errors, snapshot.Warehouse, "GoodsReceipt.Warehouse", "Request.WarehouseId");
        AddReferenceError(errors, snapshot.Employee, "GoodsReceipt.Employee", "Request.EmployeeId");
        for (var index = 0; index < lines.Count; index++)
        {
            var productId = lines[index].ProductId;
            var state = snapshot.Products.TryGetValue(productId, out var found)
                ? found
                : CatalogReferenceState.Missing;
            AddReferenceError(
                errors,
                state,
                "GoodsReceipt.Product",
                $"Request.Lines[{index}].ProductId");
        }

        return errors;
    }

    private static void AddReferenceError(
        ICollection<Error> errors,
        CatalogReferenceState state,
        string codePrefix,
        string field)
    {
        if (state == CatalogReferenceState.Active)
        {
            return;
        }

        var suffix = state == CatalogReferenceState.Missing ? "NotFound" : "Inactive";
        errors.Add(Error.Validation(
            codePrefix + suffix,
            state == CatalogReferenceState.Missing
                ? "The referenced catalog item was not found."
                : "The referenced catalog item is inactive.",
            new Dictionary<string, object> { ["Field"] = field }));
    }
}
