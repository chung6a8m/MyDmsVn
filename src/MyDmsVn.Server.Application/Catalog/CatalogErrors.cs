using System.Collections.Generic;
using ErrorOr;

namespace MyDmsVn.Server.Application.Catalog;

internal static class CatalogErrors
{
    public static Error DuplicateCode(string code, string message) =>
        Error.Conflict(code, message, new Dictionary<string, object> { ["Field"] = "Code" });
}
