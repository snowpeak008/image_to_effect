using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Derives the executable-capability profile from the persisted channel bindings. Every entry
/// surface probes the same way, so the same manifest is accepted or refused identically no matter
/// which surface submitted it (REQ-002-11). Reading the settings snapshot constructs no HTTP
/// client, parses no endpoint and reads no secret.
/// </summary>
public static class BatchCapabilityProbe
{
    /// <summary>
    /// Recipe build entries have no executor in this build, so only prompt generation can be
    /// offered, and only when the one ChatLlm binding exists. An unbound or unreadable
    /// configuration refuses the whole manifest instead of silently running a subset.
    /// </summary>
    public static BatchCapabilityProfile FromDesktopRuntime(IAiDesktopRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        try
        {
            var bound = runtime.Settings.Load().ChannelStatuses.Any(status =>
                status.Channel == AiChannel.ChatLlm && status.State != AiDesktopChannelStatusKind.Unbound);
            return bound ? BatchCapabilityProfile.GenerationOnly : BatchCapabilityProfile.GenerationUnavailable;
        }
        catch (AiGatewayException)
        {
            return BatchCapabilityProfile.GenerationUnavailable;
        }
    }
}
