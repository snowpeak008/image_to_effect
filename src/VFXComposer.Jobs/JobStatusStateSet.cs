using System.Collections.Frozen;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs;

/// <summary>
/// Closed-set membership check over the Protocol <see cref="JobStatusStates"/> constants.
/// The Protocol set itself is internal to that assembly; the strings here are the exact
/// Protocol constants, never restated literals.
/// </summary>
internal static class JobStatusStateSet
{
    private static readonly FrozenSet<string> Known =
        new[]
        {
            JobStatusStates.Queued,
            JobStatusStates.Running,
            JobStatusStates.Succeeded,
            JobStatusStates.Failed,
            JobStatusStates.Cancelled,
            JobStatusStates.Disconnected,
        }.ToFrozenSet(StringComparer.Ordinal);

    public static string Require(string? value, string parameterName) =>
        value is not null && Known.Contains(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
}
