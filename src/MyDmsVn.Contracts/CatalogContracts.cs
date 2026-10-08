using System;

namespace MyDmsVn.Contracts
{
    public sealed class CatalogListRequest
    {
        public CatalogListRequest(
            string? search,
            int pageNumber,
            int pageSize,
            bool includeInactive)
        {
            Search = search;
            PageNumber = pageNumber;
            PageSize = pageSize;
            IncludeInactive = includeInactive;
        }

        public string? Search { get; }
        public int PageNumber { get; }
        public int PageSize { get; }
        public bool IncludeInactive { get; }
    }

    public sealed class ProductDto
    {
        public ProductDto(int id, string code, string name, string unit, bool isActive)
        {
            Id = id;
            Code = code;
            Name = name;
            Unit = unit;
            IsActive = isActive;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string Unit { get; }
        public bool IsActive { get; }
    }

    public sealed class SaveProductRequest
    {
        public SaveProductRequest(string code, string name, string unit)
        {
            Code = code;
            Name = name;
            Unit = unit;
        }

        public string Code { get; }
        public string Name { get; }
        public string Unit { get; }
    }

    public sealed class WarehouseDto
    {
        public WarehouseDto(int id, string code, string name, string? address, bool isActive)
        {
            Id = id;
            Code = code;
            Name = name;
            Address = address;
            IsActive = isActive;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string? Address { get; }
        public bool IsActive { get; }
    }

    public sealed class SaveWarehouseRequest
    {
        public SaveWarehouseRequest(string code, string name, string? address)
        {
            Code = code;
            Name = name;
            Address = address;
        }

        public string Code { get; }
        public string Name { get; }
        public string? Address { get; }
    }

    public sealed class EmployeeDto
    {
        public EmployeeDto(
            int id,
            string code,
            string name,
            string? phone,
            int? userId,
            bool isActive)
        {
            Id = id;
            Code = code;
            Name = name;
            Phone = phone;
            UserId = userId;
            IsActive = isActive;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string? Phone { get; }
        public int? UserId { get; }
        public bool IsActive { get; }
    }

    public sealed class SaveEmployeeRequest
    {
        public SaveEmployeeRequest(string code, string name, string? phone, int? userId)
        {
            Code = code;
            Name = name;
            Phone = phone;
            UserId = userId;
        }

        public string Code { get; }
        public string Name { get; }
        public string? Phone { get; }
        public int? UserId { get; }
    }

    public sealed class CustomerDto
    {
        public CustomerDto(
            int id,
            string code,
            string name,
            string? address,
            string? phone,
            string? taxCode,
            bool isActive)
        {
            Id = id;
            Code = code;
            Name = name;
            Address = address;
            Phone = phone;
            TaxCode = taxCode;
            IsActive = isActive;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string? Address { get; }
        public string? Phone { get; }
        public string? TaxCode { get; }
        public bool IsActive { get; }
    }

    public sealed class SaveCustomerRequest
    {
        public SaveCustomerRequest(
            string code,
            string name,
            string? address,
            string? phone,
            string? taxCode)
        {
            Code = code;
            Name = name;
            Address = address;
            Phone = phone;
            TaxCode = taxCode;
        }

        public string Code { get; }
        public string Name { get; }
        public string? Address { get; }
        public string? Phone { get; }
        public string? TaxCode { get; }
    }
}
