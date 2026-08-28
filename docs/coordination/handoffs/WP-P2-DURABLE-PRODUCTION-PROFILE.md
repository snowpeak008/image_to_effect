# WP-P2-DURABLE-PRODUCTION-PROFILE — STOPPED / NO-GO

## Outcome

**STOPPED — NO-GO.** This package does not claim the requested scoped GO.

The sole objective was a dormant, Windows-only authenticated durable production-profile and replay-store foundation. The implementation was stopped because the required exact SACL-absence readback cannot be performed or proved by the current ordinary test token. It has no `SeSecurityPrivilege`, and the caller-supplied directory handle does not carry `ACCESS_SYSTEM_SECURITY`.

This is not permission to weaken the check. The final source explicitly asks `GetSecurityInfo` for owner, group, DACL, and SACL, and fails closed when the descriptor cannot be read. No fallback that treats an unobserved SACL as absent was added.

Model/reasoning setting: inherited Codex GPT-5 session / inherited reasoning setting. First author-controlled write was observed at `2026-08-28T02:31:46+08:00`; pre-edit baseline work occurred before that write. Freeze recorded at `2026-08-28T02:52:00+08:00`.

## Starting boundary

The pre-edit control documents matched the required identities:

- `docs/coordination/W24_PROGRAM_CONTROL.md`: `e8063eacf1e0bfe24181972266897fed8b41e7f3d2160eb61a51e23db502a625`
- `docs/coordination/W24_WORK_PACKAGE_REGISTRY.md`: `ba3c89efe161902f0e5b5165a42a4bf14d05fde785582f4b0bf3397369cd13a1`
- `docs/coordination/W24_EVIDENCE_INDEX.md`: `cfa3446e8bd995cb066d94886fd8a74591c3d91a52ab5873ff246e24751bdae8`

The required ordinal/LF baseline replay passed before edits:

| Scope | Files | SHA-256 |
| --- | ---: | --- |
| Broker | 38 | `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760` |
| Broker.Tests | 17 | `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60` |
| Protocol | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| Protocol.Tests | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| desktop schemas | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |
| ServiceHost | 8 | `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103` |
| ServiceHost.Tests | 6 | `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c` |
| solution | — | `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689` |

All three permitted D1 product/test targets were absent before the first edit.

## Frozen changed files

Only these three product/test files were authored:

| File | SHA-256 |
| --- | --- |
| `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs` | `f253647c8e24611d1908113b3d2c22eee1d5964ce915debc1efc6a885cc47731` |
| `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs` | `abbbe04b3e48c7dc5d0dec656df28174db19107d3edfc663d97c3c33a847fb32` |
| `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs` | `2fff247e51a9ee5b282c343ae0af72fe3fb78c55db59c379096e42758fb300d7` |

The deterministic changed-source manifest is [changed-source-manifest.txt](../../../.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/changed-source-manifest.txt), SHA-256 `3dde2adc5b55cb4ae8fd65bed9e0c3507f0b8672696f3ca243485562108412e9`.

This handoff intentionally has no self-hash. Generated validation files and `bin/obj` are excluded from the changed-source manifest.

## What is present, but not accepted as complete

- An internal sealed canonical durable profile with typed digest and distinct exact root/store-file control expectations (`D:PAI` for the NTFS root readback and `D:P` for explicitly created store files).
- A caller-handle-only native store shape using fixed single segments, handle-relative native opens, no-follow flags, exact descriptor/readback predicates, local NTFS/nonremote/nonreparse/single-link predicates, exclusive lock, HMAC-SHA-256 record chaining, protected random key handling, pending/create-new/flush/reopen/no-replace publish, monotonic generation, 32-byte nonce consumption, volatile issuer epoch receipts, and explicit secret zeroing.
- Tests for canonicalization, generation, nonce replay, restart receipts, tamper/pending artifact/concurrency/handle negatives, surface/ABI shape, disposal, and record binding.

Those source-level statements are not acceptance proof. In particular, no claim is made for TPM, rollback resistance on offline volumes, power-loss durability, loaded-image equivalence, signature/installer trust, or production policy activation.

## Blocking evidence and command results

`whoami /priv` showed that the executing token has only `SeShutdownPrivilege`, `SeChangeNotifyPrivilege`, `SeUndockPrivilege`, `SeIncreaseWorkingSetPrivilege`, and `SeTimeZonePrivilege`; it has no `SeSecurityPrivilege` entry.

Final observed locked build command:

```powershell
dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true
```

Result: exit `0`, `0` warnings, `0` errors.

Final strict prerequisite probe:

```powershell
dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~CreateNewPublishesFirstGeneration --logger "trx;LogFileName=d1-sacl-prerequisite.trx" --results-directory .codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/tests
```

Result: exit nonzero; `0` passed, `1` failed, `0` skipped. The failure was the intended fail-closed result: `The durable security descriptor could not be read back.` It occurs while acquiring the supplied root after SACL is requested. Receipt: [d1-sacl-prerequisite.trx](../../../.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/tests/d1-sacl-prerequisite.trx), SHA-256 `d21c7040a8241711d0689c90a14bfce15f3013a86fd0325ff50c8bcd00fe7171`.

Earlier diagnostic runs are retained, not substituted for acceptance:

- `d1-unit.trx`: `3` passed / `14` failed / `0` skipped of `17`; the fixture's attempt to write SACL failed with `SeSecurityPrivilege` not held.
- `d1-unit-r2.trx`: `4` passed / `13` failed / `0` skipped of `17`; root descriptor control readback exposed the genuine NTFS `D:PAI` distinction.
- `d1-probe.trx`: one failure exposing root SDDL `D:PAI`.
- `d1-probe-r2.trx`: one failure exposing explicitly created file SDDL `D:P`.
- `d1-probe-r3.trx`: one failure exposing `NtSetInformationFile` publish rename `STATUS_ACCESS_DENIED` (`0xC0000022`) after a root opened with `FILE_ADD_FILE`; this remains unresolved. A later `FILE_DELETE_CHILD` fixture change was not re-run because the SACL prerequisite is a prior terminal blocker.

All durable test and cleanup receipt hashes are listed in [receipt-hashes.txt](../../../.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/receipt-hashes.txt), SHA-256 `b578f305867d998567aac977fa11606b7d53c77d76b0f33f7f03e17f892d1757`.

The required full Broker regression, Release solution build, Broker `W24FS001`/23 smoke, source/PE/native/ABI forbidden-surface audit, final deterministic whole-workspace zero-residue audit, and independent audit were not run or passed. They must not be inferred from the successful local compile. After STOP, the controller expressly authorized cleanup of the exact `14` failed-fixture scratch roots found under the OS temporary directory. Their full chain and children were revalidated as non-reparse and expected-only before mutation; `0` leaf files and `42` directories were removed bottom-up without recursive or wildcard deletion. The post-check found `0` roots with the fixed `vfxcomposer-durable-profile-` prefix. This limited scratch cleanup is recorded in [temp-residue-cleanup-receipt.txt](../../../.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/temp-residue-cleanup-receipt.txt), SHA-256 `c267e36c713d3a0b2ff71a18109111090ab848e3689a3d5ed8a2451c534a7fbb`; it does not make the omitted final audit pass.

## Self-audit and next dependency

| Severity | Finding |
| --- | --- |
| P0 | Exact SACL absence cannot be read or proved with the supplied root/current token. Accepting it would create a security false positive; the code instead rejects. |
| P1 | Publish rename returned `STATUS_ACCESS_DENIED`; required target-directory access/handle semantics are not yet established. |
| P2 | Required final regression/smoke/scans/audit are absent because P0 prevents scoped GO. |

The next independently owned dependency is I1: provision and pin a local NTFS root with a handle that has the needed artifact-operation rights **and** `ACCESS_SYSTEM_SECURITY`, under a token with enabled `SeSecurityPrivilege`; it must make exact SACL readback possible. That dependency must also establish the valid no-replace rename access shape. D1 must not synthesize, discover, elevate, or mutate that prerequisite.

No network, global DOS-device, SCM, project, listener, policy-loading, Desktop, Worker, Unity, or authority operation was intentionally added. Static/PE proof of that statement was not completed and is not claimed.

**STOPPED. No continuation into I1, C1, W1, or any other package.**
