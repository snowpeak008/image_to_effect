using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class NoProjectAccessSurfaceTests
{
    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    private static readonly Assembly[] ProductAssemblies =
    [
        typeof(VfxComposerClient).Assembly,
        typeof(MainWindowViewModel).Assembly,
    ];

    // U4 permits this exact Client-only process/control-pipe implementation.
    // The Desktop assembly and every other Client type remain fully inspected.
    private static readonly string[] UserModeClientInfrastructureAllowlist =
    [
        "VFXComposer.Client.IUserModeBrokerProcessHost",
        "VFXComposer.Client.UserModeBrokerProcessHost",
        "VFXComposer.Client.UserModeDesktopSession",
    ];

    private static readonly string[] ProhibitedIdentifierFragments =
    [
        "AbsolutePath",
        "AssetDatabase",
        "AssetsPath",
        "CallerPath",
        "DirectoryPath",
        "EditorPrefs",
        "Environment",
        "FilePath",
        "FullPath",
        "Http",
        "Listen",
        "NamedPipe",
        "Network",
        "PackagesPath",
        "Pipe",
        "ProjectDirectory",
        "ProjectFolder",
        "ProjectLocation",
        "ProjectPath",
        "ProjectRoot",
        "ProjectSettingsPath",
        "RootPath",
        "Socket",
        "Tcp",
        "Udp",
        "Unity",
    ];

    private static readonly string[] ProhibitedLiteralFragments =
    [
        "System.Environment",
        "System.IO.",
        "System.Net.",
        "UnityEditor.",
        "UnityEngine.",
        "AssetDatabase",
        "EditorPrefs",
        "NamedPipe",
        "ProjectDirectory",
        "ProjectSettingsPath",
        "ProjectPath",
    ];

    private static readonly OpCode[] OneByteOpCodes = BuildOpCodeMap(twoByte: false);
    private static readonly OpCode[] TwoByteOpCodes = BuildOpCodeMap(twoByte: true);

    [TestMethod]
    public void EntireProductAssembliesContainNoProjectIoListenerOrUnityAccess()
    {
        var violations = ProductAssemblies
            .Distinct()
            .SelectMany(ScanAssembly)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            violations.Length,
            $"Product assembly access-surface violations:\n{string.Join("\n", violations)}");
    }

    [TestMethod]
    public void ScannerInspectsPrivateMethodBodiesAndIlMemberReferences()
    {
        var fixtureMethod = typeof(NoProjectAccessSurfaceTests).GetMethod(
            nameof(ProhibitedPrivateFixture),
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var violations = ScanMethodBody(fixtureMethod, fixtureMethod.Name).ToArray();

        Assert.IsTrue(
            violations.Any(violation => violation.Contains(
                "prohibited type reference System.IO.File",
                StringComparison.Ordinal)),
            "The access-surface scanner must resolve prohibited member references in private method IL.");
    }

    [TestMethod]
    public void U4ClientInfrastructureAllowlistIsExactAndClosed()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "VFXComposer.Client.IUserModeBrokerProcessHost",
                "VFXComposer.Client.UserModeBrokerProcessHost",
                "VFXComposer.Client.UserModeDesktopSession",
            },
            UserModeClientInfrastructureAllowlist);
        Assert.IsTrue(UserModeClientInfrastructureAllowlist.All(name =>
            name.StartsWith("VFXComposer.Client.", StringComparison.Ordinal)));
    }

    private static IEnumerable<string> ScanAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? "<unnamed>";
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (reference.Name?.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) == true)
            {
                yield return $"{assemblyName}: prohibited Unity assembly reference {reference.FullName}.";
            }
        }

        foreach (var type in assembly.GetTypes())
        {
            if (IsAllowlistedUserModeClientInfrastructure(type))
            {
                continue;
            }

            foreach (var violation in ScanType(type))
            {
                yield return $"{assemblyName}: {violation}";
            }
        }
    }

    private static bool IsAllowlistedUserModeClientInfrastructure(Type type)
    {
        var fullName = type.FullName;
        return fullName is not null && UserModeClientInfrastructureAllowlist.Any(allowlisted =>
            string.Equals(fullName, allowlisted, StringComparison.Ordinal) ||
            fullName.StartsWith(allowlisted + "+", StringComparison.Ordinal));
    }

    private static IEnumerable<string> ScanType(Type type)
    {
        var context = type.FullName ?? type.Name;
        foreach (var violation in CheckIdentifier(type.Name, context))
        {
            yield return violation;
        }

        foreach (var violation in CheckTypeReference(type.BaseType, $"{context} base type"))
        {
            yield return violation;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            foreach (var violation in CheckTypeReference(interfaceType, $"{context} interface"))
            {
                yield return violation;
            }
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            if (!genericArgument.IsGenericParameter)
            {
                continue;
            }

            foreach (var constraint in genericArgument.GetGenericParameterConstraints())
            {
                foreach (var violation in CheckTypeReference(constraint, $"{context} generic constraint"))
                {
                    yield return violation;
                }
            }
        }

        foreach (var violation in ScanCustomAttributes(type.CustomAttributes, $"{context} attribute"))
        {
            yield return violation;
        }

        foreach (var field in type.GetFields(AllDeclaredMembers))
        {
            foreach (var violation in CheckMember(field, $"{context}.{field.Name}"))
            {
                yield return violation;
            }
        }

        foreach (var property in type.GetProperties(AllDeclaredMembers))
        {
            foreach (var violation in CheckMember(property, $"{context}.{property.Name}"))
            {
                yield return violation;
            }
        }

        foreach (var eventInfo in type.GetEvents(AllDeclaredMembers))
        {
            foreach (var violation in CheckMember(eventInfo, $"{context}.{eventInfo.Name}"))
            {
                yield return violation;
            }
        }

        var methods = type.GetMethods(AllDeclaredMembers)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(AllDeclaredMembers));

        foreach (var method in methods)
        {
            var methodContext = $"{context}.{method.Name}";
            foreach (var violation in CheckMember(method, methodContext))
            {
                yield return violation;
            }

            foreach (var violation in ScanMethodBody(method, methodContext))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<string> ScanMethodBody(MethodBase method, string context)
    {
        if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
        {
            yield return $"{context}: native P/Invoke is prohibited in the Phase 1 product surface.";
        }

        MethodBody? body = null;
        string? bodyInspectionError = null;
        try
        {
            body = method.GetMethodBody();
        }
        catch (InvalidOperationException exception)
        {
            bodyInspectionError = exception.GetType().Name;
        }

        if (bodyInspectionError is not null)
        {
            yield return $"{context}: method body could not be inspected ({bodyInspectionError}).";
            yield break;
        }

        if (body is null)
        {
            yield break;
        }

        foreach (var local in body.LocalVariables)
        {
            foreach (var violation in CheckTypeReference(local.LocalType, $"{context} local"))
            {
                yield return violation;
            }
        }

        foreach (var clause in body.ExceptionHandlingClauses)
        {
            if (clause.Flags != ExceptionHandlingClauseOptions.Clause)
            {
                continue;
            }

            foreach (var violation in CheckTypeReference(clause.CatchType, $"{context} catch"))
            {
                yield return violation;
            }
        }

        var il = body.GetILAsByteArray();
        if (il is null || il.Length == 0)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;
        var methodArguments = method.IsGenericMethod
            ? method.GetGenericArguments()
            : null;
        var offset = 0;

        while (offset < il.Length)
        {
            var instructionOffset = offset;
            if (!TryReadOpCode(il, ref offset, out var opCode))
            {
                yield return $"{context}: invalid IL opcode at offset {instructionOffset}.";
                yield break;
            }

            switch (opCode.OperandType)
            {
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    if (!TryReadInt32(il, ref offset, out var memberToken))
                    {
                        yield return $"{context}: truncated {opCode.Name} operand at offset {instructionOffset}.";
                        yield break;
                    }

                    MemberInfo? referencedMember = null;
                    string? memberResolutionError = null;
                    try
                    {
                        referencedMember = method.Module.ResolveMember(
                            memberToken,
                            typeArguments,
                            methodArguments);
                    }
                    catch (ArgumentException exception)
                    {
                        memberResolutionError = exception.GetType().Name;
                    }

                    if (memberResolutionError is not null)
                    {
                        yield return $"{context}: unresolved IL member token 0x{memberToken:x8} ({memberResolutionError}).";
                        continue;
                    }

                    if (referencedMember is null)
                    {
                        yield return $"{context}: unresolved IL member token 0x{memberToken:x8}.";
                        continue;
                    }

                    foreach (var violation in CheckMember(
                                 referencedMember,
                                 $"{context} IL_{instructionOffset:x4}"))
                    {
                        yield return violation;
                    }

                    break;

                case OperandType.InlineString:
                    if (!TryReadInt32(il, ref offset, out var stringToken))
                    {
                        yield return $"{context}: truncated string operand at offset {instructionOffset}.";
                        yield break;
                    }

                    string? literal = null;
                    string? stringResolutionError = null;
                    try
                    {
                        literal = method.Module.ResolveString(stringToken);
                    }
                    catch (ArgumentException exception)
                    {
                        stringResolutionError = exception.GetType().Name;
                    }

                    if (stringResolutionError is not null)
                    {
                        yield return $"{context}: unresolved IL string token 0x{stringToken:x8} ({stringResolutionError}).";
                        continue;
                    }

                    foreach (var violation in CheckLiteral(literal!, $"{context} IL_{instructionOffset:x4}"))
                    {
                        yield return violation;
                    }

                    break;

                case OperandType.InlineSig:
                    if (!TryAdvance(il, ref offset, sizeof(int)))
                    {
                        yield return $"{context}: truncated signature operand at offset {instructionOffset}.";
                        yield break;
                    }

                    yield return $"{context}: indirect call signature at IL_{instructionOffset:x4} cannot pass the Phase 1 fail-closed scan.";
                    break;

                case OperandType.InlineSwitch:
                    if (!TryReadInt32(il, ref offset, out var targetCount)
                        || targetCount < 0
                        || targetCount > (il.Length - offset) / sizeof(int))
                    {
                        yield return $"{context}: invalid switch operand at offset {instructionOffset}.";
                        yield break;
                    }

                    offset += targetCount * sizeof(int);
                    break;

                default:
                    var operandSize = GetFixedOperandSize(opCode.OperandType);
                    if (operandSize < 0 || !TryAdvance(il, ref offset, operandSize))
                    {
                        yield return $"{context}: unsupported or truncated {opCode.OperandType} operand at offset {instructionOffset}.";
                        yield break;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<string> CheckMember(MemberInfo member, string context)
    {
        foreach (var violation in CheckIdentifier(member.Name, context))
        {
            yield return violation;
        }

        foreach (var violation in CheckTypeReference(member.DeclaringType, $"{context} declaring type"))
        {
            yield return violation;
        }

        switch (member)
        {
            case FieldInfo field:
                foreach (var violation in CheckTypeReference(field.FieldType, $"{context} field type"))
                {
                    yield return violation;
                }

                break;

            case PropertyInfo property:
                foreach (var violation in CheckTypeReference(property.PropertyType, $"{context} property type"))
                {
                    yield return violation;
                }

                foreach (var parameter in property.GetIndexParameters())
                {
                    foreach (var violation in CheckParameter(parameter, context))
                    {
                        yield return violation;
                    }
                }

                break;

            case EventInfo eventInfo:
                foreach (var violation in CheckTypeReference(eventInfo.EventHandlerType, $"{context} event type"))
                {
                    yield return violation;
                }

                break;

            case MethodBase method:
                if (method is MethodInfo methodInfo)
                {
                    foreach (var violation in CheckTypeReference(methodInfo.ReturnType, $"{context} return type"))
                    {
                        yield return violation;
                    }

                    foreach (var genericArgument in methodInfo.GetGenericArguments())
                    {
                        if (!genericArgument.IsGenericParameter)
                        {
                            continue;
                        }

                        foreach (var constraint in genericArgument.GetGenericParameterConstraints())
                        {
                            foreach (var violation in CheckTypeReference(constraint, $"{context} generic constraint"))
                            {
                                yield return violation;
                            }
                        }
                    }
                }

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var violation in CheckParameter(parameter, context))
                    {
                        yield return violation;
                    }
                }

                break;

            case Type referencedType:
                foreach (var violation in CheckTypeReference(referencedType, context))
                {
                    yield return violation;
                }

                break;
        }

        foreach (var violation in ScanCustomAttributes(member.CustomAttributes, $"{context} attribute"))
        {
            yield return violation;
        }
    }

    private static IEnumerable<string> CheckParameter(ParameterInfo parameter, string context)
    {
        if (parameter.Name is not null)
        {
            foreach (var violation in CheckIdentifier(parameter.Name, $"{context} parameter"))
            {
                yield return violation;
            }
        }

        foreach (var violation in CheckTypeReference(parameter.ParameterType, $"{context} parameter type"))
        {
            yield return violation;
        }

        foreach (var violation in ScanCustomAttributes(parameter.CustomAttributes, $"{context} parameter attribute"))
        {
            yield return violation;
        }
    }

    private static IEnumerable<string> CheckTypeReference(Type? type, string context)
    {
        if (type is null)
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (var violation in CheckTypeReference(type.GetElementType(), context))
            {
                yield return violation;
            }

            yield break;
        }

        var typeName = type.FullName ?? type.Name;
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        if (typeName.StartsWith("System.IO.", StringComparison.Ordinal)
            || typeName.StartsWith("System.Net.", StringComparison.Ordinal)
            || string.Equals(typeName, "System.Environment", StringComparison.Ordinal)
            || typeName.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)
            || assemblyName.StartsWith("Unity", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{context}: prohibited type reference {typeName}.";
        }

        foreach (var violation in CheckIdentifier(type.Name, context))
        {
            yield return violation;
        }

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var violation in CheckTypeReference(genericArgument, context))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<string> ScanCustomAttributes(
        IEnumerable<CustomAttributeData> attributes,
        string context)
    {
        foreach (var attribute in attributes)
        {
            foreach (var violation in CheckTypeReference(attribute.AttributeType, context))
            {
                yield return violation;
            }

            foreach (var argument in attribute.ConstructorArguments)
            {
                foreach (var violation in ScanAttributeArgument(argument, context))
                {
                    yield return violation;
                }
            }

            foreach (var argument in attribute.NamedArguments)
            {
                foreach (var violation in CheckIdentifier(argument.MemberName, context))
                {
                    yield return violation;
                }

                foreach (var violation in ScanAttributeArgument(argument.TypedValue, context))
                {
                    yield return violation;
                }
            }
        }
    }

    private static IEnumerable<string> ScanAttributeArgument(
        CustomAttributeTypedArgument argument,
        string context)
    {
        foreach (var violation in CheckTypeReference(argument.ArgumentType, context))
        {
            yield return violation;
        }

        if (argument.Value is Type typeValue)
        {
            foreach (var violation in CheckTypeReference(typeValue, context))
            {
                yield return violation;
            }
        }
        else if (argument.Value is string stringValue)
        {
            foreach (var violation in CheckLiteral(stringValue, context))
            {
                yield return violation;
            }
        }
        else if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> values)
        {
            foreach (var value in values)
            {
                foreach (var violation in ScanAttributeArgument(value, context))
                {
                    yield return violation;
                }
            }
        }
    }

    private static IEnumerable<string> CheckIdentifier(string identifier, string context)
    {
        foreach (var fragment in ProhibitedIdentifierFragments)
        {
            if (identifier.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{context}: prohibited identifier fragment {fragment} in {identifier}.";
            }
        }
    }

    private static IEnumerable<string> CheckLiteral(string literal, string context)
    {
        foreach (var fragment in ProhibitedLiteralFragments)
        {
            if (literal.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{context}: prohibited literal fragment {fragment}.";
            }
        }

        if (literal.Contains("\\\\", StringComparison.Ordinal)
            || literal.Contains("Assets/", StringComparison.OrdinalIgnoreCase)
            || literal.Contains("Assets\\", StringComparison.OrdinalIgnoreCase)
            || literal.Contains("Packages/", StringComparison.OrdinalIgnoreCase)
            || literal.Contains("Packages\\", StringComparison.OrdinalIgnoreCase)
            || literal.Contains("ProjectSettings/", StringComparison.OrdinalIgnoreCase)
            || literal.Contains("ProjectSettings\\", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{context}: prohibited project-path-like literal.";
        }
    }

    private static bool TryReadOpCode(byte[] il, ref int offset, out OpCode opCode)
    {
        opCode = default;
        if (offset >= il.Length)
        {
            return false;
        }

        var first = il[offset++];
        if (first != 0xfe)
        {
            opCode = OneByteOpCodes[first];
            return opCode.Size != 0;
        }

        if (offset >= il.Length)
        {
            return false;
        }

        opCode = TwoByteOpCodes[il[offset++]];
        return opCode.Size != 0;
    }

    private static bool TryReadInt32(byte[] il, ref int offset, out int value)
    {
        value = 0;
        if (offset < 0 || il.Length - offset < sizeof(int))
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);
        return true;
    }

    private static bool TryAdvance(byte[] il, ref int offset, int count)
    {
        if (count < 0 || offset < 0 || il.Length - offset < count)
        {
            return false;
        }

        offset += count;
        return true;
    }

    private static int GetFixedOperandSize(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineI or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => -1,
    };

    private static OpCode[] BuildOpCodeMap(bool twoByte)
    {
        var map = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = unchecked((ushort)opCode.Value);
            if ((!twoByte && value <= byte.MaxValue)
                || (twoByte && (value & 0xff00) == 0xfe00))
            {
                map[value & byte.MaxValue] = opCode;
            }
        }

        return map;
    }

    private static bool ProhibitedPrivateFixture(string candidate) =>
        System.IO.File.Exists(candidate);
}
