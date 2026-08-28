using System.Runtime.InteropServices;

namespace VFXComposer.Broker.ServiceHost;

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate void ServiceMainCallback(uint argumentCount, nint argumentVector);

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate uint ServiceControlHandlerExCallback(
    uint control,
    uint eventType,
    nint eventData,
    nint context);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ServiceTableEntry
{
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? ServiceName;

    internal ServiceMainCallback? ServiceMain;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeServiceStatus
{
    internal uint ServiceType;
    internal uint CurrentState;
    internal uint ControlsAccepted;
    internal uint Win32ExitCode;
    internal uint ServiceSpecificExitCode;
    internal uint Checkpoint;
    internal uint WaitHint;
}

internal interface IWindowsScmApi
{
    bool StartServiceCtrlDispatcher(ServiceTableEntry[] dispatchTable);

    nint RegisterServiceCtrlHandlerEx(string serviceName, ServiceControlHandlerExCallback controlHandler);

    bool SetServiceStatus(nint serviceStatusHandle, NativeServiceStatus status);

    uint GetLastError();
}

internal sealed class WindowsScmNativeApi : IWindowsScmApi
{
    public bool StartServiceCtrlDispatcher(ServiceTableEntry[] dispatchTable) =>
        NativeScmMethods.StartServiceCtrlDispatcherW(dispatchTable);

    public nint RegisterServiceCtrlHandlerEx(string serviceName, ServiceControlHandlerExCallback controlHandler) =>
        NativeScmMethods.RegisterServiceCtrlHandlerExW(serviceName, controlHandler, 0);

    public bool SetServiceStatus(nint serviceStatusHandle, NativeServiceStatus status) =>
        NativeScmMethods.SetServiceStatus(serviceStatusHandle, ref status);

    public uint GetLastError() => NativeScmMethods.GetLastError();
}

internal static class NativeScmMethods
{
    [DllImport("advapi32.dll", EntryPoint = "StartServiceCtrlDispatcherW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool StartServiceCtrlDispatcherW([In] ServiceTableEntry[] dispatchTable);

    [DllImport("advapi32.dll", EntryPoint = "RegisterServiceCtrlHandlerExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint RegisterServiceCtrlHandlerExW(
        [MarshalAs(UnmanagedType.LPWStr)] string serviceName,
        ServiceControlHandlerExCallback controlHandler,
        nint context);

    [DllImport("advapi32.dll", EntryPoint = "SetServiceStatus", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetServiceStatus(nint serviceStatusHandle, ref NativeServiceStatus status);

    [DllImport("kernel32.dll", EntryPoint = "GetLastError", ExactSpelling = true)]
    internal static extern uint GetLastError();
}
