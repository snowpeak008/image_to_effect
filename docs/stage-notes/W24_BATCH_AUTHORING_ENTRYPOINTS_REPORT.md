# W24 S0b/S3 Batch Authoring Entrypoints

Date: 2026-08-25  
Status: implemented; positive isolated `-batchmode -nographics` smoke completed for S0b and S3; destructive/fail-closed postconditions remain machine-only and no visual authority is claimed

## Purpose

`W24FormalBatchAuthoringEntrypoints` is the narrow CI/batch adapter for the first
formal authoring steps. It intentionally separates asset authoring from graphics
capture, Visual QA, L3 and user L4 signoff.

## Entry points

| W24 slice | Unity execute method | Creates | Graphics device |
| --- | --- | --- | --- |
| S0b | `VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.BuildS0bFirstFormalAssets` | sustained-flame Runtime Entry, Preview Scene, Manifest and evidence-free C0 identity | isolated `-nographics` execution completed successfully |
| S3 | `VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.BuildS3FirstFormalAssets` | the three S3 Runtime Entries, Preview Scenes, Manifests and evidence-free C0 identities | isolated `-nographics` execution completed successfully for the current tool 3.5 / compiler 2.7 authority |

Both methods reject interactive Editor execution before touching authoring APIs. They
also require `VFX_W24_SHADOW_PROJECT_ROOT` to normalize exactly to the active project
root, require that root to be below `Path.GetTempPath()`, and reject the canonical project
or any of its descendants. `VFX_W24_CANONICAL_PROJECT_ROOT` must name an existing,
different canonical project directory. They are intended for an isolated shadow project
only; a failed first-formal gate or identity mismatch throws. Unity's resulting non-zero
process exit is expected. The positive executions are recorded below; an external-process
negative smoke with a deliberately invalid shadow variable remains a separate fail-closed check.

## Postconditions verified by the adapter

For every effect the adapter verifies the importable formal Prefab and Preview Scene,
the strict Manifest-owned output set and hashes/GUIDs, the immutable C0 receipt and
bootstrap Manifest snapshot, the C0 Contract/Trace hashes, and the exact Runtime Entry
and Preview Scene bindings. It additionally requires `C0_CAPTURE_PENDING` and
`VISUAL_PENDING` and rejects any C0 Trace that already contains evidence.

No capture output, QA verdict, L3 status or L4 user signing is created by these
commands. Graphics-backed batch mode remains mandatory for later W24 recorder capture,
not for this serialized-asset authoring step.

## Recorded isolated smoke and remaining negative check

Both positive commands were executed against the isolated shadow below the task-local temp
root with the required shadow/canonical environment bindings. The S0b log
`test-results/w24-s0b-first-formal-host-r1.log` records `-batchmode -nographics`, the exact
S0b execute method and `Exiting batchmode successfully now!`. The current S3 log
`s3-tool35-authoring.log` records the same facts for the exact S3 execute method and tool
3.5/compiler 2.7 authority. A successful return is emitted only after the adapter's formal
C0 postconditions pass. These runs did not grant capture, QA, L3 or L4 authority.

The following commands remain the reproducible positive smoke shape. A deliberately invalid
shadow-variable external-process run must still return non-zero before any first-authoring
write; the pure validator and interactive guard are already covered by the focused tests.

```powershell
$shadowProject = [IO.Path]::GetFullPath('<temp>\w24-shadow-<unique>\project')
$canonicalProject = [IO.Path]::GetFullPath('D:\WorkWork\Assist\image_to_smart\project')
$env:VFX_W24_SHADOW_PROJECT_ROOT = $shadowProject
$env:VFX_W24_CANONICAL_PROJECT_ROOT = $canonicalProject

& 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath $shadowProject -executeMethod VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.BuildS0bFirstFormalAssets -logFile (Join-Path (Split-Path $shadowProject) 's0b-authoring.log')
& 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath $shadowProject -executeMethod VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.BuildS3FirstFormalAssets -logFile (Join-Path (Split-Path $shadowProject) 's3-authoring.log')
```

The repository `tools/Invoke-Unity.ps1` intentionally guards its main project against
an active graphical Unity process. These direct commands must therefore target a copied
shadow project, never the user's live project.

## Test coverage

`W24FormalBatchAuthoringEntrypointsTests` statically validates the exact public
zero-argument `-executeMethod` shape, the pre-Commit placement of identity verification,
and forbidden downstream integrations. Reflection covers interactive fail-closed behavior
and the pure shadow-path validator's positive and negative cases. The later isolated focused
run produced `test-results/w24-batch-entry-current.xml`: `7 passed / 1 intentionally ignored /
0 failed`. The ignored case is the interactive-only guard that cannot be made interactive
inside the batch runner. The two positive external `-nographics` executions are recorded
above; the deliberately invalid external-process exit smoke remains outstanding.

## Capture identity scope

The later effect-owned emissive-core repair intentionally supersedes that earlier identity statement.
`docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json` now freezes 31 exact sources
as `w24-s3-capture/3.5`, canonical hash
`sha256:f605aacf4d27128347a2a8434a29cebc95aa789af73df55cc26c6fd7a0e42726`. The authoring
source is frozen at `sha256:27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35`
with `CompilerVersion = w24-s3-baseline/2.7`; therefore a fresh isolated S3 authoring run is
required before any 3.5 capture.
