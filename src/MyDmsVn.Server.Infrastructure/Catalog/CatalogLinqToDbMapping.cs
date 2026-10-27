using LinqToDB;
using LinqToDB.Mapping;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class CatalogLinqToDbMapping : ILinqToDbMapping
{
    public void Configure(FluentMappingBuilder builder)
    {
        var product = builder.Entity<Product>().HasSchemaName("dbo").HasTableName("Products");
        product.Property(e => e.Id).HasColumnName("ProductId").IsPrimaryKey().IsIdentity().IsNotNull();
        product.Property(e => e.Code).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        product.Property(e => e.Name).HasDataType(DataType.NVarChar).HasLength(256).IsNotNull();
        product.Property(e => e.Unit).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        product.Property(e => e.IsActive).HasDataType(DataType.Boolean).IsNotNull();
        product.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        product.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        product.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        product.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);

        var warehouse = builder.Entity<Warehouse>().HasSchemaName("dbo").HasTableName("Warehouses");
        warehouse.Property(e => e.Id).HasColumnName("WarehouseId").IsPrimaryKey().IsIdentity().IsNotNull();
        warehouse.Property(e => e.Code).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        warehouse.Property(e => e.Name).HasDataType(DataType.NVarChar).HasLength(256).IsNotNull();
        warehouse.Property(e => e.Address).HasDataType(DataType.NVarChar).HasLength(500).IsNullable(true);
        warehouse.Property(e => e.IsActive).HasDataType(DataType.Boolean).IsNotNull();
        warehouse.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        warehouse.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        warehouse.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        warehouse.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);

        var employee = builder.Entity<Employee>().HasSchemaName("dbo").HasTableName("Employees");
        employee.Property(e => e.Id).HasColumnName("EmployeeId").IsPrimaryKey().IsIdentity().IsNotNull();
        employee.Property(e => e.Code).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        employee.Property(e => e.Name).HasDataType(DataType.NVarChar).HasLength(256).IsNotNull();
        employee.Property(e => e.Phone).HasDataType(DataType.NVarChar).HasLength(64).IsNullable(true);
        employee.Property(e => e.UserId).HasDataType(DataType.Int32).IsNullable(true);
        employee.Property(e => e.IsActive).HasDataType(DataType.Boolean).IsNotNull();
        employee.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        employee.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        employee.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        employee.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);

        var customer = builder.Entity<Customer>().HasSchemaName("dbo").HasTableName("Customers");
        customer.Property(e => e.Id).HasColumnName("CustomerId").IsPrimaryKey().IsIdentity().IsNotNull();
        customer.Property(e => e.Code).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        customer.Property(e => e.Name).HasDataType(DataType.NVarChar).HasLength(256).IsNotNull();
        customer.Property(e => e.Address).HasDataType(DataType.NVarChar).HasLength(500).IsNullable(true);
        customer.Property(e => e.Phone).HasDataType(DataType.NVarChar).HasLength(64).IsNullable(true);
        customer.Property(e => e.TaxCode).HasDataType(DataType.NVarChar).HasLength(32).IsNullable(true);
        customer.Property(e => e.IsActive).HasDataType(DataType.Boolean).IsNotNull();
        customer.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        customer.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        customer.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        customer.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);

    }
}
