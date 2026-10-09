using System.Linq;

namespace MyDmsVn.Server.Application.Catalog;

internal static class CatalogValidation
{
    private static readonly char[] DisplayWhitespace =
    {
        ' ', '\t', '\n', '\v', '\f', '\r', '\u00a0',
    };

    public static bool HasDisplayText(string? value)
    {
        return value != null && value.Any(character => !DisplayWhitespace.Contains(character));
    }
}
