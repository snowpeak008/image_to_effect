# WP-PROTOCOL-P3 — STOPPED

## Package and timing

- Objective: freeze the pure-C# Phase 3 command/job wire-contract foundation only.
- Assigned model/reasoning: gpt-5.6-terra / max.
- First observable source-write proxy: 2026-08-26T16:28:22Z. The exact task-start
  timestamp was not durably captured before that write.
- STOP recorded: 2026-08-26T16:35:09.3518874Z.

## Valid pre-edit baseline replay

The final replay used a Dictionary<string,string> keyed by repository-relative
forward-slash path, [Array]::Sort($paths, [StringComparer]::Ordinal), UTF-8
without BOM, and <lowercase-sha256><two spaces><path><LF>. It completed before
the first edit and matched every required identity:

| Root | Files | SHA-256 |
|---|---:|---|
| src/VFXComposer.Protocol | 48 | 4a82287e5533794c9b3b7fa915facc25a89a04978f2bc9b32b24ec4a8d939a0f |
| src/VFXComposer.Protocol.Tests | 18 | f7ce93800ab967945d67d80b0c119da61c6ff4741e4f3f56d9c54be5dcd1cabb |
| docs/schemas/desktop | 19 | 214bb22e95ab9488f39d9af230bbd9850367656956c65e4337f92390b32ef3f3 |

Receipt: .codex_tmp/WP-PROTOCOL-P3/baseline-replay.txt  
SHA-256: 20536ca9724fd895418e4e521bb9662a7420df36cef2e58ed33144329dd86fec

## STOP condition: unowned concurrent drift

While this package was writing only its allow-listed Protocol paths,
WireSchemaRegistry.cs, Json/StrictWireCodec.cs, Jobs/JobWireVocabulary.cs,
and command-schema files appeared with a different nested-envelope contract. That
content was not written by this task and conflicts with this task's in-progress,
flattened-envelope implementation. It makes writer provenance and a scoped audit
impossible.

WireSchemaRegistry.cs observations:

| Observation | Value |
|---|---|
| Initial content observed before writing | 10,182 bytes |
| Initial individual SHA-256 / timestamp | not captured; unknown |
| Post-drift size | 16,149 bytes |
| Post-drift LastWriteTimeUtc | 2026-08-26T16:31:35Z |
| Post-drift SHA-256 | a7554ba433d3da2b5805828cd28119a7beae4016db55c84c6e12a8bb13d322c2 |

The post-STOP aggregate state was Protocol 67 files /
eae304f088778dd990c5cfe51f98071bed46513f6059d25913e76706e0719b90,
Protocol.Tests unchanged at 18 files /
f7ce93800ab967945d67d80b0c119da61c6ff4741e4f3f56d9c54be5dcd1cabb, and
desktop schemas 23 files /
1b48436361bc4de4d188e2f6b4a7596f9e3b3a11dfd3c23c05874e0a2e5e82ae.

Drift inventory: .codex_tmp/WP-PROTOCOL-P3/stop-drift-inventory.txt  
SHA-256: 098533a82e27e3d7b6ad578b1ff9c80500a5eb23e8f998b7f0793991126739ec

## Files this task changed before STOP

These are the task's attempted source-write paths and their hashes at STOP. Some
were concurrently overwritten or combined, so this is an observation manifest,
not an auditable claim that the current bytes are solely this writer's work.

    5629b60a74b30b8a5b25d533ad31823aeb2418be1cd3e9ca4a2ffe456fdbfc89  src/VFXComposer.Protocol/Commands/CommandContracts.cs
    2243dbed5d30c8484c2a4ef0b459e01c01220ef1d02aa7209f56aee1f3263834  src/VFXComposer.Protocol/Commands/CommandEnvelope.cs
    373a9bbb49bcab4480fdcab47193ae143223208e03058e7dc68edeca5a6a3af0  src/VFXComposer.Protocol/Commands/ValidateRecipeCommand.cs
    f20358422f64350961070bc9c113f003b85606d3cbfb157d3bda565d37ec8904  src/VFXComposer.Protocol/Commands/BuildCandidateCommand.cs
    70824c2628fa9ef67730911f2450d20fabeeee546680ab23742689daa3631d4e  src/VFXComposer.Protocol/Commands/OpenPreviewJobCommand.cs
    0b12406a1151a48bb19c0b2b9ca25756142dc559024047572c4a9962ca42f2ab  src/VFXComposer.Protocol/Commands/ValidatePatchCommand.cs
    93d43b21cb71030c82f59c21f9cbea7569eb13ad57c351e7583ae16010cae136  src/VFXComposer.Protocol/Commands/ApplyPatchCommand.cs
    80947d6c97bb818d54f118bea131a4281ab80617a95ba4a8c532b9f68d0be7e3  src/VFXComposer.Protocol/Commands/RunFocusedTestsCommand.cs
    09c967deedfcb8dffd13b47e12cddf74d911b68145f68e301820be92dc2eaf23  src/VFXComposer.Protocol/Commands/ClosePreviewJobCommand.cs
    4e9b26af0e1bd5be7317a4ffabd8fc3710aca21d2aa0b40f2bd889029c7f6d78  src/VFXComposer.Protocol/Commands/SetPreviewPlaybackCommand.cs
    776d940d93eae142f7bd3a80e81ce35e98eb1e03e7b9910f89fd5d92074f4e56  src/VFXComposer.Protocol/Commands/CancelJobCommand.cs
    d9360818771258e83cf010263411c5a47c450bc318bb44547d219a6505941dd0  src/VFXComposer.Protocol/Jobs/JobContracts.cs
    f68cf203667dce4d105872e9350e7a0d64981f6cd870839395790703b23c2be2  src/VFXComposer.Protocol/Jobs/JobCorrelation.cs
    7e5641ee3421dccdd3e93f1385758613f4363f46147cb8e71caa2b02d166140c  src/VFXComposer.Protocol/Jobs/JobEventEnvelope.cs
    989250aba14b5f26d60a78170dd521399428eaff49a18ee5b59c463a2917ff09  src/VFXComposer.Protocol/Jobs/JobProgress.cs
    720836a3b427b08ea8857aed73b92b1f23b3027e0690a4da554a75f6cf452ce8  src/VFXComposer.Protocol/Jobs/JobLogEvent.cs
    11f16015d76afd0c09912bcc7a79df00f9897daa3443fb7e4dd12fc1eb881b94  src/VFXComposer.Protocol/Jobs/JobArtifact.cs
    8c9a1f0aade917c5c7b07b575706dced639da1554f519c24dcf9066c78046587  src/VFXComposer.Protocol/Jobs/JobCompletion.cs

Generated receipt files are excluded from that source manifest. No source, schema,
or test file was edited after the STOP acknowledgement. No deletion or revert was
performed.

## Validation state

- Completed: required documents read; final exact ordinal baseline replay.
- Not run because STOP supersedes package acceptance: Release Protocol build, Release
  Protocol tests, solution build, Draft 2020-12 meta/positive/negative validation,
  forbidden-surface scan, regression tests, golden-vector generation, deterministic
  success manifest, and acceptance receipts.
- Therefore no command/job contract, schema, strict-codec, hash, diagnostic,
  authority-boundary, or regression claim is proved by this package.

## Self-audit and next dependency

| Severity | Result |
|---|---|
| P0 | OPEN — concurrent unowned Protocol/schema drift; package is not auditable. |
| P1 | NOT ASSESSED — acceptance validation was not run. |
| P2 | NOT ASSESSED — acceptance validation was not run. |

Blocker: controller must preserve and attribute the concurrent work, resolve the
two competing command/job contract shapes, then publish a fresh single-writer
package from a newly frozen baseline. An independent read-only audit cannot begin
from this partial state.

**STOPPED — no continuation into another package.**
