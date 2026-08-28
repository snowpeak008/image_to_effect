namespace VFXComposer.Broker.ServiceHost;

internal sealed class WindowsScmServiceHost : IDisposable
{
    internal const string FixedServiceName = "VFXComposerBrokerHost";

    private readonly IWindowsScmApi _native;
    private readonly ServiceLifecycle _lifecycle = new();
    private readonly object _transitionGate = new();
    private readonly ServiceMainCallback _serviceMainCallback;
    private readonly ServiceControlHandlerExCallback _controlHandlerCallback;
    private nint _serviceStatusHandle;
    private int _runStarted;
    private int _disposed;

    internal WindowsScmServiceHost(IWindowsScmApi native)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
        _serviceMainCallback = ServiceMain;
        _controlHandlerCallback = HandleControl;
    }

    internal ServiceLifecycleState State => _lifecycle.State;

    internal int Run()
    {
        if (Interlocked.CompareExchange(ref _runStarted, 1, 0) != 0)
        {
            return ServiceHostDiagnostics.FailClosedExitCode;
        }

        try
        {
            var dispatchTable = new[]
            {
                new ServiceTableEntry
                {
                    ServiceName = FixedServiceName,
                    ServiceMain = _serviceMainCallback,
                },
                default,
            };

            if (!_native.StartServiceCtrlDispatcher(dispatchTable))
            {
                _ = _native.GetLastError();
            }
        }
        catch (Exception)
        {
            // Native dispatch errors are deliberately collapsed to the fixed code.
        }
        finally
        {
            GC.KeepAlive(_serviceMainCallback);
            GC.KeepAlive(_controlHandlerCallback);
        }

        return ServiceHostDiagnostics.FailClosedExitCode;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            ApplyTransition(_lifecycle.RequestStop);
            ApplyTransition(() => _lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode));
        }
        catch (InvalidOperationException)
        {
            // The initial and terminal states are already stopped and need no work.
        }
        catch (Exception)
        {
            // Disposal must not let managed exceptions cross a native callback.
        }
        finally
        {
            GC.KeepAlive(_serviceMainCallback);
            GC.KeepAlive(_controlHandlerCallback);
        }
    }

    private void ServiceMain(uint argumentCount, nint argumentVector)
    {
        try
        {
            var serviceStatusHandle = _native.RegisterServiceCtrlHandlerEx(
                FixedServiceName,
                _controlHandlerCallback);
            if (serviceStatusHandle == 0)
            {
                _ = _native.GetLastError();
                return;
            }

            Interlocked.Exchange(ref _serviceStatusHandle, serviceStatusHandle);
            lock (_transitionGate)
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    TryReport(ServiceStatusSnapshot.For(
                        ServiceLifecycleState.Stopped,
                        ServiceHostDiagnostics.ServiceSpecificExitCode));
                    return;
                }

                TryReport(_lifecycle.BeginStart().Status);
            }

            // There is no production issuer or policy in this executable. It must
            // leave start-pending through the bounded fail-closed stop path, never
            // through Running.
            ApplyTransition(_lifecycle.RequestStop);
            ApplyTransition(() => _lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode));
        }
        catch (Exception)
        {
            StopAfterCallbackFailure();
        }
    }

    private uint HandleControl(uint control, uint eventType, nint eventData, nint context)
    {
        try
        {
            lock (_transitionGate)
            {
                var disposition = _lifecycle.HandleControl(control, out var transition);
                if (disposition == ServiceControlDisposition.Unsupported)
                {
                    return ServiceHostDiagnostics.ErrorCallNotImplemented;
                }

                TryReport(transition.Status);
                return 0;
            }
        }
        catch (Exception)
        {
            return ServiceHostDiagnostics.ErrorCallNotImplemented;
        }
    }

    private void ApplyTransition(Func<LifecycleTransition> transitionFactory)
    {
        lock (_transitionGate)
        {
            TryReport(transitionFactory().Status);
        }
    }

    private void StopAfterCallbackFailure()
    {
        try
        {
            ApplyTransition(_lifecycle.RequestStop);
            ApplyTransition(() => _lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode));
        }
        catch (Exception)
        {
            // The native callback has no safe recovery path beyond the closed state.
        }
    }

    private void TryReport(ServiceStatusSnapshot? snapshot)
    {
        var serviceStatusHandle = Interlocked.CompareExchange(ref _serviceStatusHandle, 0, 0);
        if (snapshot is not { } value || serviceStatusHandle == 0)
        {
            return;
        }

        try
        {
            var nativeStatus = new NativeServiceStatus
            {
                ServiceType = ServiceStatusSnapshot.ServiceTypeOwnProcess,
                CurrentState = (uint)value.State,
                ControlsAccepted = value.ControlsAccepted,
                Win32ExitCode = value.Win32ExitCode,
                ServiceSpecificExitCode = value.ServiceSpecificExitCode,
                Checkpoint = value.Checkpoint,
                WaitHint = value.WaitHintMilliseconds,
            };
            _ = _native.SetServiceStatus(serviceStatusHandle, nativeStatus);
        }
        catch (Exception)
        {
            // SetServiceStatus failures must not escape to SCM or change closure.
        }
    }
}
