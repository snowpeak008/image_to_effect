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
    /// Prompt generation is offered only when the one ChatLlm binding exists; an unbound or
    /// unreadable configuration refuses prompt entries instead of silently running a subset. Recipe
    /// build capability is independent of the AI configuration — the restricted build path never
    /// touches a channel — so <paramref name="recipeBuildSupported"/> reflects whether the host
    /// registered the build executor.
    /// </summary>
    public static BatchCapabilityProfile FromDesktopRuntime(IAiDesktopRuntime runtime, bool recipeBuildSupported = false)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        bool bound;
        try
        {
            bound = runtime.Settings.Load().ChannelStatuses.Any(status =>
                status.Channel == AiChannel.ChatLlm && status.State != AiDesktopChannelStatusKind.Unbound);
        }
        catch (AiGatewayException)
        {
            bound = false;
        }

        return new BatchCapabilityProfile(bound, recipeBuildSupported);
    }
}
