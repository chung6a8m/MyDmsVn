using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Application.Catalog;

public sealed class CreateProductCommand : ApplicationRequest<ProductDto>, IAuthorizedRequest
{
    public CreateProductCommand(SaveProductRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public SaveProductRequest Request { get; }

    public string PermissionKey => PermissionKeys.CatalogProductsWrite;
}

public sealed class ListProductsQuery : ApplicationRequest<PagedResult<ProductDto>>, IAuthorizedRequest
{
    public ListProductsQuery(CatalogListRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public CatalogListRequest Request { get; }

    public string PermissionKey => PermissionKeys.CatalogProductsRead;
}

public sealed class UpdateProductCommand : ApplicationRequest<ProductDto>, IAuthorizedRequest
{
    public UpdateProductCommand(int id, SaveProductRequest request)
    {
        Id = id;
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public int Id { get; }
    public SaveProductRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogProductsWrite;
}

public sealed class SetProductActiveCommand : ApplicationRequest<UnitResponse>, IAuthorizedRequest
{
    public SetProductActiveCommand(int id, bool isActive)
    {
        Id = id;
        IsActive = isActive;
    }

    public int Id { get; }
    public bool IsActive { get; }
    public string PermissionKey => PermissionKeys.CatalogProductsWrite;
}

public sealed class GetProductByIdQuery : ApplicationRequest<ProductDto>, IAuthorizedRequest
{
    public GetProductByIdQuery(int id) => Id = id;

    public int Id { get; }
    public string PermissionKey => PermissionKeys.CatalogProductsRead;
}

public sealed class LookupProductsQuery
    : ApplicationRequest<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>, IAuthorizedRequest
{
    public LookupProductsQuery(CatalogLookupRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public CatalogLookupRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogProductsRead;
}

internal sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(request => request.Code)
            .Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required")
            .MaximumLength(32)
            .WithErrorCode("Validation.MaximumLength");
        RuleFor(request => request.Name)
            .Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required")
            .MaximumLength(256)
            .WithErrorCode("Validation.MaximumLength");
        RuleFor(request => request.Unit)
            .Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required")
            .MaximumLength(32)
            .WithErrorCode("Validation.MaximumLength");
    }
}

internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Request).SetValidator(new SaveProductRequestValidator());
    }
}

internal sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(query => query.Request.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(1, 200)
            .WithErrorCode("Validation.Range");
        RuleFor(query => query.Request.Search)
            .MaximumLength(CatalogValidation.MaximumSearchLength)
            .WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters)
            .WithErrorCode("Validation.InvalidCharacter");
    }
}

internal sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
        RuleFor(command => command.Request).SetValidator(new SaveProductRequestValidator());
    }
}

internal sealed class SetProductActiveCommandValidator : AbstractValidator<SetProductActiveCommand>
{
    public SetProductActiveCommandValidator() =>
        RuleFor(command => command.Id).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
}

internal sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator() =>
        RuleFor(query => query.Id).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
}

internal sealed class LookupProductsQueryValidator : AbstractValidator<LookupProductsQuery>
{
    public LookupProductsQueryValidator()
    {
        RuleFor(query => query.Request.Limit)
            .InclusiveBetween(1, 200)
            .WithErrorCode("Validation.Range");
        RuleFor(query => query.Request.Search)
            .MaximumLength(CatalogValidation.MaximumSearchLength)
            .WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters)
            .WithErrorCode("Validation.InvalidCharacter");
    }
}

internal sealed class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, ErrorOr<ProductDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public CreateProductCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ICurrentUserAccessor currentUserAccessor)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ErrorOr<ProductDto>> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var product = new Product
        {
            Code = request.Code,
            Name = request.Name,
            Unit = request.Unit,
            CreatedByUserId = _currentUserAccessor.Current.UserId,
        };

        using var unitOfWork = await _unitOfWorkFactory
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            product.Id = await unitOfWork.Repository<ICatalogWriteRepository>()
                .InsertProductAsync(product, cancellationToken)
                .ConfigureAwait(false);
            unitOfWork.Commit();
        }
        catch (CatalogWriteConflictException exception)
            when (exception.Conflict == CatalogWriteConflict.DuplicateCode)
        {
            return Error.Conflict("Product.DuplicateCode", "A product with this code already exists.");
        }

        return new ProductDto(product.Id, product.Code, product.Name, product.Unit, product.IsActive);
    }
}

internal sealed class ListProductsQueryHandler
    : IRequestHandler<ListProductsQuery, ErrorOr<PagedResult<ProductDto>>>
{
    private readonly IProductQueryService _queryService;

    public ListProductsQueryHandler(IProductQueryService queryService) =>
        _queryService = queryService;

    public async Task<ErrorOr<PagedResult<ProductDto>>> Handle(
        ListProductsQuery query,
        CancellationToken cancellationToken)
    {
        return await _queryService
            .ListAsync(query.Request, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand, ErrorOr<ProductDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUtcClock _clock;
    private readonly IProductQueryService _queryService;

    public UpdateProductCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ICurrentUserAccessor currentUserAccessor,
        IUtcClock clock,
        IProductQueryService queryService)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _currentUserAccessor = currentUserAccessor;
        _clock = clock;
        _queryService = queryService;
    }

    public async Task<ErrorOr<ProductDto>> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = command.Id,
            Code = command.Request.Code,
            Name = command.Request.Name,
            Unit = command.Request.Unit,
            UpdatedAtUtc = _clock.UtcNow,
            UpdatedByUserId = _currentUserAccessor.Current.UserId,
        };

        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            var found = await unitOfWork.Repository<ICatalogWriteRepository>()
                .UpdateProductAsync(product, cancellationToken)
                .ConfigureAwait(false);
            if (!found)
            {
                return Error.NotFound("Product.NotFound", "The product was not found.");
            }

            unitOfWork.Commit();
        }
        catch (CatalogWriteConflictException exception)
            when (exception.Conflict == CatalogWriteConflict.DuplicateCode)
        {
            return Error.Conflict("Product.DuplicateCode", "A product with this code already exists.");
        }

        var persisted = await _queryService.GetByIdAsync(product.Id, cancellationToken).ConfigureAwait(false);
        if (persisted == null) return Error.Unexpected("Product.ReadAfterWriteFailed", "The updated product could not be reloaded.");
        return persisted;
    }
}

internal sealed class SetProductActiveCommandHandler
    : IRequestHandler<SetProductActiveCommand, ErrorOr<UnitResponse>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUtcClock _clock;

    public SetProductActiveCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ICurrentUserAccessor currentUserAccessor,
        IUtcClock clock)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _currentUserAccessor = currentUserAccessor;
        _clock = clock;
    }

    public async Task<ErrorOr<UnitResponse>> Handle(
        SetProductActiveCommand command,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        var found = await unitOfWork.Repository<ICatalogWriteRepository>()
            .SetProductActiveAsync(
                command.Id,
                command.IsActive,
                _clock.UtcNow,
                _currentUserAccessor.Current.UserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!found)
        {
            return Error.NotFound("Product.NotFound", "The product was not found.");
        }

        unitOfWork.Commit();
        return UnitResponse.Value;
    }
}

internal sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ErrorOr<ProductDto>>
{
    private readonly IProductQueryService _queryService;

    public GetProductByIdQueryHandler(IProductQueryService queryService) => _queryService = queryService;

    public async Task<ErrorOr<ProductDto>> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        var product = await _queryService.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            return Error.NotFound("Product.NotFound", "The product was not found.");
        }

        return product;
    }
}

internal sealed class LookupProductsQueryHandler
    : IRequestHandler<LookupProductsQuery, ErrorOr<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>>
{
    private readonly IProductQueryService _queryService;

    public LookupProductsQueryHandler(IProductQueryService queryService) => _queryService = queryService;

    public async Task<ErrorOr<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>> Handle(
        LookupProductsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await _queryService.LookupAsync(query.Request, cancellationToken).ConfigureAwait(false);
        return ErrorOrFactory.From<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>(items);
    }
}
