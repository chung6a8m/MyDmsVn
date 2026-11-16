using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

// Keeps the strictly decoded source separate from the typed HTTP contract.
// Operation fingerprints must use Source, never JSON reserialized from Value.
internal sealed class StrictJsonRequest<T>
{
    public StrictJsonRequest(T value, JObject source)
    {
        Value = value;
        Source = source;
    }

    public T Value { get; }
    public JObject Source { get; }
}

internal static class StrictJsonRequest
{
    public static async Task<StrictJsonRequest<T>> Read<T>(HttpRequestMessage request, Func<JObject, T> decode)
    {
        var content = request.Content;
        if (content?.Headers.ContentType == null ||
            !string.Equals(content.Headers.ContentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            content.Headers.ContentType.CharSet != null &&
            !string.Equals(content.Headers.ContentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase) ||
            content.Headers.ContentEncoding.Any())
            throw new WireInputException();

        var source = StrictWireJson.Read(await content.ReadAsByteArrayAsync().ConfigureAwait(false));
        return new StrictJsonRequest<T>(decode(source), source);
    }
}
