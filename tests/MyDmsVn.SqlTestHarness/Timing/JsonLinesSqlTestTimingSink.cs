using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MyDmsVn.SqlTestHarness.Timing;

public sealed class JsonLinesSqlTestTimingSink : ISqlTestTimingSink
{
    private static readonly ConcurrentDictionary<string, object> PathLocks =
        new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.None,
        Converters = { new StringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _sync;

    public JsonLinesSqlTestTimingSink(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A timing output path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
        _sync = PathLocks.GetOrAdd(_path, _ => new object());
    }

    public void Write(SqlTestTimingEvent timingEvent)
    {
        if (timingEvent is null)
        {
            throw new ArgumentNullException(nameof(timingEvent));
        }

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonConvert.SerializeObject(timingEvent, Settings);
        lock (_sync)
        {
            using var stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.WriteThrough);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }
}
