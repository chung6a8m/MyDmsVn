using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebOperationResolutionWireTests
{
    public static IEnumerable<object[]> Vectors()
    {
        foreach (var file in Directory.GetFiles(Corpus(), "*.json").Where(file => Path.GetFileName(file).StartsWith("sales-",StringComparison.Ordinal) || Path.GetFileName(file).StartsWith("receipt-",StringComparison.Ordinal)))
            yield return new object[] { Path.GetFileName(file), File.ReadAllText(file) };
    }

    [Theory, MemberData(nameof(Vectors))]
    public void Published_resolution_matches_original_result_and_exact_terminal_shape(string name, string json)
    {
        using var source = new StringReader(json);
        using var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None };
        var expected = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var data = (JObject)expected["data"]!;
        var kind = Kind((string)data["operationKind"]!);
        var state = (string)data["state"]!;
        var terminal = data["terminal"] is JObject value ? new WebOperationTerminalDecision(Guid.Parse((string)value["decisionId"]!),
            (string)value["caseId"]!, DateTime.Parse((string)value["decidedAtUtc"]!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)) : null;
        var bytes = data["outcome"] is JObject result ? Encoding.UTF8.GetBytes(result.ToString(Formatting.None)) : null;
        var resultId = bytes == null ? (long?)null : long.Parse((string)data["outcome"]!["data"]![kind <= WebOperationKind.SalesPost ? "saleId" : "receiptId"]!, CultureInfo.InvariantCulture);
        var fingerprint = Hex((string)data["requestFingerprint"]!);
        var status = new WebOperationStatus(Guid.Parse((string)data["operationId"]!), Guid.Parse((string)data["deploymentEpoch"]!), kind,
            data["requestTargetId"]!.Type == JTokenType.Null ? (long?)null : long.Parse((string)data["requestTargetId"]!, CultureInfo.InvariantCulture), fingerprint,
            resultId, terminal?.DecidedAtUtc ?? new DateTime(2026,11,2,9,10,11,DateTimeKind.Utc), state, bytes);
        var actual = WebOperationResolutionWire.Envelope(new WebOperationResolution(status,terminal),(int)data["actorUserId"]!,status.DeploymentEpoch,status.OperationId,fingerprint);
        Assert.True(JToken.DeepEquals(data,actual),name);
        Assert.Equal(12,actual.Properties().Count());
    }

    [Theory]
    [InlineData("case with payload")][InlineData("")][InlineData("case\n")]
    public void Invalid_audit_metadata_cannot_become_a_terminal_certificate(string caseId)
    {
        var time=new DateTime(2026,11,2,9,10,11,DateTimeKind.Utc);
        var status=new WebOperationStatus(Guid.NewGuid(),Guid.NewGuid(),WebOperationKind.SalesCreate,null,new byte[32],null,time,"NOT_EXECUTED",null);
        Assert.Throws<WebOperationException>(()=>WebOperationResolutionWire.Envelope(new WebOperationResolution(status,
            new WebOperationTerminalDecision(Guid.NewGuid(),caseId,time)),42,status.DeploymentEpoch,status.OperationId,status.RequestFingerprint));
    }

    [Fact]
    public void A_terminal_marker_cannot_substitute_for_exact_identity_or_original_result()
    {
        var time=new DateTime(2026,11,2,9,10,11,DateTimeKind.Utc);
        var status=new WebOperationStatus(Guid.NewGuid(),Guid.NewGuid(),WebOperationKind.ReceiptPost,10,new byte[32],null,time,"NOT_EXECUTED",null);
        var resolution=new WebOperationResolution(status,new WebOperationTerminalDecision(Guid.NewGuid(),"CASE-42",time));
        Assert.Throws<WebOperationException>(()=>WebOperationResolutionWire.Envelope(resolution,42,Guid.NewGuid(),status.OperationId,status.RequestFingerprint));
        Assert.Throws<WebOperationException>(()=>WebOperationResolutionWire.Envelope(new WebOperationResolution(status),42,status.DeploymentEpoch,status.OperationId,status.RequestFingerprint));
        var fake=new WebOperationStatus(status.OperationId,status.DeploymentEpoch,status.Kind,10,status.RequestFingerprint,10,time,"NOT_EXECUTED",Encoding.UTF8.GetBytes("{}"));
        Assert.Throws<WebOperationException>(()=>WebOperationResolutionWire.Envelope(new WebOperationResolution(fake,resolution.Terminal),42,status.DeploymentEpoch,status.OperationId,status.RequestFingerprint));
    }

    private static byte[] Hex(string value)=>Enumerable.Range(0,value.Length/2).Select(i=>byte.Parse(value.Substring(i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture)).ToArray();
    private static WebOperationKind Kind(string value)=> (WebOperationKind)Enum.Parse(typeof(WebOperationKind),value.Replace(".",""));
    private static string Corpus()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null){var path=Path.Combine(dir.FullName,"docs","contracts","web5-resolution-v1");if(Directory.Exists(path))return path;dir=dir.Parent;}
        throw new DirectoryNotFoundException("WEB5 source corpus required.");
    }
}
