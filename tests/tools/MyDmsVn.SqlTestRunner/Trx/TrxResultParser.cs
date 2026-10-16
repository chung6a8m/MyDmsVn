using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MyDmsVn.SqlTestRunner.Inventory;

namespace MyDmsVn.SqlTestRunner.Trx;

public static class TrxResultParser
{
    public static TrxParseResult Parse(string path, string tfm)
    {
        var result = TryParse(path, tfm);
        if (!result.IsComplete)
        {
            throw new InvalidDataException("TRX is incomplete: " + result.IncompleteReason);
        }

        return result;
    }

    public static TrxParseResult TryParse(string path, string tfm)
    {
        if (!File.Exists(path))
        {
            return TrxParseResult.Incomplete(TrxIncompleteReason.Missing);
        }

        try
        {
            var document = XDocument.Load(path);
            var results = document.Descendants()
                .Where(element => element.Name.LocalName == "UnitTestResult")
                .Select(element =>
                {
                    var display = (string?)element.Attribute("testName") ?? string.Empty;
                    var method = TheoryRowIdentity.MethodName(display);
                    var outcome = ParseOutcome((string?)element.Attribute("outcome"));
                    var duration = TimeSpan.TryParse(
                        (string?)element.Attribute("duration"),
                        CultureInfo.InvariantCulture,
                        out var parsed)
                        ? parsed
                        : TimeSpan.Zero;
                    return new TrxTestResult(
                        TheoryRowIdentity.Create(method, tfm, display),
                        method,
                        tfm,
                        outcome,
                        duration)
                    {
                        PhysicalRowLocator = LogicalCaseIdentity.CreatePhysicalRowLocator(method, display),
                    };
                })
                .ToArray();
            return new TrxParseResult(true, TrxIncompleteReason.None, results);
        }
        catch (Exception exception) when (exception is IOException || exception is System.Xml.XmlException)
        {
            return TrxParseResult.Incomplete(TrxIncompleteReason.Corrupt);
        }
    }

    private static TestOutcome ParseOutcome(string? value) => value switch
    {
        "Passed" => TestOutcome.Passed,
        "Failed" => TestOutcome.Failed,
        _ => TestOutcome.Skipped,
    };
}

public enum TestOutcome
{
    Passed,
    Failed,
    Skipped,
}

public enum TrxIncompleteReason
{
    None,
    Missing,
    Corrupt,
}

public sealed record TrxTestResult(
    string StableCaseId,
    string MethodName,
    string Tfm,
    TestOutcome Outcome,
    TimeSpan Duration)
{
    public string PhysicalRowLocator { get; init; } = string.Empty;

    public static TrxTestResult Passed(
        string stableCaseId,
        string methodName,
        string tfm,
        TimeSpan duration) =>
        new TrxTestResult(stableCaseId, methodName, tfm, TestOutcome.Passed, duration);
}

public sealed record TrxParseResult(
    bool IsComplete,
    TrxIncompleteReason IncompleteReason,
    IReadOnlyList<TrxTestResult> Results)
{
    public static TrxParseResult Incomplete(TrxIncompleteReason reason) =>
        new TrxParseResult(false, reason, Array.Empty<TrxTestResult>());
}
