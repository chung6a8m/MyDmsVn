using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentValidation;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Catalog;

public sealed class UpdateWarehouseCommand : ApplicationRequest<WarehouseDto>, IAuthorizedRequest
{
    public UpdateWarehouseCommand(int id, SaveWarehouseRequest request) { Id = id; Request = request ?? throw new ArgumentNullException(nameof(request)); }
    public int Id { get; }
    public SaveWarehouseRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogWarehousesWrite;
}
public sealed class SetWarehouseActiveCommand : ApplicationRequest<UnitResponse>, IAuthorizedRequest
{
    public SetWarehouseActiveCommand(int id, bool isActive) { Id = id; IsActive = isActive; }
    public int Id { get; }
    public bool IsActive { get; }
    public string PermissionKey => PermissionKeys.CatalogWarehousesWrite;
}
public sealed class ListWarehousesQuery : ApplicationRequest<PagedResult<WarehouseDto>>, IAuthorizedRequest
{
    public ListWarehousesQuery(CatalogListRequest request) => Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogListRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogWarehousesRead;
}
public sealed class GetWarehouseByIdQuery : ApplicationRequest<WarehouseDto>, IAuthorizedRequest
{
    public GetWarehouseByIdQuery(int id) => Id = id;
    public int Id { get; }
    public string PermissionKey => PermissionKeys.CatalogWarehousesRead;
}

public sealed class UpdateEmployeeCommand : ApplicationRequest<EmployeeDto>, IAuthorizedRequest
{
    public UpdateEmployeeCommand(int id, SaveEmployeeRequest request) { Id = id; Request = request ?? throw new ArgumentNullException(nameof(request)); }
    public int Id { get; }
    public SaveEmployeeRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogEmployeesWrite;
}
public sealed class SetEmployeeActiveCommand : ApplicationRequest<UnitResponse>, IAuthorizedRequest
{
    public SetEmployeeActiveCommand(int id, bool isActive) { Id = id; IsActive = isActive; }
    public int Id { get; }
    public bool IsActive { get; }
    public string PermissionKey => PermissionKeys.CatalogEmployeesWrite;
}
public sealed class ListEmployeesQuery : ApplicationRequest<PagedResult<EmployeeDto>>, IAuthorizedRequest
{
    public ListEmployeesQuery(CatalogListRequest request) => Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogListRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogEmployeesRead;
}
public sealed class GetEmployeeByIdQuery : ApplicationRequest<EmployeeDto>, IAuthorizedRequest
{
    public GetEmployeeByIdQuery(int id) => Id = id;
    public int Id { get; }
    public string PermissionKey => PermissionKeys.CatalogEmployeesRead;
}

public sealed class UpdateCustomerCommand : ApplicationRequest<CustomerDto>, IAuthorizedRequest
{
    public UpdateCustomerCommand(int id, SaveCustomerRequest request) { Id = id; Request = request ?? throw new ArgumentNullException(nameof(request)); }
    public int Id { get; }
    public SaveCustomerRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogCustomersWrite;
}
public sealed class SetCustomerActiveCommand : ApplicationRequest<UnitResponse>, IAuthorizedRequest
{
    public SetCustomerActiveCommand(int id, bool isActive) { Id = id; IsActive = isActive; }
    public int Id { get; }
    public bool IsActive { get; }
    public string PermissionKey => PermissionKeys.CatalogCustomersWrite;
}
public sealed class ListCustomersQuery : ApplicationRequest<PagedResult<CustomerDto>>, IAuthorizedRequest
{
    public ListCustomersQuery(CatalogListRequest request) => Request = request ?? throw new ArgumentNullException(nameof(request));
    public CatalogListRequest Request { get; }
    public string PermissionKey => PermissionKeys.CatalogCustomersRead;
}
public sealed class GetCustomerByIdQuery : ApplicationRequest<CustomerDto>, IAuthorizedRequest
{
    public GetCustomerByIdQuery(int id) => Id = id;
    public int Id { get; }
    public string PermissionKey => PermissionKeys.CatalogCustomersRead;
}

internal static class OtherCatalogValidationRules
{
    public static void AddIdRule<T>(AbstractValidator<T> validator, Expression<Func<T, int>> id) =>
        validator.RuleFor(id).GreaterThan(0).WithErrorCode("Validation.GreaterThan");

    public static void AddListRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int>> pageNumber,
        Expression<Func<T, int>> pageSize,
        Expression<Func<T, string?>> search)
    {
        validator.RuleFor(pageNumber).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
        validator.RuleFor(pageSize).InclusiveBetween(1, 200).WithErrorCode("Validation.Range");
        AddSearchRule(validator, search);
    }

    public static void AddLookupRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int>> limit,
        Expression<Func<T, string?>> search)
    {
        validator.RuleFor(limit).InclusiveBetween(1, 200).WithErrorCode("Validation.Range");
        AddSearchRule(validator, search);
    }

    private static void AddSearchRule<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string?>> search)
    {
        validator.RuleFor(search)
            .MaximumLength(CatalogValidation.MaximumSearchLength)
            .WithErrorCode("Validation.MaximumLength")
            .Must(CatalogValidation.HasSupportedSearchCharacters)
            .WithErrorCode("Validation.InvalidCharacter");
    }
}

internal sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator() { OtherCatalogValidationRules.AddIdRule(this, x => x.Id); RuleFor(x => x.Request).SetValidator(new SaveWarehouseRequestValidator()); }
}
internal sealed class SetWarehouseActiveCommandValidator : AbstractValidator<SetWarehouseActiveCommand>
{ public SetWarehouseActiveCommandValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class ListWarehousesQueryValidator : AbstractValidator<ListWarehousesQuery>
{ public ListWarehousesQueryValidator() => OtherCatalogValidationRules.AddListRules(this, x => x.Request.PageNumber, x => x.Request.PageSize, x => x.Request.Search); }
internal sealed class GetWarehouseByIdQueryValidator : AbstractValidator<GetWarehouseByIdQuery>
{ public GetWarehouseByIdQueryValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class LookupWarehousesQueryValidator : AbstractValidator<LookupWarehousesQuery>
{ public LookupWarehousesQueryValidator() => OtherCatalogValidationRules.AddLookupRules(this, x => x.Request.Limit, x => x.Request.Search); }

internal sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{ public UpdateEmployeeCommandValidator() { OtherCatalogValidationRules.AddIdRule(this, x => x.Id); RuleFor(x => x.Request).SetValidator(new SaveEmployeeRequestValidator()); } }
internal sealed class SetEmployeeActiveCommandValidator : AbstractValidator<SetEmployeeActiveCommand>
{ public SetEmployeeActiveCommandValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class ListEmployeesQueryValidator : AbstractValidator<ListEmployeesQuery>
{ public ListEmployeesQueryValidator() => OtherCatalogValidationRules.AddListRules(this, x => x.Request.PageNumber, x => x.Request.PageSize, x => x.Request.Search); }
internal sealed class GetEmployeeByIdQueryValidator : AbstractValidator<GetEmployeeByIdQuery>
{ public GetEmployeeByIdQueryValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class LookupEmployeesQueryValidator : AbstractValidator<LookupEmployeesQuery>
{ public LookupEmployeesQueryValidator() => OtherCatalogValidationRules.AddLookupRules(this, x => x.Request.Limit, x => x.Request.Search); }

internal sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{ public UpdateCustomerCommandValidator() { OtherCatalogValidationRules.AddIdRule(this, x => x.Id); RuleFor(x => x.Request).SetValidator(new SaveCustomerRequestValidator()); } }
internal sealed class SetCustomerActiveCommandValidator : AbstractValidator<SetCustomerActiveCommand>
{ public SetCustomerActiveCommandValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class ListCustomersQueryValidator : AbstractValidator<ListCustomersQuery>
{ public ListCustomersQueryValidator() => OtherCatalogValidationRules.AddListRules(this, x => x.Request.PageNumber, x => x.Request.PageSize, x => x.Request.Search); }
internal sealed class GetCustomerByIdQueryValidator : AbstractValidator<GetCustomerByIdQuery>
{ public GetCustomerByIdQueryValidator() => OtherCatalogValidationRules.AddIdRule(this, x => x.Id); }
internal sealed class LookupCustomersQueryValidator : AbstractValidator<LookupCustomersQuery>
{ public LookupCustomersQueryValidator() => OtherCatalogValidationRules.AddLookupRules(this, x => x.Request.Limit, x => x.Request.Search); }
