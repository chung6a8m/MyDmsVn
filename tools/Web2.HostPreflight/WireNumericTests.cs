using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Web2.HostPreflight;

public sealed class WireNumericTests
{
    public static IEnumerable<object[]> Vectors()
    {
        var document = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "numeric-vectors.json")));
        foreach (var item in (JArray)document["int64"]!)
        {
            yield return new object[] { "int64", (string)item["value"]!, 0, (bool)item["allowZero"]! };
        }
        foreach (var item in (JArray)document["decimals"]!)
        {
            yield return new object[] { (string)item["field"]!, (string)item["value"]!, (int)item["scale"]!, false };
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void CanonicalWireStringsRoundtripThroughNet48NumericTypesAndJson(
        string field, string value, int scale, bool allowZero)
    {
        string formatted;
        if (field == "int64")
        {
            var parsed = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
            Assert.True(parsed > 0 || (allowZero && parsed == 0));
            formatted = parsed.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var parsed = decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture);
            formatted = parsed.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
        Assert.Equal(value, formatted);
        var json = JsonConvert.SerializeObject(new { value = formatted });
        var roundtrip = JObject.Parse(json)["value"]!;
        Assert.Equal(JTokenType.String, roundtrip.Type);
        Assert.Equal(value, roundtrip.Value<string>());
    }
}
