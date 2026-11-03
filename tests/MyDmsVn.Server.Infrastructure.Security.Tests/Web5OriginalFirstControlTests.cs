using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5OriginalFirstControlTests
{
    [Fact]
    public void Stage_telemetry_is_allowlisted_and_contains_no_request_data()
    {
        using var output = new StringWriter();
        Web5OriginalFirstTelemetry.Write(output, Web5OriginalFirstStage.WriterObserved);
        Assert.Equal("WEB5_ORIGINAL_FIRST stage=WriterObserved\n", output.ToString().Replace("\r\n", "\n"));
        Assert.Throws<InvalidDataException>(() => Web5OriginalFirstTelemetry.Write(output, (Web5OriginalFirstStage)999));
    }
    [Theory]
    [InlineData("ArmReplySent")]
    [InlineData("WaitReplySent")]
    [InlineData("ReleaseReplySent")]
    public void Reply_telemetry_contains_only_allowlisted_stage(string expected)
    {
        using var output = new StringWriter();
        Web5OriginalFirstTelemetry.Write(output, (Web5OriginalFirstStage)Enum.Parse(typeof(Web5OriginalFirstStage), expected));
        Assert.Equal("WEB5_ORIGINAL_FIRST stage=" + expected + "\n", output.ToString().Replace("\r\n", "\n"));
    }
    [Theory]
    [InlineData("waitOriginal")]
    [InlineData("releaseOriginal")]
    public void Key_actions_accept_only_exact_frame_and_canonical_key(string action)
    {
        var frame = new JObject { ["id"] = 1, ["token"] = "secret", ["action"] = action, ["operationId"] = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee" };
        Assert.Equal(action, WebRecoveryPilotControl.Validate(frame, "secret"));
        frame["operationId"] = "AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE";
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "secret"));
        frame["operationId"] = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"; frame["extra"] = 1;
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "secret"));
    }

    [Fact]
    public void Arm_requires_object_and_owner_token()
    {
        var frame = new JObject { ["id"] = 1, ["token"] = "secret", ["action"] = "armOriginal", ["operation"] = new JObject() };
        Assert.Equal("armOriginal", WebRecoveryPilotControl.Validate(frame, "secret"));
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "wrong"));
        frame["operation"] = "bad";
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "secret"));
    }
}
