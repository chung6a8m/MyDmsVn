using System;
using System.IO;
using System.Linq;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator;

public static class DatabaseMigratorCommand
{
    public static int Run(
        SqlConnectionConfigurationLoader configurationLoader,
        TextWriter output,
        TextWriter error)
    {
        if (configurationLoader == null)
        {
            throw new ArgumentNullException(nameof(configurationLoader));
        }

        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (error == null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        try
        {
            var configuration = configurationLoader.Load(
                SqlConnectionConfigurationPurpose.Migration);
            var result = new DatabaseMigrationRunner().Migrate(configuration.ConnectionString);
            if (!result.Successful)
            {
                error.WriteLine(
                    "Migration.Failed: Migration không thành công. Xem log quản trị an toàn để chẩn đoán.");
                return 1;
            }

            output.WriteLine(
                $"Migration.Completed: đã áp dụng {result.Scripts.Count()} script.");
            return 0;
        }
        catch (OperationalConfigurationException exception)
        {
            error.WriteLine($"{exception.Code}: {exception.SafeMessage}");
            return 2;
        }
        catch (DatabaseMigrationConcurrencyException exception)
        {
            error.WriteLine($"{exception.Code}: {exception.Message}");
            return 3;
        }
        catch (DatabaseMigrationPreflightException exception)
        {
            error.WriteLine($"{exception.Code}: {exception.Message}");
            return 4;
        }
        catch (DatabaseMigrationAuthorizationException exception)
        {
            error.WriteLine($"{exception.Code}: {exception.Message}");
            return 5;
        }
        catch (Exception)
        {
            error.WriteLine(
                "Migration.Failed: Migration không thành công. Xem log quản trị an toàn để chẩn đoán.");
            return 1;
        }
    }
}
