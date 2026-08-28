namespace VFXComposer.Broker.ServiceHost;

internal enum ServiceLifecycleState : uint
{
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
}

internal enum ServiceControlDisposition
{
    Accepted,
    Unsupported,
}

internal readonly record struct ServiceStatusSnapshot(
    ServiceLifecycleState State,
    uint ControlsAccepted,
    uint Win32ExitCode,
    uint ServiceSpecificExitCode,
    uint Checkpoint,
    uint WaitHintMilliseconds)
{
    internal const uint ServiceTypeOwnProcess = 0x00000010;
    internal const uint ServiceAcceptStop = 0x00000001;
    internal const uint ServiceAcceptShutdown = 0x00000004;
    internal const uint PendingCheckpoint = 1;
    internal const uint PendingWaitHintMilliseconds = 5000;
    internal const uint MaximumWaitHintMilliseconds = 30000;

    internal static ServiceStatusSnapshot For(ServiceLifecycleState state, uint serviceSpecificExitCode)
    {
        var snapshot = state switch
        {
            ServiceLifecycleState.StartPending => new ServiceStatusSnapshot(
                state,
                0,
                0,
                0,
                PendingCheckpoint,
                PendingWaitHintMilliseconds),
            ServiceLifecycleState.Running => new ServiceStatusSnapshot(
                state,
                ServiceAcceptStop | ServiceAcceptShutdown,
                0,
                0,
                0,
                0),
            ServiceLifecycleState.StopPending => new ServiceStatusSnapshot(
                state,
                0,
                0,
                0,
                PendingCheckpoint,
                PendingWaitHintMilliseconds),
            ServiceLifecycleState.Stopped => new ServiceStatusSnapshot(
                state,
                0,
                serviceSpecificExitCode == 0 ? 0 : ServiceHostDiagnostics.ErrorServiceSpecificError,
                serviceSpecificExitCode,
                0,
                0),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        Validate(snapshot);
        return snapshot;
    }

    internal static void Validate(ServiceStatusSnapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.State))
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }

        var isPending = snapshot.State is ServiceLifecycleState.StartPending or ServiceLifecycleState.StopPending;
        if (isPending)
        {
            if (snapshot.ControlsAccepted != 0 ||
                snapshot.Win32ExitCode != 0 ||
                snapshot.ServiceSpecificExitCode != 0 ||
                snapshot.Checkpoint is 0 or > PendingCheckpoint ||
                snapshot.WaitHintMilliseconds is 0 or > MaximumWaitHintMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshot));
            }

            return;
        }

        if (snapshot.Checkpoint != 0 || snapshot.WaitHintMilliseconds != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }

        if (snapshot.State == ServiceLifecycleState.Running)
        {
            if (snapshot.ControlsAccepted != (ServiceAcceptStop | ServiceAcceptShutdown) ||
                snapshot.Win32ExitCode != 0 ||
                snapshot.ServiceSpecificExitCode != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshot));
            }

            return;
        }

        if (snapshot.ControlsAccepted != 0 ||
            ((snapshot.Win32ExitCode == 0) != (snapshot.ServiceSpecificExitCode == 0)) ||
            (snapshot.Win32ExitCode != 0 &&
             snapshot.Win32ExitCode != ServiceHostDiagnostics.ErrorServiceSpecificError))
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }
    }
}

internal readonly record struct LifecycleTransition(
    ServiceLifecycleState Previous,
    ServiceLifecycleState Current,
    ServiceStatusSnapshot? Status)
{
    internal bool Changed => Previous != Current;
}

internal sealed class ServiceLifecycle
{
    private readonly object _gate = new();
    private ServiceLifecycleState _state = ServiceLifecycleState.Stopped;

    internal ServiceLifecycleState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    internal LifecycleTransition BeginStart() => Transition(ServiceLifecycleState.StartPending, 0);

    internal LifecycleTransition MarkRunning() => Transition(ServiceLifecycleState.Running, 0);

    internal LifecycleTransition RequestStop()
    {
        lock (_gate)
        {
            return _state switch
            {
                ServiceLifecycleState.StartPending or ServiceLifecycleState.Running =>
                    MoveTo(ServiceLifecycleState.StopPending, 0),
                ServiceLifecycleState.StopPending or ServiceLifecycleState.Stopped =>
                    new LifecycleTransition(_state, _state, null),
                _ => throw new InvalidOperationException("The lifecycle state is not recognized."),
            };
        }
    }

    internal LifecycleTransition CompleteStop(uint serviceSpecificExitCode)
    {
        lock (_gate)
        {
            return _state switch
            {
                ServiceLifecycleState.StopPending => MoveTo(ServiceLifecycleState.Stopped, serviceSpecificExitCode),
                ServiceLifecycleState.Stopped => new LifecycleTransition(_state, _state, null),
                _ => throw new InvalidOperationException("A service can stop only after stop-pending."),
            };
        }
    }

    internal ServiceControlDisposition HandleControl(uint control, out LifecycleTransition transition)
    {
        switch (control)
        {
            case 1: // SERVICE_CONTROL_STOP
            case 5: // SERVICE_CONTROL_SHUTDOWN
                transition = RequestStop();
                return ServiceControlDisposition.Accepted;
            default:
                transition = default;
                return ServiceControlDisposition.Unsupported;
        }
    }

    private LifecycleTransition Transition(ServiceLifecycleState target, uint serviceSpecificExitCode)
    {
        lock (_gate)
        {
            if (!IsLegal(_state, target))
            {
                throw new InvalidOperationException($"The lifecycle transition {_state} -> {target} is not legal.");
            }

            return MoveTo(target, serviceSpecificExitCode);
        }
    }

    private LifecycleTransition MoveTo(ServiceLifecycleState target, uint serviceSpecificExitCode)
    {
        var previous = _state;
        _state = target;
        return new LifecycleTransition(previous, target, ServiceStatusSnapshot.For(target, serviceSpecificExitCode));
    }

    private static bool IsLegal(ServiceLifecycleState current, ServiceLifecycleState target) =>
        (current, target) switch
        {
            (ServiceLifecycleState.Stopped, ServiceLifecycleState.StartPending) => true,
            (ServiceLifecycleState.StartPending, ServiceLifecycleState.Running) => true,
            (ServiceLifecycleState.StartPending, ServiceLifecycleState.StopPending) => true,
            (ServiceLifecycleState.Running, ServiceLifecycleState.StopPending) => true,
            (ServiceLifecycleState.StopPending, ServiceLifecycleState.Stopped) => true,
            _ => false,
        };
}
