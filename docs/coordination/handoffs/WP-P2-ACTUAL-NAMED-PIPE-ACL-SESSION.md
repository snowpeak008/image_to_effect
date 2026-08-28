# WP-P2 actual named-pipe ACL session — lifetime remediation handoff

Status: `STOPPED` — P1 namespace-gate lifetime remediation, evidence refresh, and writer self-audit are complete. A fresh independent frozen-byte audit and controller integration remain pending.

Execution metadata:

- Model: `gpt-5.6-terra`
- Reasoning: `max`
- Evidence receipt window (UTC): `2026-08-28T04:53:51.5093184Z` to `2026-08-28T05:01:31.9895404Z`

## Starting gate and authorized final source identity

The remediation began only after re-reading the active P1 control contract, the prior P1 handoff, and the lifetime findings. The exact four-leaf input gate was:

```text
a79a1164c89dce77907420504b15365d7ecc71dfe50d63ab0dbd58e83df8edaa  services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs
7db7579ac1f01c837634a752c91428665cbcd64b61dcaa7681deacf5cf07cd6a  services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs
57d9dec585064b7dec121e0324b6e5ac94d3df2a06e8eb33118edd7d5416a3e1  services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs
29354ae4f07acc8390d315b4cd320146c3f7770230655b34a49f7674833f8993  services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs
```

Only the four authorized P1 leaves are product/test source. This handoff and the existing `.codex_tmp/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION/**` generated evidence root are the only metadata changes. No `Program`, existing broker host, ServiceHost, policy, protocol, client, desktop, Unity, project file, lock file, solution, control document, or production wiring file was edited.

The final strict-ordinal source manifest is four rows / `7d356775a5ef48e940918d02c73d5fc695adbc7eb3a9c30db2a2935dbb43d1ff`:

```text
29354ae4f07acc8390d315b4cd320146c3f7770230655b34a49f7674833f8993  services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs
cbd3826354e06a3472e6a0ffbf081499cd7a004d94a4eb0776698dd3c6093db3  services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs
c61178690ed61092ff30555a4d21a10f4f23bc925312b39ec08e938bfc50a39d  services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs
7db7579ac1f01c837634a752c91428665cbcd64b61dcaa7681deacf5cf07cd6a  services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs
```

## P1-1 strong-exception lifecycle closure

`WindowsProductionNamedPipeNamespaceState` now has an explicit production `Plan → PrepareCandidateLease → CommitPreparedCandidateLease` sequence.

- The first physical handle is still pessimistically latched as permanent rejection before ACL readback or owner construction.
- `TryPrepareCandidateLease` validates the same readback handle, computes the checked next live count, and allocates the sole candidate owner before changing any count or first-attempt flag.
- A first-path allocation/construction failure therefore leaves the pessimistic rejection in place; the host `finally` closes the physical handle and calls `RejectFirstAttemptPermanently`. A later-path preparation failure leaves the prior count unchanged and its still-host-owned physical handle is closed in the same `finally`.
- `TryCommitPreparedCandidateLease` performs only state checks, a candidate compare-and-swap, and gate-protected integer/flag assignments. It allocates nothing and has no throwing operation on its successful path.
- The last committed owner release decrements the real count to zero, so the next non-disposed namespace plan is `FIRST_PIPE_INSTANCE` again.

The direct deterministic host test uses the production state and a real prepared candidate owner, deliberately abandons it before commit, and disposes that uncommitted owner. This is the deterministic pre-commit failure seam: no test delegate, native factory, `ForTests` path, or forced/claimed `OutOfMemoryException` is used. It proves that no pre-commit count/flag mutation exists; a real allocation failure cannot reach commit. The same test verifies both first-path permanent rejection and later-path unchanged count plus physical-handle closure.

## P1-2 unified finalizable ownership closure

`WindowsProductionNamedPipeCandidate` is now the one sealed, finalizable production owner for its private `SafePipeHandle` and its committed namespace lease. The old separately disposable `VerifiedCandidateLease` does not exist.

- A candidate exposes only immutable `DurableProfileDigest`, `ServiceSid`, and `UserSid` proof metadata. It exposes no `Handle`, `Readback`, `IntPtr`, or independently disposable native-handle/lease property.
- Explicit disposal and the finalizer both atomically take the single ownership state. The winner holds its preallocated candidate gate, takes the same namespace `_gate` as host plan/create, keeps that gate through `SafeHandle.Dispose()`/`ReleaseHandle`, and then performs the exact committed release or prepared/failed-close abort in `CloseAndTransitionUnderGate`; duplicate and concurrent disposal cannot release twice or return before that transition.
- `SafePipeHandle` retains its critical finalizer. The candidate finalizer has no caller-provided callback, delegate, or factory path; it invokes the same candidate-gate-to-namespace-gate routine and contains all finalizer exceptions while preserving the SafeHandle critical-finalizer backstop.
- Host disposal and planning use the same namespace gate. Namespace state never acquires the candidate gate; the pre-publication host cleanup is safe through monitor reentrancy when it already owns `_gate`. A disposed host cannot plan another creation.

The host suite remains exactly eight tests, while the readback suite remains its unchanged eight tests. The deterministic `BarrierSafePipeHandle` case blocks `ReleaseHandle` while the winner holds the real namespace gate, starts a production `TryPlanNextCreation` call and proves it remains monitor-blocked, then releases the barrier and proves dispose, the duplicate dispose waiter, and the planner complete with `FIRST_PIPE_INSTANCE` and count zero. The other host coverage remains ABI, pessimistic first lifecycle, direct prepared-owner abandonment, successful immutable metadata commit, bounded `WeakReference`/GC finalizer abandonment, fresh later-readback/mismatched-handle and candidate/host-disposal serialization, and ordinary-token fail-closed host behavior. These are executable production-state tests, not source-text or delegate-injection oracles.

## Retrospective provenance disclosure

`receipts/retrospective-provenance/retrospective-reconstruction.json` remains explicitly labelled `RETROSPECTIVE_RECONSTRUCTION_NOT_CONTEMPORANEOUS`. It removes exactly the four final P1 leaves from current final roots and reproduces:

- Broker `40` / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c`
- Broker.Tests `18` / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`

It verifies canonical target ownership with zero case-insensitive collisions. This remains an honest noncontemporaneous reconstruction: no durable pre-edit capture is claimed, and the retained retrospective-provenance P2 disclosure is not rewritten as a contemporaneous receipt.

## Validation evidence and strict manifests

Each command receipt under `.codex_tmp/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION/receipts/` contains actual `stdout.raw.bin` and `stderr.raw.bin`, their hex renderings, normalized LF/no-BOM decoded text, LF/no-BOM command/timestamp/exit files, and any TRX. Raw `.raw.bin` streams are authoritative; normalized `.txt` files are not described as raw. The separately manifested `diagnostics/` record is explicitly not an original raw receipt.

| Receipt | UTC start → finish | Result |
|---|---|---|
| `locked-build` | 04:53:51.5093184 → 04:53:52.5433504 | exit `0`; `0` warnings / `0` errors |
| `targeted-tests` | 04:54:04.5043420 → 04:54:05.9108934 | exit `0`; `16/16` |
| `broker-tests-full` | 04:56:14.1244332 → 04:56:16.3435167 | exit `0`; `129/129` |
| `stability-r1` | 05:01:13.1667334 → 05:01:15.1999404 | exit `0`; unique-path raw/TRX, `129/129` |
| `stability-r2` | 05:01:30.0120056 → 05:01:31.9895404 | exit `0`; unique-path raw/TRX, `129/129` |
| `phase2-schema` | 04:54:22.8595848 → 04:54:23.5342305 | exit `0`; `22 / 13 / 14 / 236` |
| `solution-build` | 04:54:29.4019575 → 04:54:31.2183450 | exit `0`; `0` warnings / `0` errors |
| `broker-smoke` | 04:54:37.6285964 → 04:54:37.7201589 | exit `23`; zero stdout; raw stderr `57323446533030310D0A` (`W24FS001\r\n`) |
| `pe-abi-forbidden-no-wiring` | 04:54:51.1485206 → 04:54:53.9983490 | exit `0`; PASS; static verifies close-under-namespace-gate and no candidate-gate inversion in namespace state |
| `frozen-roots-and-manifests` | 04:55:00.9870574 → 04:55:04.2981511 | exit `0`; PASS |
| `retrospective-provenance` | 04:55:10.8399067 → 04:55:11.3667292 | exit `0`; PASS / explicitly not contemporaneous |
| `residue-and-no-live-listener` | 04:56:34.3452231 → 04:56:35.0582793 | exit `0`; zero residue, unexpected P1 references, and live Broker processes |

The first `broker-tests-full` attempt at `04:54:15.3990698 → 04:54:17.5595292` observed `128/129` and exit `1` in the unrelated `WindowsPinnedExecutableContentObserverTests.ObserveRejectsDirectoryZeroOversizeAndReadShareConflicts` teardown (`PinnedScratchTreeCleanup` found a nonempty cleanup directory). It is outside the four authorized leaves and was not fixed in this package. Its raw/TRX was unfortunately overwritten by the same-path retry before preservation direction arrived; [`diagnostics/superseded-full-run-failure.md`](../../../.codex_tmp/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION/diagnostics/superseded-full-run-failure.md) records that limitation and the observed stack without claiming replayable raw evidence. The final `broker-tests-full`, `stability-r1`, and `stability-r2` raw/TRX receipts are three independent retained `129/129` green runs.

The compiled static/ABI receipt verifies internal-only P1 types, a finalizable candidate owner without an escaped handle/readback property, absence of a standalone lease type, Plan/Prepare/Commit shape, exact 8+8 executable tests, `SECURITY_ATTRIBUTES` x86/x64 layout derivation and PE reflection, native ABI, forbidden production terms, no existing-host wiring, and the source/reflection contract that `CloseAndTransitionUnderGate` holds `_gate` through `SafeHandle.Dispose()` without namespace state referencing `_disposeGate`.

Final ordinal roots are Broker `42` / `3d3306953327ac1e9360db424d5e53b7124c800097c9e910feeea1b8c47b6dff`; Broker.Tests `20` / `14427f84c1e7892bbf3aa0bffbe5321cbc97a8378bc3540658bcdd355d066513`; all seven foreign roots replay their frozen identities. The nine-row final-root aggregate is `7c66ae1edf37cf258841e7086f5037603c7c2b7747d4d85c679f2b6420672f5e`.

The self-excluded receipt manifest is `127` rows / `f567a16fd3b28f02278abc7071c802afbab9a53f4bca441d2879a9f113cc0c39`; it includes the honest superseded-run diagnostic plus both unique stability runs and does not include `manifests/receipt-manifest.sha256`. No self-hash is recorded for this handoff.

## Boundary and audit state

Writer self-audit finds the namespace-gate lifetime P1 defect remediated in this scoped source slice. The retrospective reconstruction remains transparently noncontemporaneous rather than invented as a P0/P1 closure. This is not an independent audit verdict; the independent frozen-byte audit remains pending.

The slice remains dormant source/static/synthetic behavior only. It does not start or register a service, activate a production listener, call accept/connect, make the Broker `Running`, open project paths, connect Desktop/Worker/Unity production routes, issue commands, grant authority, or claim production readiness.

STOPPED
