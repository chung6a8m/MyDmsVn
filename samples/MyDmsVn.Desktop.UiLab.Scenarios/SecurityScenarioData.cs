using System.Collections.Generic;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.UiLab.Scenarios;
public sealed class SecurityScenarioData
{
    public List<SecurityUserDto> Users { get; } = new List<SecurityUserDto>
    {
        new SecurityUserDto(101, "lan", "Nguyễn Thị Lan", "lan@example.test", true, 11, new[] { 201, 203 }, new[] { new SecurityDecisionDto("Sales.Receipts.Post", false), new SecurityDecisionDto("Inventory.Stock.Read", true) }),
        new SecurityUserDto(102, "inactive", "Tài khoản ngừng hoạt động", null, false, 12, new[] { 201 }, new SecurityDecisionDto[0]),
        new SecurityUserDto(103, "protected", "Quản trị bảo vệ", null, true, 13, new int[0], new SecurityDecisionDto[0], true)
    };
    public List<SecurityRoleDto> Roles { get; } = new List<SecurityRoleDto>
    {
        new SecurityRoleDto(201, "Nhân viên bán hàng", true, 21, new[] { 101, 102 }, new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Legacy.Orphan" }),
        new SecurityRoleDto(202, "Kiểm soát", true, 22, new int[0], new[] { "Sales.Receipts.Post" }),
        new SecurityRoleDto(203, "Vai trò ngừng hoạt động", false, 23, new[] { 101 }, new[] { "Sales.Receipts.Read", "Inventory.Stock.Audit" })
    };
    public PermissionCatalogDto Catalog { get; set; } = new PermissionCatalogDto("UI Lab", "security-fixture-v1", new[]
    {
        new PermissionCatalogItemDto("Sales.Receipts.Read", "Sales", "Xem phiếu bán hàng", "Active"),
        new PermissionCatalogItemDto("Sales.Receipts.Write", "Sales", "Sửa phiếu bán hàng", "Active"),
        new PermissionCatalogItemDto("Sales.Receipts.Post", "Sales", "Ghi sổ bán hàng", "Active"),
        new PermissionCatalogItemDto("Inventory.Stock.Read", "Inventory", "Xem tồn kho", "Active"),
        new PermissionCatalogItemDto("Inventory.Stock.Audit", "Inventory", "Kiểm tra tồn kho", "Active"),
        new PermissionCatalogItemDto("Inventory.Stock.Export", "Inventory", "Xuất danh sách tồn kho", "Active"),
        new PermissionCatalogItemDto("Legacy.Deprecated", "Legacy", "Quyền ngừng hỗ trợ", "Deprecated")
    });
}
