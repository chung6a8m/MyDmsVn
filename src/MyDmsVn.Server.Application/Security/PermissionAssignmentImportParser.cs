using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using MyDmsVn.Contracts.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Application.Security;

public sealed class PermissionAssignmentImportParseResult
{
    private PermissionAssignmentImportParseResult(
        ParsedPermissionAssignmentImportDto? value,
        IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    public bool IsValid => Value != null;
    public ParsedPermissionAssignmentImportDto? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    internal static PermissionAssignmentImportParseResult Success(
        ParsedPermissionAssignmentImportDto value) =>
        new(value, Array.Empty<string>());

    internal static PermissionAssignmentImportParseResult Invalid(params string[] errors) =>
        new(null, new ReadOnlyCollection<string>(errors));
}

/// <summary>Parses the deliberately narrow, versioned permission assignment import format.</summary>
public sealed class PermissionAssignmentImportParser
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumUtf8Bytes = PermissionAssignmentImportLimits.MaximumUtf8Bytes;
    public const int MaximumEntries = PermissionAssignmentImportLimits.MaximumEntries;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IPermissionRegistry _registry;

    public PermissionAssignmentImportParser(IPermissionRegistry registry) =>
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public PermissionAssignmentImportParseResult Parse(string? json)
    {
        if (json == null)
        {
            return PermissionAssignmentImportParseResult.Invalid("Import content is required.");
        }

        try
        {
            if (StrictUtf8.GetByteCount(json) > MaximumUtf8Bytes)
            {
                return PermissionAssignmentImportParseResult.Invalid("Import content exceeds the 1 MiB limit.");
            }
        }
        catch (EncoderFallbackException)
        {
            return PermissionAssignmentImportParseResult.Invalid("Import content is not valid UTF-8 text.");
        }

        try
        {
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text)
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 16,
                SupportMultipleContent = false,
            };
            var rootToken = JToken.ReadFrom(reader, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Load,
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            });
            if (reader.Read() || rootToken.Type != JTokenType.Object ||
                ((JContainer)rootToken).Descendants().Any(token => token.Type == JTokenType.Comment))
            {
                return PermissionAssignmentImportParseResult.Invalid("Import must be one JSON object without comments or trailing content.");
            }

            var root = (JObject)rootToken;
            if (!HasExactProperties(root, "schemaVersion", "targetType", "targetId", "entries"))
            {
                return PermissionAssignmentImportParseResult.Invalid("Import fields do not match schema version 1.");
            }

            if (root["schemaVersion"]?.Type != JTokenType.Integer ||
                (long)root["schemaVersion"]! != CurrentSchemaVersion)
            {
                return PermissionAssignmentImportParseResult.Invalid("Unsupported or missing import schema version.");
            }

            if (root["targetType"]?.Type != JTokenType.String)
            {
                return PermissionAssignmentImportParseResult.Invalid("Import target type is required.");
            }

            PermissionImportTargetTypeDto targetType;
            switch ((string)root["targetType"]!)
            {
                case "User": targetType = PermissionImportTargetTypeDto.User; break;
                case "Role": targetType = PermissionImportTargetTypeDto.Role; break;
                default: return PermissionAssignmentImportParseResult.Invalid("Import target type must be User or Role.");
            }

            if (root["targetId"]?.Type != JTokenType.Integer ||
                !int.TryParse(root["targetId"]!.ToString(Formatting.None), out var targetId) || targetId <= 0)
            {
                return PermissionAssignmentImportParseResult.Invalid("Import target ID must be a positive integer.");
            }

            if (root["entries"]?.Type != JTokenType.Array)
            {
                return PermissionAssignmentImportParseResult.Invalid("Import entries must be an array.");
            }

            var entryArray = (JArray)root["entries"]!;
            if (entryArray.Count > MaximumEntries)
            {
                return PermissionAssignmentImportParseResult.Invalid("Import cannot contain more than 5,000 entries.");
            }

            var entries = new List<PermissionAssignmentImportEntryDto>(entryArray.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in entryArray)
            {
                if (token.Type != JTokenType.Object ||
                    !HasExactProperties((JObject)token, "permissionKey", "decision"))
                {
                    return PermissionAssignmentImportParseResult.Invalid("Each entry must contain exactly permissionKey and decision.");
                }

                var item = (JObject)token;
                if (item["permissionKey"]?.Type != JTokenType.String || item["decision"]?.Type != JTokenType.String)
                {
                    return PermissionAssignmentImportParseResult.Invalid("Permission key and decision must be strings.");
                }

                var key = (string)item["permissionKey"]!;
                if (string.IsNullOrWhiteSpace(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal) ||
                    !_registry.TryGet(key, out var definition) ||
                    definition.LifecycleStatus != PermissionLifecycleStatus.Active)
                {
                    return PermissionAssignmentImportParseResult.Invalid("Import contains an unknown, deprecated, or noncanonical permission key.");
                }

                if (!seen.Add(key))
                {
                    return PermissionAssignmentImportParseResult.Invalid("Import contains duplicate permission entries.");
                }

                var decision = (string)item["decision"]!;
                bool granted;
                if (decision == "Allow")
                {
                    granted = true;
                }
                else if (decision == "Deny" && targetType == PermissionImportTargetTypeDto.User)
                {
                    granted = false;
                }
                else
                {
                    return PermissionAssignmentImportParseResult.Invalid("Decision must be Allow; direct user imports may also use Deny.");
                }

                entries.Add(new PermissionAssignmentImportEntryDto(key, granted));
            }

            return PermissionAssignmentImportParseResult.Success(new ParsedPermissionAssignmentImportDto(
                CurrentSchemaVersion,
                targetType,
                targetId,
                new ReadOnlyCollection<PermissionAssignmentImportEntryDto>(entries)));
        }
        catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                         exception is OverflowException || exception is ArgumentException)
        {
            return PermissionAssignmentImportParseResult.Invalid("Import JSON is malformed or exceeds parser limits.");
        }
    }

    private static bool HasExactProperties(JObject value, params string[] names)
    {
        var actual = value.Properties().Select(property => property.Name).ToArray();
        return actual.Length == names.Length && names.All(name =>
            actual.Count(candidate => string.Equals(candidate, name, StringComparison.Ordinal)) == 1);
    }
}
