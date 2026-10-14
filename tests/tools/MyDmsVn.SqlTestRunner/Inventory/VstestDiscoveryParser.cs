using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class VstestDiscoveryParser
{
    public static IReadOnlyList<DiscoveredTestCase> Parse(string output, string tfm)
    {
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var cases = new List<DiscoveredTestCase>();
        var started = false;
        foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (!started)
            {
                started = line.Equals("The following Tests are available:", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (line.Length == 0 || line.StartsWith("Total tests:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var method = TheoryRowIdentity.MethodName(line);
            rows.TryGetValue(method, out var ordinal);
            cases.Add(CreateCase(method, tfm, ordinal, line));
            rows[method] = ordinal + 1;
        }

        return cases;
    }

    public static DiscoveredTestCase CreateCase(string methodName, string tfm, int rowOrdinal) =>
        CreateCase(methodName, tfm, rowOrdinal, methodName + "#synthetic-row:" + rowOrdinal);

    private static DiscoveredTestCase CreateCase(
        string methodName,
        string tfm,
        int rowOrdinal,
        string displayName) =>
        new DiscoveredTestCase(
            methodName,
            tfm,
            rowOrdinal,
            TheoryRowIdentity.Create(methodName, tfm, displayName));
}

public sealed record DiscoveredTestCase(
    string MethodName,
    string Tfm,
    int RowOrdinal,
    string StableCaseId);
