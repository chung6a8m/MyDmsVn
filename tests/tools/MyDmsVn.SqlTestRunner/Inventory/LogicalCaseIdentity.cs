using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class LogicalCaseIdentity
{
    private static readonly Regex GenericArity = new Regex(
        "`[0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Create(
        int version,
        string tfm,
        string normalizedSignature,
        string rowKey,
        IReadOnlyCollection<string> classification)
    {
        if (version != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Only logical identity v1 is supported.");
        }

        var normalizedTfm = Required(tfm, nameof(tfm));
        var normalizedMethod = Required(normalizedSignature, nameof(normalizedSignature));
        var normalizedRow = Required(rowKey, nameof(rowKey));
        ArgumentNullException.ThrowIfNull(classification);
        var normalizedClassification = classification
            .Select(value => Required(value, nameof(classification)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalizedClassification.Length == 0)
        {
            throw new ArgumentException("At least one reviewed classification is required.", nameof(classification));
        }

        var payload = Frame(version.ToString(CultureInfo.InvariantCulture)) +
            Frame(normalizedTfm) +
            Frame(normalizedMethod) +
            Frame(normalizedRow) +
            string.Concat(normalizedClassification.Select(Frame));
        return "v1:" + Hash(payload);
    }

    public static string NormalizeSignature(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var declaringType = method.DeclaringType?.FullName
            ?? throw new ArgumentException("The test method requires a declaring type.", nameof(method));
        var genericArity = method.IsGenericMethodDefinition || method.IsGenericMethod
            ? "`" + method.GetGenericArguments().Length.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        var parameters = string.Join(",", method.GetParameters().Select(parameter =>
            NormalizeType(parameter.ParameterType)));
        return declaringType.Replace('+', '.') + "." + method.Name + genericArity +
            "(" + parameters + ")";
    }

    public static string CreatePhysicalRowLocator(string methodName, string displayName)
    {
        var payload = Frame(Required(methodName, nameof(methodName))) +
            Frame(Required(displayName, nameof(displayName)).Trim());
        return Hash(payload);
    }

    private static string NormalizeType(Type type)
    {
        if (type.IsByRef)
        {
            return NormalizeType(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return NormalizeType(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            return NormalizeType(type.GetElementType()!) +
                "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "!" : "!!") +
                type.GenericParameterPosition.ToString(CultureInfo.InvariantCulture);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName
                ?? throw new InvalidOperationException("A generic parameter type name could not be resolved.");
            var name = GenericArity.Replace(definition.Replace('+', '.'), string.Empty);
            return name + "<" + string.Join(",", type.GetGenericArguments().Select(NormalizeType)) + ">";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", name)
            : value;

    private static string Frame(string value) =>
        value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

    private static string Hash(string value) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();
}
