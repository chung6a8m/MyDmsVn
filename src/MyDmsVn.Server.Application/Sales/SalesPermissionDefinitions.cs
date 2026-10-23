using System.Collections.Generic;
using MyDmsVn.Server.Application.Security;
namespace MyDmsVn.Server.Application.Sales;
public static class SalesPermissionKeys
{
    public const string ReceiptsRead = "Sales.Receipts.Read";
    public const string ReceiptsWrite = "Sales.Receipts.Write";
    public const string ReceiptsPost = "Sales.Receipts.Post";
}
public static class SalesPermissionDefinitions
{
    public static IReadOnlyList<PermissionDefinition> All { get; } = new[]
    {
        new PermissionDefinition(SalesPermissionKeys.ReceiptsRead, "Sales", "Read sales receipts and persisted cost/profit."),
        new PermissionDefinition(SalesPermissionKeys.ReceiptsWrite, "Sales", "Create and update draft sales receipts."),
        new PermissionDefinition(SalesPermissionKeys.ReceiptsPost, "Sales", "Post sales receipts."),
    };
}
