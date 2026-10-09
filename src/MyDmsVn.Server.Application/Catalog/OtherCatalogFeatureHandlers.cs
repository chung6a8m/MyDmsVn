using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Application.Catalog;

internal sealed class WarehouseFeatureHandlers :
    IRequestHandler<UpdateWarehouseCommand, ErrorOr<WarehouseDto>>,
    IRequestHandler<SetWarehouseActiveCommand, ErrorOr<UnitResponse>>,
    IRequestHandler<ListWarehousesQuery, ErrorOr<PagedResult<WarehouseDto>>>,
    IRequestHandler<GetWarehouseByIdQuery, ErrorOr<WarehouseDto>>,
    IRequestHandler<LookupWarehousesQuery, ErrorOr<IReadOnlyList<CatalogLookupDto>>>
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUtcClock _clock;
    private readonly IWarehouseQueryService _queries;

    public WarehouseFeatureHandlers(IUnitOfWorkFactory factory, IUtcClock clock, IWarehouseQueryService queries)
    { _factory = factory; _clock = clock; _queries = queries; }

    public async Task<ErrorOr<WarehouseDto>> Handle(UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var entity = new Warehouse { Id = command.Id, Code = command.Request.Code, Name = command.Request.Name, Address = command.Request.Address, UpdatedAtUtc = _clock.UtcNow, UpdatedByUserId = command.AuthorizedUserId };
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        try
        {
            if (!await uow.Repository<ICatalogWriteRepository>().UpdateWarehouseAsync(entity, cancellationToken).ConfigureAwait(false))
                return Error.NotFound("Warehouse.NotFound", "The warehouse was not found.");
            uow.Commit();
        }
        catch (CatalogWriteConflictException)
        { return Error.Conflict("Warehouse.DuplicateCode", "A warehouse with this code already exists."); }
        var persisted = await _queries.GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        if (persisted == null) return Error.Unexpected("Warehouse.ReadAfterWriteFailed", "The updated warehouse could not be reloaded.");
        return persisted;
    }

    public async Task<ErrorOr<UnitResponse>> Handle(SetWarehouseActiveCommand command, CancellationToken cancellationToken)
    {
        var authorizedUserId = command.AuthorizedUserId;
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        if (!await uow.Repository<ICatalogWriteRepository>().SetWarehouseActiveAsync(command.Id, command.IsActive, _clock.UtcNow, authorizedUserId, cancellationToken).ConfigureAwait(false))
            return Error.NotFound("Warehouse.NotFound", "The warehouse was not found.");
        uow.Commit();
        return UnitResponse.Value;
    }

    public async Task<ErrorOr<PagedResult<WarehouseDto>>> Handle(ListWarehousesQuery query, CancellationToken cancellationToken) =>
        await _queries.ListAsync(query.Request, cancellationToken).ConfigureAwait(false);

    public async Task<ErrorOr<WarehouseDto>> Handle(GetWarehouseByIdQuery query, CancellationToken cancellationToken)
    {
        var value = await _queries.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (value == null) return Error.NotFound("Warehouse.NotFound", "The warehouse was not found.");
        return value;
    }

    public async Task<ErrorOr<IReadOnlyList<CatalogLookupDto>>> Handle(LookupWarehousesQuery query, CancellationToken cancellationToken) =>
        ErrorOrFactory.From<IReadOnlyList<CatalogLookupDto>>(await _queries.LookupAsync(query.Request, cancellationToken).ConfigureAwait(false));
}

internal sealed class EmployeeFeatureHandlers :
    IRequestHandler<UpdateEmployeeCommand, ErrorOr<EmployeeDto>>,
    IRequestHandler<SetEmployeeActiveCommand, ErrorOr<UnitResponse>>,
    IRequestHandler<ListEmployeesQuery, ErrorOr<PagedResult<EmployeeDto>>>,
    IRequestHandler<GetEmployeeByIdQuery, ErrorOr<EmployeeDto>>,
    IRequestHandler<LookupEmployeesQuery, ErrorOr<IReadOnlyList<CatalogLookupDto>>>
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUtcClock _clock;
    private readonly IEmployeeQueryService _queries;

    public EmployeeFeatureHandlers(IUnitOfWorkFactory factory, IUtcClock clock, IEmployeeQueryService queries)
    { _factory = factory; _clock = clock; _queries = queries; }

    public async Task<ErrorOr<EmployeeDto>> Handle(UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var entity = new Employee { Id = command.Id, Code = command.Request.Code, Name = command.Request.Name, Phone = command.Request.Phone, UserId = command.Request.UserId, UpdatedAtUtc = _clock.UtcNow, UpdatedByUserId = command.AuthorizedUserId };
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        try
        {
            if (!await uow.Repository<ICatalogWriteRepository>().UpdateEmployeeAsync(entity, cancellationToken).ConfigureAwait(false))
                return Error.NotFound("Employee.NotFound", "The employee was not found.");
            uow.Commit();
        }
        catch (CatalogWriteConflictException exception)
        { return MapEmployeeConflict(exception.Conflict); }
        var persisted = await _queries.GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        if (persisted == null) return Error.Unexpected("Employee.ReadAfterWriteFailed", "The updated employee could not be reloaded.");
        return persisted;
    }

    public async Task<ErrorOr<UnitResponse>> Handle(SetEmployeeActiveCommand command, CancellationToken cancellationToken)
    {
        var authorizedUserId = command.AuthorizedUserId;
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        if (!await uow.Repository<ICatalogWriteRepository>().SetEmployeeActiveAsync(command.Id, command.IsActive, _clock.UtcNow, authorizedUserId, cancellationToken).ConfigureAwait(false))
            return Error.NotFound("Employee.NotFound", "The employee was not found.");
        uow.Commit();
        return UnitResponse.Value;
    }

    public async Task<ErrorOr<PagedResult<EmployeeDto>>> Handle(ListEmployeesQuery query, CancellationToken cancellationToken) =>
        await _queries.ListAsync(query.Request, cancellationToken).ConfigureAwait(false);

    public async Task<ErrorOr<EmployeeDto>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken)
    {
        var value = await _queries.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (value == null) return Error.NotFound("Employee.NotFound", "The employee was not found.");
        return value;
    }

    public async Task<ErrorOr<IReadOnlyList<CatalogLookupDto>>> Handle(LookupEmployeesQuery query, CancellationToken cancellationToken) =>
        ErrorOrFactory.From<IReadOnlyList<CatalogLookupDto>>(await _queries.LookupAsync(query.Request, cancellationToken).ConfigureAwait(false));

    private static Error MapEmployeeConflict(CatalogWriteConflict conflict) => conflict switch
    {
        CatalogWriteConflict.EmployeeUserAlreadyLinked => Error.Conflict("Employee.UserAlreadyLinked", "This user is already linked to an employee."),
        CatalogWriteConflict.EmployeeUserNotFound => Error.Conflict("Employee.UserNotFound", "The selected user does not exist."),
        _ => Error.Conflict("Employee.DuplicateCode", "An employee with this code already exists."),
    };
}

internal sealed class CustomerFeatureHandlers :
    IRequestHandler<UpdateCustomerCommand, ErrorOr<CustomerDto>>,
    IRequestHandler<SetCustomerActiveCommand, ErrorOr<UnitResponse>>,
    IRequestHandler<ListCustomersQuery, ErrorOr<PagedResult<CustomerDto>>>,
    IRequestHandler<GetCustomerByIdQuery, ErrorOr<CustomerDto>>,
    IRequestHandler<LookupCustomersQuery, ErrorOr<IReadOnlyList<CatalogLookupDto>>>
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUtcClock _clock;
    private readonly ICustomerQueryService _queries;

    public CustomerFeatureHandlers(IUnitOfWorkFactory factory, IUtcClock clock, ICustomerQueryService queries)
    { _factory = factory; _clock = clock; _queries = queries; }

    public async Task<ErrorOr<CustomerDto>> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var entity = new Customer { Id = command.Id, Code = command.Request.Code, Name = command.Request.Name, Address = command.Request.Address, Phone = command.Request.Phone, TaxCode = command.Request.TaxCode, UpdatedAtUtc = _clock.UtcNow, UpdatedByUserId = command.AuthorizedUserId };
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        try
        {
            if (!await uow.Repository<ICatalogWriteRepository>().UpdateCustomerAsync(entity, cancellationToken).ConfigureAwait(false))
                return Error.NotFound("Customer.NotFound", "The customer was not found.");
            uow.Commit();
        }
        catch (CatalogWriteConflictException)
        { return Error.Conflict("Customer.DuplicateCode", "A customer with this code already exists."); }
        var persisted = await _queries.GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        if (persisted == null) return Error.Unexpected("Customer.ReadAfterWriteFailed", "The updated customer could not be reloaded.");
        return persisted;
    }

    public async Task<ErrorOr<UnitResponse>> Handle(SetCustomerActiveCommand command, CancellationToken cancellationToken)
    {
        var authorizedUserId = command.AuthorizedUserId;
        using var uow = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        uow.BeginTransaction();
        if (!await uow.Repository<ICatalogWriteRepository>().SetCustomerActiveAsync(command.Id, command.IsActive, _clock.UtcNow, authorizedUserId, cancellationToken).ConfigureAwait(false))
            return Error.NotFound("Customer.NotFound", "The customer was not found.");
        uow.Commit();
        return UnitResponse.Value;
    }

    public async Task<ErrorOr<PagedResult<CustomerDto>>> Handle(ListCustomersQuery query, CancellationToken cancellationToken) =>
        await _queries.ListAsync(query.Request, cancellationToken).ConfigureAwait(false);

    public async Task<ErrorOr<CustomerDto>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        var value = await _queries.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (value == null) return Error.NotFound("Customer.NotFound", "The customer was not found.");
        return value;
    }

    public async Task<ErrorOr<IReadOnlyList<CatalogLookupDto>>> Handle(LookupCustomersQuery query, CancellationToken cancellationToken) =>
        ErrorOrFactory.From<IReadOnlyList<CatalogLookupDto>>(await _queries.LookupAsync(query.Request, cancellationToken).ConfigureAwait(false));
}
