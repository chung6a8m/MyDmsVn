using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class TestMethodInventory
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    public static IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var reviewedCode = new ReviewedSourceIndex(ReadRepositorySource(fullPath));
        var context = new AssemblyLoadContext("sql-inventory-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        var resolver = new AssemblyDependencyResolver(fullPath);
        context.Resolving += (_, assemblyName) =>
        {
            var dependencyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return dependencyPath is null ? null : context.LoadFromAssemblyPath(dependencyPath);
        };
        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            return assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .Select(method => new { Method = method, Attributes = CustomAttributeData.GetCustomAttributes(method) })
                .Where(item => item.Attributes.Any(attribute =>
                    IsAttributeOrDerived(attribute.AttributeType, "Xunit.FactAttribute") ||
                    IsAttributeOrDerived(attribute.AttributeType, "Xunit.TheoryAttribute")))
                .Select(item =>
                {
                    var fingerprint = FingerprintReviewedCodeCore(item.Method, reviewedCode);
                    return new TestMethodDescriptor(
                        item.Method.DeclaringType!.FullName + "." + item.Method.Name,
                        item.Attributes.Any(attribute =>
                            IsAttributeOrDerived(attribute.AttributeType, "Xunit.TheoryAttribute")),
                        fingerprint.Fingerprint,
                        ReadCategories(item.Method),
                        fingerprint.IsComplete)
                    {
                        NormalizedSignature = LogicalCaseIdentity.NormalizeSignature(item.Method),
                    };
                })
                .OrderBy(item => item.FullyQualifiedName, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            context.Unload();
        }
    }

    private static bool IsAttributeOrDerived(Type attributeType, string expectedFullName)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, expectedFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string FingerprintReviewedCode(
        MethodInfo method,
        IReadOnlyDictionary<string, byte[]> reviewedFiles)
        => FingerprintReviewedCodeCore(method, new ReviewedSourceIndex(reviewedFiles)).Fingerprint;

    internal static bool IsReviewedCodeFingerprintComplete(
        MethodInfo method,
        IReadOnlyDictionary<string, byte[]> reviewedFiles)
        => FingerprintReviewedCodeCore(method, new ReviewedSourceIndex(reviewedFiles)).IsComplete;

    private static ReviewedCodeFingerprint FingerprintReviewedCodeCore(
        MethodInfo method,
        ReviewedSourceIndex reviewedCode)
    {
        var selection = SelectReviewedCode(method, reviewedCode);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(
            method.DeclaringType!.FullName + "." + method.Name));
        foreach (var file in selection.Files.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Key.ToLowerInvariant()));
            hash.AppendData(file.Value);
        }

        return new ReviewedCodeFingerprint(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            selection.IsComplete);
    }

    private static ReviewedCodeSelection SelectReviewedCode(
        MethodInfo rootMethod,
        ReviewedSourceIndex reviewedCode)
    {
        var selected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<MethodBase>();
        var isComplete = true;
        pending.Push(rootMethod);
        while (pending.Count != 0)
        {
            var method = pending.Pop();
            var identity = MethodIdentity(method);
            if (!visited.Add(identity))
            {
                continue;
            }

            var hasReviewedSource = AddDeclaringTypeSource(
                method.DeclaringType,
                reviewedCode,
                selected,
                method.Module.Assembly != rootMethod.Module.Assembly,
                out var sourceResolutionComplete);
            isComplete &= sourceResolutionComplete;
            if (!hasReviewedSource)
            {
                continue;
            }

            if (method is MethodInfo methodInfo)
            {
                foreach (var stateMachineMethod in ReadStateMachineMethods(methodInfo))
                {
                    pending.Push(stateMachineMethod);
                }
            }

            var references = ReadReferencedMethods(method);
            if (!references.IsComplete)
            {
                isComplete &= reviewedCode.AddClosedProjectBoundary(method.DeclaringType, selected);
            }
            foreach (var unresolvedHint in references.UnresolvedMemberHints)
            {
                if (!reviewedCode.AddProjectBoundaryForHint(unresolvedHint, selected))
                {
                    isComplete &= reviewedCode.AddClosedProjectBoundary(method.DeclaringType, selected);
                }
            }

            foreach (var referencedMethod in references.Methods)
            {
                if (!reviewedCode.HasSource(referencedMethod.DeclaringType))
                {
                    if (IsRepositoryAssembly(referencedMethod.Module.Assembly))
                    {
                        isComplete = false;
                    }

                    continue;
                }

                if (referencedMethod.Module.Assembly == rootMethod.Module.Assembly)
                {
                    pending.Push(referencedMethod);
                }
                else
                {
                    AddDeclaringTypeSource(
                        referencedMethod.DeclaringType,
                        reviewedCode,
                        selected,
                        includeProjectBoundary: true,
                        out var referencedSourceResolutionComplete);
                    isComplete &= referencedSourceResolutionComplete;
                }
            }

            foreach (var referencedType in references.Types)
            {
                if (!reviewedCode.HasSource(referencedType))
                {
                    if (IsRepositoryAssembly(referencedType.Assembly))
                    {
                        isComplete = false;
                    }

                    continue;
                }

                AddDeclaringTypeSource(
                    referencedType,
                    reviewedCode,
                    selected,
                    referencedType.Assembly != rootMethod.Module.Assembly,
                    out var referencedTypeResolutionComplete);
                isComplete &= referencedTypeResolutionComplete;
            }
        }

        if (!reviewedCode.AddReferencedConstantSources(selected))
        {
            isComplete &= reviewedCode.AddClosedProjectBoundary(rootMethod.DeclaringType, selected);
        }

        if (!isComplete)
        {
            reviewedCode.AddClosedProjectBoundary(rootMethod.DeclaringType, selected);
        }

        if (selected.Count == 0)
        {
            throw new InvalidOperationException(
                "No reviewed source file could be resolved for " + MethodIdentity(rootMethod) + ".");
        }

        return new ReviewedCodeSelection(selected, isComplete);
    }

    private static bool IsRepositoryAssembly(Assembly assembly) =>
        assembly.GetName().Name?.StartsWith("MyDmsVn.", StringComparison.Ordinal) == true;

    private static IEnumerable<MethodInfo> ReadStateMachineMethods(MethodInfo method)
    {
        foreach (var attribute in CustomAttributeData.GetCustomAttributes(method))
        {
            if ((string.Equals(
                    attribute.AttributeType.FullName,
                    "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
                    StringComparison.Ordinal) ||
                string.Equals(
                    attribute.AttributeType.FullName,
                    "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
                    StringComparison.Ordinal)) &&
                attribute.ConstructorArguments.Count == 1 &&
                attribute.ConstructorArguments[0].Value is Type stateMachineType)
            {
                var moveNext = stateMachineType.GetMethod(
                    "MoveNext",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (moveNext is not null)
                {
                    yield return moveNext;
                }
            }
        }
    }

    private static ReferencedMethodReadResult ReadReferencedMethods(MethodBase method)
    {
        var methods = new List<MethodBase>();
        var types = new List<Type>();
        var unresolvedMemberHints = new List<string>();
        var isComplete = true;
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
        var typeArguments = method.DeclaringType?.GetGenericArguments();
        var methodArguments = method is MethodInfo methodInfo && methodInfo.IsGenericMethod
            ? methodInfo.GetGenericArguments()
            : null;
        for (var index = 0; index < il.Length;)
        {
            var first = il[index++];
            short value = first == 0xfe && index < il.Length
                ? unchecked((short)(0xfe00 | il[index++]))
                : first;
            if (!OpCodesByValue.TryGetValue(value, out var opCode))
            {
                return new ReferencedMethodReadResult(methods, types, false, unresolvedMemberHints);
            }

            var operandIndex = index;
            if (opCode.OperandType == OperandType.InlineMethod && operandIndex <= il.Length - 4)
            {
                MethodBase? referenced = null;
                try
                {
                    referenced = method.Module.ResolveMethod(
                        BitConverter.ToInt32(il, operandIndex),
                        typeArguments,
                        methodArguments);
                }
                catch (ArgumentException)
                {
                    isComplete = false;
                }
                catch (BadImageFormatException)
                {
                    isComplete = false;
                }
                catch (MissingMethodException exception)
                {
                    unresolvedMemberHints.Add(exception.Message);
                }
                catch (TypeLoadException)
                {
                    isComplete = false;
                }
                catch (FileNotFoundException)
                {
                    isComplete = false;
                }
                catch (FileLoadException)
                {
                    isComplete = false;
                }
                catch (NotSupportedException)
                {
                    isComplete = false;
                }

                if (referenced is not null)
                {
                    methods.Add(referenced);
                }
            }
            else if ((opCode.OperandType == OperandType.InlineField ||
                      opCode.OperandType == OperandType.InlineType ||
                      opCode.OperandType == OperandType.InlineTok) &&
                     operandIndex <= il.Length - 4)
            {
                try
                {
                    var member = method.Module.ResolveMember(
                        BitConverter.ToInt32(il, operandIndex),
                        typeArguments,
                        methodArguments);
                    switch (member)
                    {
                        case MethodBase referencedMethod:
                            methods.Add(referencedMethod);
                            break;
                        case Type referencedType:
                            types.Add(referencedType);
                            break;
                        case FieldInfo referencedField when referencedField.DeclaringType is not null:
                            types.Add(referencedField.DeclaringType);
                            break;
                    }
                }
                catch (ArgumentException)
                {
                    isComplete = false;
                }
                catch (BadImageFormatException)
                {
                    isComplete = false;
                }
                catch (MissingMemberException exception)
                {
                    unresolvedMemberHints.Add(exception.Message);
                }
                catch (MemberAccessException)
                {
                    isComplete = false;
                }
                catch (TypeLoadException)
                {
                    isComplete = false;
                }
                catch (FileNotFoundException)
                {
                    isComplete = false;
                }
                catch (FileLoadException)
                {
                    isComplete = false;
                }
                catch (NotSupportedException)
                {
                    isComplete = false;
                }
            }

            index += OperandSize(opCode.OperandType, il, operandIndex);
        }

        return new ReferencedMethodReadResult(methods, types, isComplete, unresolvedMemberHints);
    }

    private static int OperandSize(OperandType operandType, byte[] il, int operandIndex) =>
        operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
                OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch when operandIndex <= il.Length - 4 =>
                4 + (BitConverter.ToInt32(il, operandIndex) * 4),
            _ => 0,
        };

    private static bool AddDeclaringTypeSource(
        Type? declaringType,
        ReviewedSourceIndex reviewedCode,
        IDictionary<string, byte[]> selected,
        bool includeProjectBoundary,
        out bool isComplete)
    {
        var source = reviewedCode.Find(declaringType);
        isComplete = !source.IsAmbiguous;
        var found = false;
        foreach (var match in source.Files)
        {
            selected[match.Key] = match.Value;
            if (includeProjectBoundary)
            {
                reviewedCode.AddProjectBoundary(match.Key, selected);
            }

            found = true;
        }

        return found;
    }

    private static string MethodIdentity(MethodBase method)
    {
        try
        {
            return method.Module.ModuleVersionId.ToString("N") + ":" +
                method.MetadataToken.ToString("x8");
        }
        catch (InvalidOperationException)
        {
            return method.DeclaringType?.AssemblyQualifiedName + "." + method.Name;
        }
    }

    private static IReadOnlyDictionary<string, byte[]> ReadRepositorySource(string assemblyPath)
    {
        var repositoryRoot = FindRepositoryRoot(Path.GetDirectoryName(assemblyPath)!);
        if (repositoryRoot is null)
        {
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetFileName(assemblyPath)] = File.ReadAllBytes(assemblyPath),
            };
        }

        // Build a source index once, then fingerprint only the root method's
        // declaring source and source-backed methods reached through its IL.
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".json", ".props", ".resx", ".sql", ".targets",
        };
        return Directory.GetFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(path => !HasBuildOutputSegment(repositoryRoot, path))
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .ToDictionary(
                path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static bool HasBuildOutputSegment(string repositoryRoot, string path)
    {
        var relative = Path.GetRelativePath(repositoryRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ReviewedSourceIndex
    {
        private readonly IReadOnlyDictionary<string, byte[]> _files;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SourceTypeEntry>> _filesByTypeIdentity;
        private readonly IReadOnlyList<ConstantSourceEntry> _constants;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, byte[]>>> _projectFilesByDirectory;
        private readonly IReadOnlyDictionary<string, string> _assemblyNameByProjectDirectory;
        private readonly IReadOnlyDictionary<string, string> _strippedSourceByPath;
        private readonly Dictionary<string, ISet<string>> _reachableProjectsByDirectory =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<ConstantBinding>> _constantBindingsByProject =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ConstantReference> _constantReferenceCache =
            new(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _baseTypeNamesByTypeName;
        private readonly ISet<string> _knownTypeNames;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _globalStaticImportsByProject;

        public ReviewedSourceIndex(IReadOnlyDictionary<string, byte[]> files)
        {
            _files = files;
            _strippedSourceByPath = files
                .Where(file => string.Equals(Path.GetExtension(file.Key), ".cs", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    file => file.Key,
                    file => StripCommentsAndLiterals(Encoding.UTF8.GetString(file.Value)),
                    StringComparer.OrdinalIgnoreCase);
            var inheritance = _strippedSourceByPath.Values.SelectMany(ReadInheritance).ToArray();
            _baseTypeNamesByTypeName = inheritance
                .GroupBy(item => item.DerivedTypeName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .SelectMany(item => item.BaseTypeNames)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            _knownTypeNames = _strippedSourceByPath.Values
                .SelectMany(ReadQualifiedTypeNames)
                .ToHashSet(StringComparer.Ordinal);
            _projectFilesByDirectory = files
                .Where(file => string.Equals(
                    Path.GetExtension(file.Key),
                    ".csproj",
                    StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => DirectoryName(file.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<KeyValuePair<string, byte[]>>)group.ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            _globalStaticImportsByProject = _strippedSourceByPath
                .Select(item => new { item.Value, ProjectDirectory = FindProjectDirectory(item.Key) })
                .Where(item => item.ProjectDirectory is not null)
                .SelectMany(item => Regex.Matches(
                        item.Value,
                        @"\bglobal\s+using\s+static\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;",
                        RegexOptions.CultureInvariant)
                    .Cast<Match>()
                    .Select(match => new { item.ProjectDirectory, TypeName = match.Groups[1].Value }))
                .GroupBy(item => item.ProjectDirectory!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .Select(item => item.TypeName)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            _assemblyNameByProjectDirectory = _projectFilesByDirectory.ToDictionary(
                item => item.Key,
                item => ReadAssemblyName(item.Value[0]),
                StringComparer.OrdinalIgnoreCase);
            _filesByTypeIdentity = files
                .Where(file => string.Equals(
                    Path.GetExtension(file.Key),
                    ".cs",
                    StringComparison.OrdinalIgnoreCase))
                .Select(file => new { File = file, ProjectDirectory = FindProjectDirectory(file.Key) })
                .Where(item => item.ProjectDirectory is not null)
                .SelectMany(item => DeclaredTypeIdentities(
                    item.File,
                    _assemblyNameByProjectDirectory[item.ProjectDirectory!])
                    .Select(identity => new SourceTypeEntry(identity, item.ProjectDirectory!, item.File)))
                .GroupBy(item => item.Identity, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<SourceTypeEntry>)group.ToArray(),
                    StringComparer.Ordinal);
            _constants = files
                .Where(file => string.Equals(Path.GetExtension(file.Key), ".cs", StringComparison.OrdinalIgnoreCase))
                .Select(file => new { File = file, ProjectDirectory = FindProjectDirectory(file.Key) })
                .Where(item => item.ProjectDirectory is not null)
                .SelectMany(item => DeclaredConstants(item.File, item.ProjectDirectory!))
                .ToArray();
        }

        public bool HasSource(Type? declaringType)
        {
            var source = Find(declaringType);
            return source.Files.Count != 0 && !source.IsAmbiguous;
        }

        private static IEnumerable<string> DeclaredTypeIdentities(
            KeyValuePair<string, byte[]> file,
            string assemblyName)
        {
            var source = StripCommentsAndLiterals(Encoding.UTF8.GetString(file.Value));
            var namespaceMatch = Regex.Match(
                source,
                @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
                RegexOptions.CultureInvariant);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
            var scopes = ReadTypeScopes(source);
            foreach (var scope in scopes)
            {
                var qualifiedName = QualifiedSourceTypeName(namespaceName, scope, scopes);
                yield return assemblyName + "|" + qualifiedName;
            }
        }

        private static string ReadAssemblyName(
            KeyValuePair<string, byte[]> projectFile)
        {
            var projectText = Encoding.UTF8.GetString(projectFile.Value);
            var assemblyName = Regex.Match(
                projectText,
                @"<AssemblyName>\s*([^<]+?)\s*</AssemblyName>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return assemblyName.Success
                ? assemblyName.Groups[1].Value.Trim()
                : Path.GetFileNameWithoutExtension(projectFile.Key);
        }

        private static IEnumerable<ConstantSourceEntry> DeclaredConstants(
            KeyValuePair<string, byte[]> file,
            string projectDirectory)
        {
            var source = StripCommentsAndLiterals(Encoding.UTF8.GetString(file.Value));
            var namespaceMatch = Regex.Match(
                source,
                @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
                RegexOptions.CultureInvariant);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
            var typeScopes = ReadTypeScopes(source);
            if (typeScopes.Count == 0)
            {
                yield break;
            }
            foreach (Match declaration in Regex.Matches(
                source,
                @"\bconst\s+[A-Za-z_][A-Za-z0-9_.<>?\[\]]*\s+([^;]+);",
                RegexOptions.CultureInvariant))
            {
                var declaringType = typeScopes
                    .Where(scope => scope.OpeningBrace < declaration.Index && declaration.Index < scope.ClosingBrace)
                    .OrderByDescending(scope => scope.OpeningBrace)
                    .FirstOrDefault();
                if (declaringType is null)
                {
                    continue;
                }

                foreach (Match declarator in Regex.Matches(
                    declaration.Groups[1].Value,
                    @"(?:^|,)\s*([A-Za-z_][A-Za-z0-9_]*)\s*=",
                    RegexOptions.CultureInvariant))
                {
                    var qualifiedTypeName = QualifiedSourceTypeName(
                        namespaceName,
                        declaringType,
                        typeScopes);
                    yield return new ConstantSourceEntry(
                        declarator.Groups[1].Value,
                        declaringType.Name,
                        qualifiedTypeName,
                        projectDirectory,
                        file);
                }
            }

            foreach (var enumScope in typeScopes.Where(scope => scope.IsEnum))
            {
                var body = source.Substring(
                    enumScope.OpeningBrace + 1,
                    enumScope.ClosingBrace - enumScope.OpeningBrace - 1);
                foreach (Match memberMatch in Regex.Matches(
                    body,
                    @"(?:^|,)\s*(?:\[[^\]]+\]\s*)*([A-Za-z_][A-Za-z0-9_]*)\s*(?:=[^,]*)?(?=,|$)",
                    RegexOptions.CultureInvariant))
                {
                    var qualifiedTypeName = QualifiedSourceTypeName(
                        namespaceName,
                        enumScope,
                        typeScopes);
                    yield return new ConstantSourceEntry(
                        memberMatch.Groups[1].Value,
                        enumScope.Name,
                        qualifiedTypeName,
                        projectDirectory,
                        file);
                }
            }
        }

        private static int FindMatchingBrace(string source, int openingBrace)
        {
            if (openingBrace < 0)
            {
                return -1;
            }

            var depth = 0;
            for (var index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}' && --depth == 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static IReadOnlyList<TypeScope> ReadTypeScopes(string source) =>
            Regex.Matches(
                    source,
                    @"\b(class|struct|interface|enum|record(?:\s+class|\s+struct)?)\s+([A-Za-z_][A-Za-z0-9_]*)",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match =>
                {
                    var openingBrace = source.IndexOf('{', match.Index + match.Length);
                    return new TypeScope(
                        match.Groups[2].Value,
                        string.Equals(match.Groups[1].Value, "enum", StringComparison.Ordinal),
                        openingBrace,
                        FindMatchingBrace(source, openingBrace));
                })
                .Where(scope => scope.OpeningBrace >= 0 && scope.ClosingBrace > scope.OpeningBrace)
                .ToArray();

        private static string QualifiedSourceTypeName(
            string namespaceName,
            TypeScope scope,
            IReadOnlyList<TypeScope> scopes)
        {
            var containingTypes = scopes
                .Where(candidate =>
                    candidate.OpeningBrace < scope.OpeningBrace &&
                    scope.ClosingBrace < candidate.ClosingBrace)
                .OrderBy(candidate => candidate.OpeningBrace)
                .Select(candidate => candidate.Name)
                .Append(scope.Name);
            var typePath = string.Join(".", containingTypes);
            return string.IsNullOrEmpty(namespaceName) ? typePath : namespaceName + "." + typePath;
        }

        private static IEnumerable<InheritanceEntry> ReadInheritance(string source)
        {
            var namespaceMatch = Regex.Match(
                source,
                @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
                RegexOptions.CultureInvariant);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
            foreach (Match match in Regex.Matches(
                source,
                @"\b(?:class|record(?:\s+class)?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*([^\{]+)\{",
                RegexOptions.CultureInvariant))
            {
                var derivedTypeName = QualifyTypeName(namespaceName, match.Groups[1].Value);
                yield return new InheritanceEntry(
                    derivedTypeName,
                    match.Groups[2].Value
                        .Split(',')
                        .Select(value => value.Trim())
                        .Select(value => value.Split('<')[0])
                        .Select(value => value.Contains('.', StringComparison.Ordinal)
                            ? value
                            : QualifyTypeName(namespaceName, value))
                        .Where(value => value.Length != 0)
                        .ToArray());
            }
        }

        private static IEnumerable<string> ReadQualifiedTypeNames(string source)
        {
            var namespaceMatch = Regex.Match(
                source,
                @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
                RegexOptions.CultureInvariant);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
            var scopes = ReadTypeScopes(source);
            return scopes.Select(scope => QualifiedSourceTypeName(namespaceName, scope, scopes));
        }

        private static string QualifyTypeName(string namespaceName, string typeName) =>
            string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;

        public bool AddReferencedConstantSources(IDictionary<string, byte[]> selected)
        {
            var pending = new Queue<KeyValuePair<string, byte[]>>(selected);
            var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var isComplete = true;
            while (pending.Count != 0)
            {
                var sourceFile = pending.Dequeue();
                if (!scanned.Add(sourceFile.Key) ||
                    !string.Equals(Path.GetExtension(sourceFile.Key), ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var source = _strippedSourceByPath[sourceFile.Key];
                var sourceProject = FindProjectDirectory(sourceFile.Key);
                if (sourceProject is null)
                {
                    continue;
                }

                var bindings = ConstantBindingsForProject(sourceProject);
                foreach (var binding in bindings)
                {
                    var candidates = binding.Candidates;
                    var constant = candidates[0];
                    if (candidates.Any(candidate => string.Equals(
                        candidate.File.Key,
                        sourceFile.Key,
                        StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var cacheKey = sourceFile.Key + "|" + binding.Identity;
                    if (!_constantReferenceCache.TryGetValue(cacheKey, out var reference))
                    {
                        reference = ResolveConstantReference(source, sourceProject, constant, bindings);
                        _constantReferenceCache[cacheKey] = reference;
                    }

                    if (reference == ConstantReference.Ambiguous)
                    {
                        isComplete = false;
                        continue;
                    }

                    if (reference == ConstantReference.None)
                    {
                        continue;
                    }

                    var projectCount = candidates
                        .Select(candidate => candidate.ProjectDirectory)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();
                    if (projectCount != 1)
                    {
                        isComplete = false;
                        continue;
                    }

                    foreach (var candidate in candidates)
                    {
                        if (!selected.ContainsKey(candidate.File.Key))
                        {
                            selected[candidate.File.Key] = candidate.File.Value;
                            pending.Enqueue(candidate.File);
                        }
                    }
                }
            }

            return isComplete;
        }

        private IReadOnlyList<ConstantBinding> ConstantBindingsForProject(string projectDirectory)
        {
            if (_constantBindingsByProject.TryGetValue(projectDirectory, out var cached))
            {
                return cached;
            }

            var reachableProjects = ReachableProjectDirectories(projectDirectory);
            var bindings = _constants
                .Where(constant => reachableProjects.Contains(constant.ProjectDirectory))
                .GroupBy(
                    constant => constant.QualifiedTypeName + "|" + constant.MemberName,
                    StringComparer.Ordinal)
                .Select(group => new ConstantBinding(group.Key, group.ToArray()))
                .ToArray();
            _constantBindingsByProject[projectDirectory] = bindings;
            return bindings;
        }

        private ConstantReference ResolveConstantReference(
            string source,
            string sourceProject,
            ConstantSourceEntry constant,
            IReadOnlyList<ConstantBinding> bindings)
        {
            var member = Regex.Escape(constant.MemberName);
            var type = Regex.Escape(constant.TypeName);
            var qualifiedType = Regex.Escape(constant.QualifiedTypeName);
            if (_globalStaticImportsByProject.TryGetValue(sourceProject, out var globalImports) &&
                Regex.IsMatch(source, @"\b" + member + @"\b", RegexOptions.CultureInvariant))
            {
                if (globalImports.Contains(constant.QualifiedTypeName, StringComparer.Ordinal))
                {
                    return ConstantReference.Exact;
                }

                if (globalImports.Any(import =>
                    string.Equals(import, constant.TypeName, StringComparison.Ordinal)))
                {
                    var matchingBindings = bindings.Count(binding =>
                        string.Equals(binding.Candidates[0].TypeName, constant.TypeName, StringComparison.Ordinal) &&
                        string.Equals(binding.Candidates[0].MemberName, constant.MemberName, StringComparison.Ordinal));
                    return matchingBindings == 1
                        ? ConstantReference.Exact
                        : ConstantReference.Ambiguous;
                }
            }
            if (Regex.IsMatch(
                source,
                @"\b" + qualifiedType + @"\s*\.\s*" + member + @"\b",
                RegexOptions.CultureInvariant))
            {
                return ConstantReference.Exact;
            }

            if (Regex.IsMatch(
                    source,
                    @"\b(?:global\s+)?using\s+static\s+" + qualifiedType + @"\s*;",
                    RegexOptions.CultureInvariant) &&
                Regex.IsMatch(source, @"\b" + member + @"\b", RegexOptions.CultureInvariant))
            {
                return ConstantReference.Exact;
            }

            if (Regex.IsMatch(
                    source,
                    @"\b(?:global\s+)?using\s+static\s+" + type + @"\s*;",
                    RegexOptions.CultureInvariant) &&
                Regex.IsMatch(source, @"\b" + member + @"\b", RegexOptions.CultureInvariant))
            {
                var matchingBindings = bindings.Count(binding =>
                    string.Equals(binding.Candidates[0].TypeName, constant.TypeName, StringComparison.Ordinal) &&
                    string.Equals(binding.Candidates[0].MemberName, constant.MemberName, StringComparison.Ordinal));
                return matchingBindings == 1
                    ? ConstantReference.Exact
                    : ConstantReference.Ambiguous;
            }

            foreach (Match alias in Regex.Matches(
                source,
                @"\busing\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*" + qualifiedType + @"\s*;",
                RegexOptions.CultureInvariant))
            {
                if (Regex.IsMatch(
                    source,
                    @"\b" + Regex.Escape(alias.Groups[1].Value) + @"\s*\.\s*" + member + @"\b",
                    RegexOptions.CultureInvariant))
                {
                    return ConstantReference.Exact;
                }
            }

            foreach (Match access in Regex.Matches(
                source,
                @"\b([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*\.\s*" + member + @"\b",
                RegexOptions.CultureInvariant))
            {
                var accessedType = access.Groups[1].Value;
                var resolvedTypes = ResolveTypeReference(source, accessedType);
                if (resolvedTypes.Count > 1)
                {
                    return ConstantReference.Ambiguous;
                }

                if (resolvedTypes.Count == 0)
                {
                    continue;
                }

                var resolvedType = resolvedTypes[0];
                var directBinding = bindings.FirstOrDefault(binding =>
                    string.Equals(
                        binding.Candidates[0].QualifiedTypeName,
                        resolvedType,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.Candidates[0].MemberName,
                        constant.MemberName,
                        StringComparison.Ordinal));
                if (directBinding is not null)
                {
                    if (string.Equals(
                        directBinding.Candidates[0].QualifiedTypeName,
                        constant.QualifiedTypeName,
                        StringComparison.Ordinal))
                    {
                        return ConstantReference.Exact;
                    }

                    continue;
                }

                if (IsDerivedFrom(resolvedType, constant.QualifiedTypeName))
                {
                    return ConstantReference.Exact;
                }
            }

            if (Regex.IsMatch(source, @"\b" + member + @"\b", RegexOptions.CultureInvariant) &&
                ReadInheritance(source).Any(item => IsDerivedFrom(
                    item.DerivedTypeName,
                    constant.QualifiedTypeName)))
            {
                return ConstantReference.Exact;
            }

            return ConstantReference.None;
        }

        private bool IsDerivedFrom(string derivedTypeName, string baseTypeName)
        {
            var roots = derivedTypeName.Contains('.', StringComparison.Ordinal)
                ? new[] { derivedTypeName }
                : _knownTypeNames
                    .Where(name => name.EndsWith("." + derivedTypeName, StringComparison.Ordinal) ||
                                   string.Equals(name, derivedTypeName, StringComparison.Ordinal))
                    .ToArray();
            if (roots.Length != 1)
            {
                return false;
            }

            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(roots[0]);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current) || !_baseTypeNamesByTypeName.TryGetValue(current, out var bases))
                {
                    continue;
                }

                if (bases.Contains(baseTypeName, StringComparer.Ordinal))
                {
                    return true;
                }

                foreach (var candidate in bases)
                {
                    pending.Push(candidate);
                }
            }

            return false;
        }

        private IReadOnlyList<string> ResolveTypeReference(string source, string typeReference)
        {
            var normalizedReference = typeReference.StartsWith("global::", StringComparison.Ordinal)
                ? typeReference.Substring("global::".Length)
                : typeReference;
            var aliases = Regex.Matches(
                    source,
                    @"\busing\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global::)?([A-Za-z_][A-Za-z0-9_.]*)\s*;",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Where(match => string.Equals(
                    match.Groups[1].Value,
                    normalizedReference,
                    StringComparison.Ordinal))
                .Select(match => match.Groups[2].Value)
                .Where(_knownTypeNames.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (aliases.Length != 0)
            {
                return aliases;
            }

            var namespaceMatch = Regex.Match(
                source,
                @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
                RegexOptions.CultureInvariant);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;
            var imports = Regex.Matches(
                    source,
                    @"\b(?:global\s+)?using\s+(?!static\b)([A-Za-z_][A-Za-z0-9_.]*)\s*;",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value);
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            AddKnownType(candidates, normalizedReference);
            if (!string.IsNullOrEmpty(namespaceName))
            {
                AddKnownType(candidates, namespaceName + "." + normalizedReference);
            }

            foreach (var import in imports)
            {
                AddKnownType(candidates, import + "." + normalizedReference);
            }

            if (candidates.Count == 0 && !normalizedReference.Contains('.', StringComparison.Ordinal))
            {
                foreach (var knownType in _knownTypeNames.Where(name =>
                    name.EndsWith("." + normalizedReference, StringComparison.Ordinal) ||
                    string.Equals(name, normalizedReference, StringComparison.Ordinal)))
                {
                    candidates.Add(knownType);
                }
            }

            return candidates.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private void AddKnownType(ISet<string> candidates, string typeName)
        {
            if (_knownTypeNames.Contains(typeName))
            {
                candidates.Add(typeName);
            }
        }

        private ISet<string> ReachableProjectDirectories(string projectDirectory)
        {
            if (_reachableProjectsByDirectory.TryGetValue(projectDirectory, out var cached))
            {
                return cached;
            }

            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(projectDirectory);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                if (!reachable.Add(current) || !_projectFilesByDirectory.TryGetValue(current, out var projectFiles))
                {
                    continue;
                }

                foreach (var projectFile in projectFiles)
                {
                    var projectText = Encoding.UTF8.GetString(projectFile.Value);
                    foreach (Match match in Regex.Matches(
                        projectText,
                        "<ProjectReference\\s+Include=\"([^\"]+)\"",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        var referencedPath = NormalizeRelativePath(
                            current + "/" + match.Groups[1].Value.Replace('\\', '/'));
                        var referencedDirectory = DirectoryName(referencedPath);
                        if (_projectFilesByDirectory.ContainsKey(referencedDirectory))
                        {
                            pending.Push(referencedDirectory);
                        }
                    }
                }
            }

            _reachableProjectsByDirectory[projectDirectory] = reachable;
            return reachable;
        }

        private static string StripCommentsAndLiterals(string source)
        {
            var result = source.ToCharArray();
            var index = 0;
            while (index < result.Length)
            {
                if (index + 1 < result.Length && result[index] == '/' && result[index + 1] == '/')
                {
                    BlankUntilLineEnd(result, ref index);
                    continue;
                }

                if (index + 1 < result.Length && result[index] == '/' && result[index + 1] == '*')
                {
                    BlankUntil(result, ref index, '*', '/');
                    continue;
                }

                if (result[index] == '@' && index + 1 < result.Length && result[index + 1] == '"')
                {
                    result[index++] = ' ';
                    BlankQuoted(result, ref index, '"', doubledQuoteEscapes: true);
                    continue;
                }

                if (result[index] == '"' || result[index] == '\'')
                {
                    BlankQuoted(result, ref index, result[index], doubledQuoteEscapes: false);
                    continue;
                }

                index++;
            }

            return new string(result);
        }

        private static void BlankUntilLineEnd(char[] text, ref int index)
        {
            while (index < text.Length && text[index] != '\r' && text[index] != '\n')
            {
                text[index++] = ' ';
            }
        }

        private static void BlankUntil(char[] text, ref int index, char terminatorFirst, char terminatorSecond)
        {
            text[index++] = ' ';
            text[index++] = ' ';
            while (index < text.Length)
            {
                if (index + 1 < text.Length && text[index] == terminatorFirst && text[index + 1] == terminatorSecond)
                {
                    text[index++] = ' ';
                    text[index++] = ' ';
                    return;
                }

                if (text[index] != '\r' && text[index] != '\n')
                {
                    text[index] = ' ';
                }

                index++;
            }
        }

        private static void BlankQuoted(
            char[] text,
            ref int index,
            char quote,
            bool doubledQuoteEscapes)
        {
            text[index++] = ' ';
            while (index < text.Length)
            {
                if (text[index] == quote)
                {
                    text[index++] = ' ';
                    if (doubledQuoteEscapes && index < text.Length && text[index] == quote)
                    {
                        text[index++] = ' ';
                        continue;
                    }

                    return;
                }

                if (!doubledQuoteEscapes && text[index] == '\\' && index + 1 < text.Length)
                {
                    text[index++] = ' ';
                    text[index++] = ' ';
                    continue;
                }

                if (text[index] != '\r' && text[index] != '\n')
                {
                    text[index] = ' ';
                }

                index++;
            }
        }

        public bool AddProjectBoundaryForHint(
            string memberHint,
            IDictionary<string, byte[]> selected)
        {
            var sourceMatches = _files
                .Where(file =>
                    string.Equals(Path.GetExtension(file.Key), ".cs", StringComparison.OrdinalIgnoreCase) &&
                    memberHint.IndexOf(
                        Path.GetFileNameWithoutExtension(file.Key),
                        StringComparison.Ordinal) >= 0)
                .ToArray();
            if (sourceMatches.Length == 0)
            {
                return false;
            }

            foreach (var sourceMatch in sourceMatches)
            {
                AddProjectBoundary(sourceMatch.Key, selected);
            }

            return true;
        }

        public bool AddClosedProjectBoundary(
            Type? declaringType,
            IDictionary<string, byte[]> selected)
        {
            var source = Find(declaringType);
            if (source.IsAmbiguous || source.Files.Count == 0)
            {
                return false;
            }

            foreach (var file in source.Files)
            {
                AddProjectBoundary(file.Key, selected);
            }

            return true;
        }

        public void AddProjectBoundary(
            string sourcePath,
            IDictionary<string, byte[]> selected)
        {
            var projectDirectory = FindProjectDirectory(sourcePath);
            if (projectDirectory is null)
            {
                return;
            }

            AddProjectBoundary(projectDirectory, selected, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private void AddProjectBoundary(
            string projectDirectory,
            IDictionary<string, byte[]> selected,
            ISet<string> visited)
        {
            if (!visited.Add(projectDirectory))
            {
                return;
            }

            var projectPrefix = projectDirectory + "/";
            foreach (var file in _files.Where(file =>
                file.Key.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) &&
                FindProjectDirectory(file.Key) == projectDirectory))
            {
                selected[file.Key] = file.Value;
            }

            if (!_projectFilesByDirectory.TryGetValue(projectDirectory, out var projectFiles))
            {
                return;
            }

            foreach (var projectFile in projectFiles)
            {
                var projectText = Encoding.UTF8.GetString(projectFile.Value);
                foreach (Match match in Regex.Matches(
                    projectText,
                    "<ProjectReference\\s+Include=\"([^\"]+)\"",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    var referencedPath = NormalizeRelativePath(
                        projectDirectory + "/" + match.Groups[1].Value.Replace('\\', '/'));
                    var referencedDirectory = DirectoryName(referencedPath);
                    if (_projectFilesByDirectory.ContainsKey(referencedDirectory))
                    {
                        AddProjectBoundary(referencedDirectory, selected, visited);
                    }
                }
            }
        }

        private string? FindProjectDirectory(string path)
        {
            for (var directory = DirectoryName(path);
                !string.IsNullOrEmpty(directory);
                directory = DirectoryName(directory))
            {
                if (_projectFilesByDirectory.ContainsKey(directory))
                {
                    return directory;
                }
            }

            return null;
        }

        private static string DirectoryName(string path)
        {
            var separator = path.LastIndexOf('/');
            return separator < 0 ? string.Empty : path.Substring(0, separator);
        }

        private static string NormalizeRelativePath(string path)
        {
            var segments = new List<string>();
            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count != 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    continue;
                }

                segments.Add(segment);
            }

            return string.Join("/", segments);
        }

        public SourceLookup Find(Type? declaringType)
        {
            if (declaringType is null)
            {
                return new SourceLookup(Array.Empty<KeyValuePair<string, byte[]>>(), false);
            }

            var outermostType = declaringType;
            while (outermostType.DeclaringType is not null)
            {
                outermostType = outermostType.DeclaringType;
            }

            var typeName = outermostType.Name;
            var genericMarker = typeName.IndexOf('`');
            if (genericMarker >= 0)
            {
                typeName = typeName.Substring(0, genericMarker);
            }

            var qualifiedName = string.IsNullOrEmpty(outermostType.Namespace)
                ? typeName
                : outermostType.Namespace + "." + typeName;
            var identity = outermostType.Assembly.GetName().Name + "|" + qualifiedName;
            if (!_filesByTypeIdentity.TryGetValue(identity, out var entries))
            {
                return new SourceLookup(Array.Empty<KeyValuePair<string, byte[]>>(), false);
            }

            var projectCount = entries
                .Select(entry => entry.ProjectDirectory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            return new SourceLookup(
                entries.Select(entry => entry.File).Distinct().ToArray(),
                projectCount != 1);
        }

        private sealed record SourceTypeEntry(
            string Identity,
            string ProjectDirectory,
            KeyValuePair<string, byte[]> File);

        private sealed record ConstantSourceEntry(
            string MemberName,
            string TypeName,
            string QualifiedTypeName,
            string ProjectDirectory,
            KeyValuePair<string, byte[]> File);

        private sealed record ConstantBinding(
            string Identity,
            IReadOnlyList<ConstantSourceEntry> Candidates);

        private sealed record TypeScope(
            string Name,
            bool IsEnum,
            int OpeningBrace,
            int ClosingBrace);

        private sealed record InheritanceEntry(
            string DerivedTypeName,
            IReadOnlyList<string> BaseTypeNames);

        private enum ConstantReference
        {
            None,
            Exact,
            Ambiguous,
        }

        public sealed record SourceLookup(
            IReadOnlyList<KeyValuePair<string, byte[]>> Files,
            bool IsAmbiguous);
    }

    private sealed record ReviewedCodeSelection(
        IReadOnlyDictionary<string, byte[]> Files,
        bool IsComplete);

    private sealed record ReviewedCodeFingerprint(string Fingerprint, bool IsComplete);

    private sealed record ReferencedMethodReadResult(
        IReadOnlyList<MethodBase> Methods,
        IReadOnlyList<Type> Types,
        bool IsComplete,
        IReadOnlyList<string> UnresolvedMemberHints);

    private static IReadOnlyList<string> ReadCategories(MethodInfo method) =>
        CustomAttributeData.GetCustomAttributes(method.DeclaringType!)
            .Concat(CustomAttributeData.GetCustomAttributes(method))
            .SelectMany(ReadAttributeCategories)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> ReadAttributeCategories(CustomAttributeData attribute)
    {
        if (string.Equals(attribute.AttributeType.FullName, "Xunit.TraitAttribute", StringComparison.Ordinal) &&
            attribute.ConstructorArguments.Count == 2 &&
            string.Equals(attribute.ConstructorArguments[0].Value as string, "Category", StringComparison.Ordinal) &&
            attribute.ConstructorArguments[1].Value is string traitCategory)
        {
            yield return traitCategory;
        }

        if (!string.Equals(attribute.AttributeType.Name, "SqlServerFactAttribute", StringComparison.Ordinal))
        {
            yield break;
        }

        if (attribute.ConstructorArguments.Count == 0 ||
            attribute.ConstructorArguments[0].Value is not IReadOnlyCollection<CustomAttributeTypedArgument> arguments ||
            arguments.Count == 0)
        {
            yield return SqlTestCategories.FastSql;
            yield break;
        }

        foreach (var argument in arguments)
        {
            if (argument.Value is string category)
            {
                yield return category;
            }
        }
    }
}

public sealed record TestMethodDescriptor(
    string FullyQualifiedName,
    bool IsTheory,
    string BodyFingerprint,
    IReadOnlyList<string> Categories,
    bool IsBodyFingerprintComplete = true)
{
    public string NormalizedSignature { get; init; } = FullyQualifiedName + "()";

    public TestMethodDescriptor(
        string FullyQualifiedName,
        bool IsTheory,
        string BodyFingerprint)
        : this(FullyQualifiedName, IsTheory, BodyFingerprint, Array.Empty<string>(), true)
    {
    }
}
