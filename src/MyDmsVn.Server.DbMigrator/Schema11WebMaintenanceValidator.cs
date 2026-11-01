using Microsoft.Data.SqlClient;
namespace MyDmsVn.Server.DbMigrator;
public static class Schema11WebMaintenanceValidator
{
    public static void Validate(SqlConnection connection) => Schema9MaintenanceValidator.Validate(connection);
}
