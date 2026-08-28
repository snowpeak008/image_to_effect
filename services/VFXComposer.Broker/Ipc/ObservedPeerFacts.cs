using VFXComposer.Protocol.Hashing;
using VFXComposer.Broker.Security;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Ipc;

/// <summary>Facts observed from the connected OS pipe peer, never from its JSON claim.</summary>
internal sealed class ObservedPeerFacts : IDisposable
{
    private SafeProcessHandle? _processHandle;

    internal ObservedPeerFacts(
        SafeProcessHandle processHandle,
        int processId,
        string processEpoch,
        WindowsSid userSid,
        TypedHash imageIdentity)
    {
        _processHandle = processHandle ?? throw new ArgumentNullException(nameof(processHandle));
        if (processHandle.IsInvalid || processId <= 0)
        {
            processHandle.Dispose();
            throw new ArgumentException("Observed peer process handle is invalid.", nameof(processHandle));
        }

        ProcessId = processId;
        ProcessEpoch = processEpoch;
        UserSid = userSid ?? throw new ArgumentNullException(nameof(userSid));
        UserSidIdentity = UserSid.UserIdentityHash;
        ImageIdentity = imageIdentity;
    }

    public int ProcessId { get; }
    public string ProcessEpoch { get; }
    public WindowsSid UserSid { get; }
    public TypedHash UserSidIdentity { get; }
    public TypedHash ImageIdentity { get; }

    internal SafeProcessHandle TakeProcessHandle()
    {
        var handle = Interlocked.Exchange(ref _processHandle, null);
        return handle ?? throw new ObjectDisposedException(nameof(ObservedPeerFacts));
    }

    public void Dispose() => Interlocked.Exchange(ref _processHandle, null)?.Dispose();
}
