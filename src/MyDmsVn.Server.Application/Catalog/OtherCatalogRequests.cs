using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Application.Catalog;

public sealed class CreateWarehouseCommand : AuthorizedActorApplicationRequest<WarehouseDto>
{
    public CreateWarehouseCommand(SaveWarehouseRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public SaveWarehouseRequest Request { get; }
    public override string PermissionKey => PermissionKeys.CatalogWarehousesWrite;
}

public sealed class CreateEmployeeCommand : AuthorizedActorApplicationRequest<EmployeeDto>
{
    public CreateEmployeeCommand(SaveEmployeeRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public SaveEmployeeRequest Request { get; }
    public override string PermissionKey => PermissionKeys.CatalogEmployeesWrite;
}

public sealed class CreateCustomerCommand : AuthorizedActorApplicationRequest<CustomerDto>
{
    public CreateCustomerCommand(SaveCustomerRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public SaveCustomerRequest Request { get; }
    public override string PermissionKey => PermissionKeys.CatalogCustomersWrite;
}

public sealed class LookupWarehousesQuery : ApplicationRequest<IReadOnlyList<CatalogLookupDto>>, IAuthorizedRequest
{
    public LookupWarehousesQuery(CatalogLookupRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogLookupRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogWarehousesRead;
}

public sealed class LookupEmployeesQuery : ApplicationRequest<IReadOnlyList<CatalogLookupDto>>, IAuthorizedRequest
{
    public LookupEmployeesQuery(CatalogLookupRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogLookupRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogEmployeesRead;
}

public sealed class LookupCustomersQuery : ApplicationRequest<IReadOnlyList<CatalogLookupDto>>, IAuthorizedRequest
{
    public LookupCustomersQuery(CatalogLookupRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogLookupRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogCustomersRead;
}

internal sealed class SaveWarehouseRequestValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseRequestValidator()
    {
        RuleFor(request => request.Code).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(32).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Name).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(256).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Address).MaximumLength(500).WithErrorCode("Validation.MaximumLength");
    }
}

internal sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator() =>
        RuleFor(command => command.Request).SetValidator(new SaveWarehouseRequestValidator());
}

internal sealed class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(request => request.Code).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(32).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Name).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(256).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Address).MaximumLength(500).WithErrorCode("Validation.MaximumLength");
        RuleFor(request => request.Phone).MaximumLength(64).WithErrorCode("Validation.MaximumLength");
        RuleFor(request => request.TaxCode).MaximumLength(32).WithErrorCode("Validation.MaximumLength");
    }
}

internal sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator() =>
        RuleFor(command => command.Request).SetValidator(new SaveCustomerRequestValidator());
}

internal sealed class CreateWarehouseCommandHandler
    : IRequestHandler<CreateWarehouseCommand, ErrorOr<WarehouseDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public CreateWarehouseCommandHandler(IUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    public async Task<ErrorOr<WarehouseDto>> Handle(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var entity = new Warehouse
        {
            Code = command.Request.Code,
            Name = command.Request.Name,
            Address = command.Request.Address,
            CreatedByUserId = command.AuthorizedUserId,
        };
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            entity.Id = await unitOfWork.Repository<ICatalogWriteRepository>()
                .InsertWarehouseAsync(entity, cancellationToken).ConfigureAwait(false);
            unitOfWork.Commit();
        }
        catch (CatalogWriteConflictException)
        {
            return Error.Conflict("Warehouse.DuplicateCode", "A warehouse with this code already exists.");
        }

        return new WarehouseDto(entity.Id, entity.Code, entity.Name, entity.Address, entity.IsActive);
    }
}

internal sealed class CreateCustomerCommandHandler
    : IRequestHandler<CreateCustomerCommand, ErrorOr<CustomerDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public CreateCustomerCommandHandler(IUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    public async Task<ErrorOr<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var entity = new Customer
        {
            Code = command.Request.Code,
            Name = command.Request.Name,
            Address = command.Request.Address,
            Phone = command.Request.Phone,
            TaxCode = command.Request.TaxCode,
            CreatedByUserId = command.AuthorizedUserId,
        };
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            entity.Id = await unitOfWork.Repository<ICatalogWriteRepository>()
                .InsertCustomerAsync(entity, cancellationToken).ConfigureAwait(false);
            unitOfWork.Commit();
        }
        catch (CatalogWriteConflictException)
        {
            return Error.Conflict("Customer.DuplicateCode", "A customer with this code already exists.");
        }

        return new CustomerDto(
            entity.Id, entity.Code, entity.Name, entity.Address, entity.Phone, entity.TaxCode, entity.IsActive);
    }
}

internal sealed class SaveEmployeeRequestValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeRequestValidator()
    {
        RuleFor(request => request.Code).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(32).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Name).Must(CatalogValidation.HasDisplayText)
            .WithErrorCode("Validation.Required").MaximumLength(256).WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters).WithErrorCode("Validation.InvalidCharacter");
        RuleFor(request => request.Phone).MaximumLength(64).WithErrorCode("Validation.MaximumLength");
        RuleFor(request => request.UserId).GreaterThan(0).When(request => request.UserId.HasValue)
            .WithErrorCode("Validation.GreaterThan");
    }
}

internal sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator() =>
        RuleFor(command => command.Request).SetValidator(new SaveEmployeeRequestValidator());
}

internal sealed class CreateEmployeeCommandHandler
    : IRequestHandler<CreateEmployeeCommand, ErrorOr<EmployeeDto>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public CreateEmployeeCommandHandler(IUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    public async Task<ErrorOr<EmployeeDto>> Handle(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var employee = new Employee
        {
            Code = command.Request.Code,
            Name = command.Request.Name,
            Phone = command.Request.Phone,
            UserId = command.Request.UserId,
            CreatedByUserId = command.AuthorizedUserId,
        };
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            employee.Id = await unitOfWork.Repository<ICatalogWriteRepository>()
                .InsertEmployeeAsync(employee, cancellationToken).ConfigureAwait(false);
            unitOfWork.Commit();
        }
        catch (CatalogWriteConflictException exception)
        {
            return exception.Conflict switch
            {
                CatalogWriteConflict.EmployeeUserAlreadyLinked =>
                    Error.Conflict("Employee.UserAlreadyLinked", "This user is already linked to an employee."),
                CatalogWriteConflict.EmployeeUserNotFound =>
                    Error.Conflict("Employee.UserNotFound", "The selected user does not exist."),
                _ => Error.Conflict("Employee.DuplicateCode", "An employee with this code already exists."),
            };
        }

        return new EmployeeDto(
            employee.Id,
            employee.Code,
            employee.Name,
            employee.Phone,
            employee.UserId,
            employee.IsActive);
    }
}
