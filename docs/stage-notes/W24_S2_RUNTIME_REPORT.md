# W24 S2 Runtime Module Report

Status: **r17 completes the current-byte S2 machine/protocol boundary: all four isolated Unity gates passed `7/7 + 1/1 + 1/1 + 1/1`, the dedicated Windows Development Player built and completed all five launch scenarios, and the independent audit returned GO with P0=0, P1=0, P2=0. This does not establish rendered visual quality, L3, L4, Publication, or user acceptance.**

## Delivered modules

1. `W24MovingEmitterTrailProtocol` + `W24MotionSampleProtocol` — motion is sampled from a real transform; a stationary source disables new trail emission; pool reset clears every trail.
2. `W24ModelBindingAdapter` + `W24ModelBindingResolver` — resolves Transform, socket, renderer, mesh and named-bone targets; failures expose explicit fault codes rather than silently binding to a root.
3. `W24FragmentMotionSystem` + `W24FragmentMotionKernel` — each fragment has its own seed, position, rotation, velocity, angular velocity, damping and lifetime.
4. `W24RealLightingModule` — controls actual Unity `Light` identities under a budget. Null and duplicate serialized slots do not consume budget or inflate telemetry; replacing an already-live configured set immediately disables both old and replacement identities before reuse. `IW24Light2DAdapter` remains an optional boundary without a hard URP dependency.
5. `W24SemanticTimeline` + `W24SemanticTimelineModel` — distinct continuous, impulse, replace, clear and interrupt commands, with separate completion and interruption exits.
6. `W24SemanticTelemetry` — common module/state/seed/event/active-count/cleanup/fault facts for Capture/Trace integration.

These are protocol/component facts. They do not substitute for rendered frames.

## Remediated Windows Player evidence harness

The current source closes the previous static review findings as follows:

- `W24S2PlayerEvidenceRunner` is inert in the Editor and without the valueless `-w24S2Evidence` activation flag. It rejects duplicate/malformed activation and the legacy `-w24S2ResultPath` surface. The evidence-only `-w24S2ForceProbeFailure` flag is restricted to a Development Player.
- The Player derives its build root from `Directory.GetParent(Application.dataPath).FullName` and can publish only `evidence/player-result.json` below that root. The caller cannot select another output path.
- Existing path components, the evidence directory, final file and matching pending files are rejected if any is a reparse point. Publication uses a random same-directory pending file, `FileMode.CreateNew`, `FileShare.None`, UTF-8 without BOM, durable flush, an absent final destination and same-volume `File.Move`. The Player never deletes or replaces an old final. On failure it may remove only the unique pending file that its own process successfully created.
- Moving history is sampled by component `Update` while the source moves across three real PlayerLoop frames; the probe never calls `Tick`. Fragment motion and impulse completion advance only through real `Update` frames and scaled `Time.deltaTime`; the probe never calls either module's `Advance`. Binding and real lighting remain live for at least one frame before readback. The common telemetry row consumes those post-frame snapshots. Each named module stage independently receives a bounded 1,000,000-frame / 10-realtime-second deadline; the driver detects a stage transition immediately after the coroutine advances and records that same frame as the new origin. This keeps both bounds while avoiding the invalid assumption that 60,000 headless frames represent the whole suite's required real duration.
- Owned objects cross a further PlayerLoop boundary before cleanup. Probe failure, write failure and any failed module return exit `24`; malformed evidence command input returns `25`; only six passing rows return `0`.
- `W24S2PlayerBuildEvidenceTests` builds once into an exact `vfxcomposer_w24_s2_player_<32 lowercase hex>` direct child of system temp outside both the Unity project and workspace. It recursively refuses reparse points and launches the same build for duplicate and malformed command cases, a forced-probe failure, success and a write-once conflict. Windows `CreateProcessW(CREATE_SUSPENDED)` creates the Player before it can spawn a child; the exact process handle is assigned to a `KILL_ON_JOB_CLOSE` Job Object before `ResumeThread`. Timeout/cleanup use `TerminateJobObject`, bounded primary-handle wait and bounded `QueryInformationJobObject.ActiveProcesses == 0`, avoiding both unsupported `Process.Kill(bool)` and PID-reuse termination. Temp cleanup and Build Settings restoration are nested independent `finally` boundaries.
- Result consumption uses strict UTF-8 with invalid-byte exceptions and rejects BOM before `W24StrictJsonText.ParseObject`. Root and module key sets and every JSON type are frozen. Success requires empty `failure`, exact six-module order and six true rows; forced failure requires exit `24`, non-empty `failure` and `modules=[]`.
- `LaunchPlayer` snapshots evidence in its outer `finally`, after bounded Job cleanup but before any exception can return to the caller or the external build can be deleted. Thus normal exit, 120-second timeout, termination failure, post-exit tree-completion failure, missing log, exit-code failure and `Dispose` failure all emit scenario, OS exit or `NONE`, raw-result Base64 or `NONE`, result/log SHA-256, bounded log tail and any snapshot-read error. Before scenario-specific exit/result assertions, a successfully strict-parsed result is additionally emitted as normalized JSON with Unity version. Runtime exceptions encode the active stage plus `Exception.ToString()` in the frozen `failure` string, so an unexpected published failure retains the stage and stack in NUnit output. The harness does not claim an attachment or write a canonical/shadow artifact.

`Tests/PlayerEvidence/W24S2PlayerEvidence.unity` remains a package-owned scene containing only the serialized evidence runner. It is supplied directly through `BuildPlayerOptions.scenes`, is not under `Assets/`, and is not registered in global `EditorBuildSettings`.

## Current verification boundary

The r17 isolated shadow ran Unity 2022.3.62f3c1 against bytes that were SHA-256 exact with the canonical remediation source set. All four Unity processes exited naturally with OS exit `0`:

| Gate/filter | Result | XML UTC interval / duration | XML SHA-256 | Log SHA-256 | Process |
|---|---:|---|---|---|---|
| `VFXComposer.Tests.EditMode.W24S2RuntimeModuleTests` | `7/7` | `2026-08-25 17:35:46Z` / `0.0632319s` | `4ead6c7f3beedd0a0a808378fc815004d451facf15ff7a11a68ff5f3aac43e35` | `4440a802fc0f2a5ff973a55fde1073846eba27094147f94572e0bc29b44afb48` | natural exit `0` |
| `VFXComposer.Tests.PlayMode.W24S2RuntimeModulePlayModeTests.LightingModule_ControlsActualLightWithinBudget` | `1/1` | `2026-08-25 17:36:06Z` / `0.2247999s` | `4a14e5fcf2266033cab333ee20fca85093adb8b3334eaad162ee2b04048007be` | `17596ef9f2dba3453121b1fbb2387e8678299401c8cf6c7b1dedc328c7dde180` | natural exit `0` |
| `VFXComposer.Tests.EditMode.W24S2PlayerBuildEvidenceTests.S2_PlayerEvidenceFixture_IsPackageOwnedAndDoesNotDependOnAssetsOrBuildSettings` | `1/1` | `2026-08-25 17:36:21Z` / `0.1279095s` | `8faa1be01e880110e8b8623efc917977bf9fa6b8d8846afb9d52d7b4314b5718` | `caeb9bec44462b9c6b2ecfa7a5341dc9de4e3066c6d2ad1c25bdfe3b39fc3128` | natural exit `0` |
| `VFXComposer.Tests.EditMode.W24S2PlayerBuildEvidenceTests.S2_DedicatedWindowsPlayer_BuildsLaunchesAndPassesAllSixRuntimeModules` | `1/1` | `2026-08-25 17:36:42Z–17:36:56Z` / `13.1721348s` | `e4a61f80e1101a7febc27eca4de4c462f6cdf41333bb0b76135b3c40b1367b6f` | `d14784ca674fcbb9673a1b09a005d656f120d72da02bb8f3b673c5d070119e9b` | natural exit `0` |

The four gates aggregate to `10/10`, `failed=0`. The earlier focused Roslyn compilation also remained `0/0/0/0` for Runtime/Editor/EditMode/PlayMode, with only the two unrelated existing `StyledVfxController.portalRadius/swirlSpeed` CS0414 warnings; r17, rather than Roslyn alone, supplies the current Unity import, BuildPipeline, real Player, coroutine, NUnit and cleanup evidence.

## Latest accepted Player gate: r17

The accepted r17 Player gate built one Windows Development Player and reused it for all five frozen scenarios:

| Scenario | OS exit | Result SHA-256 | Player log SHA-256 | Verified result |
|---|---:|---|---|---|
| `invalid-command` | `25` | `NONE` | `759af90729a668369fa3007350b688bb2ace95116158995e7867ec11d4a46f23` | no result published |
| `invalid-flag-value` | `25` | `NONE` | `f8f6aa7dd1c22906726b9413f5c465f67618cfa7355cea25d00dcfc411c4248c` | no result published |
| `forced-probe-failure` | `24` | `6aaba16ff0ec05967d1a7f1cedf60b1a184a5cf1f14a7d95743eea1cd3a2a73f` | `6184bef2b77ff89dcd5525ca877efca8907bd17aa7002d8d42841d4cbc2349e6` | strict failure, `modules=[]`, `stage=forced_probe_failure` |
| `success` | `0` | `7b5888377a9c3f846575f9a95fa637f9a9f06f3e3cbb819f9a2c3df05f9682e7` | `dcb0427f63f807c360010f3b27440497e0da38f79fe6368794c368ffb7ea943f` | strict success, six ordered true rows, empty `failure` |
| `write-once-conflict` | `24` | `7b5888377a9c3f846575f9a95fa637f9a9f06f3e3cbb819f9a2c3df05f9682e7` | `f920497220f042d2fc9419c78bbad84943830bf280585afeebe04924f56b4969` | pre-existing success result remained byte-identical |

The successful result identifies Unity `2022.3.62f3c1`, `WindowsPlayer`, graphics device `Null`, batch mode `true`, and the exact passing order `moving_emitter_trail`, `model_binding`, `fragment_motion`, `real_lighting`, `semantic_state_machine`, `semantic_telemetry`. The conflict launch preserved both the success SHA and its exact raw Base64 bytes; the passed NUnit assertion independently compared the old and post-conflict byte arrays. Every scenario reported `resultReadFailure=NONE` and `playerLogReadFailure=NONE`.

The fixture cleanup receipt names `vfxcomposer_w24_s2_player_40d8f58f75bc484d8b56043cd4453e6b`; after the task, matching `vfxcomposer_w24_s2_player_*` temp directories numbered `0`. The five remediation-bound C# files were canonical/shadow byte-exact: runner `27601d2afa8a51de06510ba21fc92eb7e4423805d668977b2b89e217e139f37c`, lighting `9208c260367f10c179bd30d7c41819ab08ac3c6dfe0955837a42ebd9969fb7ed`, EditMode runtime tests `8077a40ae8200544c9be96adbe5f92e684716c4a8895305e2766f0b448829d32`, Player-build tests `67f872014a573fa2d5f7eb3a18a3c209cd25829355876b3bccaa55077bcea2b9`, and PlayMode tests `bbe50ef7a73dc7e4abf94a7e305a8ee67613a3f9988fdf92f0719d0d3fbbbfae`. Independent read-only audit verdict: **GO, P0=0, P1=0, P2=0**.

## Rejected diagnosis: r16 failed

The isolated Windows Player r16 gate on 2026-08-25 built the Development Player successfully. Its duplicate activation and malformed valueless-flag launches each returned OS exit `25` without a result; its injected probe failure returned OS exit `24` with a strict failure result. The nominal success launch instead returned OS exit `24`. Its baseline recorded result SHA-256 `98e80a4d86f28e443c9f834b970a4177511ef943d3a443ecd7a1c77b477e98ca`, player-log SHA-256 `345e73226c7a701b7b5d77d6013ce21e74ae4c3c30aca018cb2d4903a3bf0b6a`, and about 60,005 Player frames before the fixture was safely deleted.

The test asserted OS exit `0` before parsing that nominal result, so r16 did not retain its normalized failure text in NUnit XML. A deterministic reconstruction from the frozen serializer shape and current module details shows that the recorded result hash matches exactly the candidate with the first four module rows passing and `System.TimeoutException: S2 Player probe exceeded 60000 PlayerLoop frames.`; candidates with zero through three or five through six rows do not match. This locates the failure at `semantic_state_machine`: the earlier 0.8-second fragment Update sequence had consumed most of a suite-global frame budget before the 0.3-second impulse sequence. This hash reconstruction is diagnosis, not a substitute for the deleted raw result.

The post-r16 source resets both deadline clocks for each named stage, raises the per-stage frame ceiling for high-throughput `-batchmode -nographics` loops, and emits any strict result before checking the OS exit. It does not call `Advance`, synthesize time, or change the module's scaled-time semantics. Accepted r17 validates that remediation; r16 remains a rejected diagnostic record and is not counted as passing evidence.

## Superseded historical evidence

On 2026-08-25 the former fixture/import and former Player build/launch filters each reported `1/1 Passed` in the isolated shadow (`w24-s2-player-import.xml` and `w24-s2-player-build-run.xml`). Those XML files validate older source that accepted an arbitrary absolute result path and executed most module work synchronously. The ephemeral raw result and Player log were not retained. Therefore the historical XML is not current-machine evidence and is not used to claim this remediation passed.

## Authority boundary

The harness deliberately captures no Beauty frame and makes no visual-quality decision. Accepted r17 establishes only the S2 machine/protocol boundary; it cannot grant Visual QA, L3, L4, Publication, or user acceptance.
