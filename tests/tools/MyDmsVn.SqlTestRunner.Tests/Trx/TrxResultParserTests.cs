using System;
using System.IO;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Trx;

public sealed class TrxResultParserTests
{
    [Fact]
    public void Parser_reads_outcomes_and_duration_without_parameter_values_in_id()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                "<UnitTestResult testId=\"a\" testName=\"Example.Tests.Theory(value: secret)\" outcome=\"Passed\" duration=\"00:00:01.2500000\" />" +
                "</Results></TestRun>");

            var result = TrxResultParser.Parse(path, "net8.0");

            var test = Assert.Single(result.Results);
            Assert.Equal(TestOutcome.Passed, test.Outcome);
            Assert.Equal(TimeSpan.FromMilliseconds(1250), test.Duration);
            Assert.DoesNotContain("secret", test.StableCaseId, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(64, test.PhysicalRowLocator.Length);
            Assert.DoesNotContain(
                "secret",
                test.PhysicalRowLocator,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Missing_or_corrupt_trx_is_typed_incomplete()
    {
        var result = TrxResultParser.TryParse("missing.trx", "net8.0");
        Assert.False(result.IsComplete);
        Assert.Equal(TrxIncompleteReason.Missing, result.IncompleteReason);
    }
}
