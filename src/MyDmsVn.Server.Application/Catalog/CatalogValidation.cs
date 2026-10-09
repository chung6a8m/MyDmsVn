using System.Linq;

namespace MyDmsVn.Server.Application.Catalog;

internal static class CatalogValidation
{
    public const int MaximumSearchLength = 256;

    private static readonly char[] DisplayWhitespace =
    {
        ' ', '\t', '\n', '\v', '\f', '\r', '\u00a0',
    };

    public static bool HasDisplayText(string? value)
    {
        return value != null && value.Any(character => !DisplayWhitespace.Contains(character));
    }

    public static bool HasSupportedSearchCharacters(string? value)
    {
        return value == null || value.IndexOf('\0') < 0;
    }
}
