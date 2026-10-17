using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.DbMigrator;

public static class PermissionCatalogCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (error == null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        if (args.Length != 3 ||
            !string.Equals(args[0], "list", StringComparison.Ordinal) ||
            !string.Equals(args[1], "--format", StringComparison.Ordinal) ||
            !string.Equals(args[2], "json", StringComparison.Ordinal))
        {
            error.WriteLine("PermissionCatalog.InvalidArguments: Use permissions list --format json.");
            return 2;
        }

        output.WriteLine(JsonConvert.SerializeObject(
            PermissionCatalogFactory.CreateDefault(),
            Formatting.Indented,
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            }));
        return 0;
    }
}
