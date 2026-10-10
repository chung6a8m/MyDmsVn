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
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Inventory;

namespace MyDmsVn.Server.Application.Inventory;

public sealed class CreateGoodsReceiptDraftCommand : AuthorizedActorApplicationRequest<GoodsReceiptDto>
{
    public CreateGoodsReceiptDraftCommand(SaveGoodsReceiptRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public SaveGoodsReceiptRequest Request { get; }

    public override string PermissionKey => PermissionKeys.InventoryGoodsReceiptsWrite;
}

public sealed class UpdateGoodsReceiptDraftCommand : AuthorizedActorApplicationRequest<GoodsReceiptDto>
{
    public UpdateGoodsReceiptDraftCommand(UpdateGoodsReceiptDraftRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public UpdateGoodsReceiptDraftRequest Request { get; }

    public override string PermissionKey => PermissionKeys.InventoryGoodsReceiptsWrite;
}

public sealed class GetGoodsReceiptByIdQuery : ApplicationRequest<GoodsReceiptDto>, IAuthorizedRequest
{
    public GetGoodsReceiptByIdQuery(long id) => Id = id;

    public long Id { get; }

    public string PermissionKey => PermissionKeys.InventoryGoodsReceiptsRead;
}

public sealed class ListGoodsReceiptsQuery
    : ApplicationRequest<PagedResult<GoodsReceiptDto>>, IAuthorizedRequest
{
    public ListGoodsReceiptsQuery(GoodsReceiptListRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public GoodsReceiptListRequest Request { get; }

    public string PermissionKey => PermissionKeys.InventoryGoodsReceiptsRead;
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
            if (!seen.Add(lines[index].ProductId))
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
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(line => line.UnitCost)
            .GreaterThanOrEqualTo(0m)
            .WithErrorCode("Validation.GreaterThanOrEqual");
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
                    .WithErrorCode("Validation.Required");
                RuleForEach(request => request.Lines)
                    .SetValidator(new SaveGoodsReceiptLineRequestValidator());
                RuleFor(request => request).Custom((request, context) =>
                {
                    var seen = new HashSet<int>();
                    for (var index = 0; index < request.Lines.Count; index++)
                    {
                        if (!seen.Add(request.Lines[index].ProductId))
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
        unitOfWork.Commit();
        return response;
    }
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
