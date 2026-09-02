using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Desktop.Services;
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

    private const string PrivateImagePreviewDecoderType =
        "VFXComposer.Desktop.Services.PrivateImagePreviewDecoder";

    // The current-user UI preference document is the only Desktop storage. U4 forbids project access, not a per-user
    // preference: this store derives its location from local application data alone and has no API that can accept a
    // project path. Exactly this one type may therefore reference filesystem and local-application-data types; every
    // other rule (network, listeners, pipes, Unity, project-path-like literals) still applies inside it.
    private const string UiPreferencesStoreType =
        "VFXComposer.Desktop.Services.UiPreferencesStore";

    // ADR-008 §2.3: the launcher's whole authority is its own deployment directory (locating the fixed-name build
    // host executable) and its own child process. Exactly this one type may therefore reference System.IO types; the
    // System.Diagnostics.Process family it also uses is not in the prohibited set today, but the allowance is declared
    // here so a future tightening of the scan cannot drift it in silently. Every other rule (network, listeners,
    // pipes, Unity, project-path-like literals) still applies inside it, and no member accepts any location at all.
    private const string BuildHostLauncherType =
        "VFXComposer.Desktop.Services.BuildHostLauncher";

    // The declared allowance surface of the launcher exemption, asserted as an exact closed set below.
    private static readonly string[] BuildHostLauncherAllowedTypePrefixes =
    [
        "System.Diagnostics.Process",
        "System.IO.",
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

    [TestMethod]
    public void PrivatePreviewStreamAllowanceIsExactAndClosed()
    {
        var decoder = typeof(PrivateImagePreviewDecoder);
        Assert.AreEqual(PrivateImagePreviewDecoderType, decoder.FullName);
        CollectionAssert.AreEqual(
            new[] { "DecodeAsync" },
            decoder.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [TestMethod]
    public void PrivatePreviewStreamAllowanceRejectsTypesThatOnlyShareTheDecoderPrefix()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("VFXComposer.Desktop.Tests.PreviewScannerFixture"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("fixture");
        var typeBuilder = module.DefineType(
            PrivateImagePreviewDecoderType + "Shadow",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = typeBuilder.DefineMethod(
            "PassThrough",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            [typeof(System.IO.Stream)]);
        method.GetILGenerator().Emit(OpCodes.Ret);
        var shadow = typeBuilder.CreateType()!;

        var violations = ScanType(shadow).ToArray();

        Assert.IsTrue(
            violations.Any(violation => violation.Contains(
                "prohibited type reference System.IO.Stream",
                StringComparison.Ordinal)),
            "Only PrivateImagePreviewDecoder.DecodeAsync and its compiler-generated state machine may receive Stream.");
    }

    [TestMethod]
    public void CurrentUserPreferenceStorageAllowanceIsExactAndClosed()
    {
        Assert.AreEqual(UiPreferencesStoreType, typeof(UiPreferencesStore).FullName);

        Assert.IsTrue(IsCurrentUserPreferenceStorageContext(UiPreferencesStoreType + ".Save local"));
        Assert.IsTrue(IsCurrentUserPreferenceStorageContext(UiPreferencesStoreType + "+<>c.Save local"));
        Assert.IsFalse(IsCurrentUserPreferenceStorageContext(UiPreferencesStoreType + "Shadow.Save local"));
        Assert.IsFalse(IsCurrentUserPreferenceStorageContext(
            "VFXComposer.Desktop.ViewModels.MainWindowViewModel.Save local"));

        var storageContext = UiPreferencesStoreType + ".Save local";
        Assert.AreEqual(
            0,
            CheckTypeReference(typeof(System.IO.FileStream), storageContext).Count());

        // The exemption covers storage only. Everything else stays prohibited inside the store as well.
        Assert.IsTrue(CheckTypeReference(typeof(System.Net.Sockets.Socket), storageContext).Any());
        Assert.IsTrue(CheckTypeReference(typeof(System.IO.FileStream), "VFXComposer.Desktop.App.Save local").Any());
    }

    [TestMethod]
    public void BuildHostLauncherAllowanceIsExactAndClosed()
    {
        Assert.AreEqual(BuildHostLauncherType, typeof(BuildHostLauncher).FullName);

        // The declared allowance list itself is a closed set: exactly deployment-directory IO plus
        // the shell's own child process, nothing else.
        CollectionAssert.AreEquivalent(
            new[] { "System.Diagnostics.Process", "System.IO." },
            BuildHostLauncherAllowedTypePrefixes);

        Assert.IsTrue(IsBuildHostLauncherContext(BuildHostLauncherType + ".TryLaunch local"));
        Assert.IsTrue(IsBuildHostLauncherContext(BuildHostLauncherType + "+<>c__DisplayClass0_0.TryLaunch local"));
        Assert.IsFalse(IsBuildHostLauncherContext(BuildHostLauncherType + "Shadow.TryLaunch local"));
        Assert.IsFalse(IsBuildHostLauncherContext("VFXComposer.Desktop.ViewModels.CreateViewModel.TryLaunch local"));

        var launcherContext = BuildHostLauncherType + ".TryLaunch local";
        Assert.AreEqual(0, CheckTypeReference(typeof(System.IO.File), launcherContext).Count());
        Assert.AreEqual(0, CheckTypeReference(typeof(System.Diagnostics.Process), launcherContext).Count());
        Assert.AreEqual(0, CheckTypeReference(typeof(System.Diagnostics.ProcessStartInfo), launcherContext).Count());

        // The exemption covers exactly the declared surface. Network, pipes, Environment and
        // Unity-named types stay prohibited inside the launcher as well (ADR-008 §2.3 point 3).
        Assert.IsTrue(CheckTypeReference(typeof(System.Net.Sockets.Socket), launcherContext).Any());
        Assert.IsTrue(CheckTypeReference(typeof(System.Net.Http.HttpClient), launcherContext).Any());
        Assert.IsTrue(CheckTypeReference(typeof(System.IO.Pipes.NamedPipeServerStream), launcherContext).Any(),
            "Pipes are carved out of the IO allowance: no IPC surface may ride in on the launcher exemption.");
        Assert.IsTrue(CheckTypeReference(typeof(Environment), launcherContext).Any());

        // The allowance is context-bound: the same IO types stay prohibited everywhere else.
        Assert.IsTrue(CheckTypeReference(typeof(System.IO.File), "VFXComposer.Desktop.App.Run local").Any());
    }

    [TestMethod]
    public void BuildHostLauncherAllowanceRejectsTypesThatOnlyShareThePrefix()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("VFXComposer.Desktop.Tests.LauncherScannerFixture"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("fixture");
        var typeBuilder = module.DefineType(
            BuildHostLauncherType + "Shadow",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = typeBuilder.DefineMethod(
            "PassThrough",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            [typeof(System.IO.FileStream)]);
        method.GetILGenerator().Emit(OpCodes.Ret);
        var shadow = typeBuilder.CreateType()!;

        var violations = ScanType(shadow).ToArray();

        Assert.IsTrue(
            violations.Any(violation => violation.Contains(
                "prohibited type reference System.IO.FileStream",
                StringComparison.Ordinal)),
            "Only BuildHostLauncher itself and its compiler-generated nested types may receive the IO allowance.");
    }

    [TestMethod]
    public void BuildHostLauncherExposesNoCallerSuppliedLocationAndNoProjectLiteral()
    {
        // The launcher's inputs are identity strings only: no constructor or method parameter can carry a
        // location of any kind, so it is structurally unable to receive a project path.
        var parameters = typeof(BuildHostLauncher)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Concat(typeof(BuildHostLauncher)
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters()))
            .Where(parameter => parameter.ParameterType == typeof(string))
            .Select(parameter => parameter.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "canonicalSha256", "draftId" }, parameters);
        // The deployment name is pinned as data so a rename of the host project breaks this test
        // rather than silently orphaning already-deployed shells.
        StringAssert.StartsWith(BuildHostLauncher.HostExecutableName, "VFXComposer.BuildHost");
        StringAssert.EndsWith(BuildHostLauncher.HostExecutableName, ".exe");
    }

    [TestMethod]
    public void UiPreferenceStorageExposesNoCallerSuppliedProjectLocation()
    {
        // The store's only location input is its own storage directory, and it must be fully qualified: no overload
        // can be handed a project path, a relative path, or a document name.
        var parameters = typeof(UiPreferencesStore)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Concat(typeof(UiPreferencesStore)
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters()))
            .Where(parameter => parameter.ParameterType == typeof(string))
            .Select(parameter => parameter.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "storageDirectory" }, parameters);
        Assert.ThrowsExactly<ArgumentException>(() => new UiPreferencesStore("relative-location"));
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

        // The decoder is the only Desktop component allowed to receive the provider-issued in-memory stream. It still
        // receives no filesystem, environment, network, project, or Unity type exemption.
        if (type == typeof(System.IO.Stream) &&
            IsPrivatePreviewStreamContext(context))
        {
            yield break;
        }

        if (IsCurrentUserStorageType(type) && IsCurrentUserPreferenceStorageContext(context))
        {
            yield break;
        }

        // The launcher receives exactly its declared allowance (deployment-directory IO plus its own child process)
        // and nothing else: network, Unity, listeners and System.Environment stay prohibited inside it.
        if (IsBuildHostLauncherAllowedType(type) && IsBuildHostLauncherContext(context))
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

    private static bool IsCurrentUserStorageType(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        return typeName.StartsWith("System.IO.", StringComparison.Ordinal)
            || string.Equals(typeName, "System.Environment", StringComparison.Ordinal);
    }

    private static bool IsCurrentUserPreferenceStorageContext(string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.StartsWith(UiPreferencesStoreType, StringComparison.Ordinal))
        {
            return false;
        }

        // Only the store itself, its members and its compiler-generated nested types qualify: a type that merely
        // begins with the same name receives nothing.
        var index = UiPreferencesStoreType.Length;
        return index >= context.Length || context[index] is '.' or ' ' or '+';
    }

    private static bool IsBuildHostLauncherAllowedType(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        // Pipes are carved out before the IO prefix is consulted: the launcher allowance must not
        // become an IPC surface (ADR-008 §2.3 point 3 keeps pipes and listeners prohibited inside it).
        if (typeName.StartsWith("System.IO.Pipes.", StringComparison.Ordinal))
        {
            return false;
        }

        return BuildHostLauncherAllowedTypePrefixes.Any(prefix =>
            typeName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsBuildHostLauncherContext(string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.StartsWith(BuildHostLauncherType, StringComparison.Ordinal))
        {
            return false;
        }

        // Only the launcher itself, its members and its compiler-generated nested types (the Exited handler's
        // closure) qualify: a type that merely begins with the same name receives nothing.
        var index = BuildHostLauncherType.Length;
        return index >= context.Length || context[index] is '.' or ' ' or '+';
    }

    private static bool IsPrivatePreviewStreamContext(string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var directMethod = PrivateImagePreviewDecoderType + ".DecodeAsync";
        if (context.StartsWith(directMethod + " ", StringComparison.Ordinal))
        {
            return true;
        }

        // Roslyn emits the async state machine as PrivateImagePreviewDecoder+<DecodeAsync>d__<ordinal>. The scanner
        // must inspect that generated type because it owns the local Stream field and DisposeAsync call, but no type
        // that merely begins with the decoder's name receives this exemption.
        var stateMachinePrefix = PrivateImagePreviewDecoderType + "+<DecodeAsync>d__";
        if (!context.StartsWith(stateMachinePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var index = stateMachinePrefix.Length;
        var firstOrdinalDigit = index;
        while (index < context.Length && char.IsAsciiDigit(context[index]))
        {
            index++;
        }

        return index > firstOrdinalDigit &&
            index < context.Length &&
            (context[index] == '.' || context[index] == ' ');
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
