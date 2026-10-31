using Microsoft.Data.SqlClient;
namespace MyDmsVn.Server.DbMigrator;
public static class Schema10WebMaintenanceValidator
{
    // Identical SQL-login-only restriction/DMV/complete-drain boundary, explicitly selected for 10->11.
    public static void Validate(SqlConnection connection) => Schema9MaintenanceValidator.Validate(connection);
}
