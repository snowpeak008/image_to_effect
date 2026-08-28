# WP-BROKER-P2-LIVE-ATTESTATION handoff

Status: **STOPPED** — `DORMANT_WINDOWS_PROCESS_TOKEN_PATH_OBSERVATION_FOUNDATION_ONLY`. This package remains unwired from `Program.Main`, `BrokerPolicy.TryLoadProduction`, listeners, registration, ACL application, project access, Worker/Desktop/Unity, authority, L3, and L4.

Model: `gpt-5.6-terra`; reasoning: `max`.

Implementation and receipt-closeout window: `2026-08-27T05:06Z` through `2026-08-27T05:47Z`.

## Exact scope and authored files

Only the registered three source/test files and this handoff were authored. Generated evidence is confined to `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/` and normal `bin/`/`obj/` paths.

| Path | SHA-256 |
|---|---|
| `services/VFXComposer.Broker/Security/WindowsServiceProcessAttestation.cs` | `7331c5eb40778b764e79bf6d9a01297bce94ccdae0194308b89ead6a03704af6` |
| `services/VFXComposer.Broker/Configuration/HostBootstrapAttestationAdmission.cs` | `e17f2062580f26b8c8072f5e2cbe77d0b50f5022c158c4f6e9dc32a07dfcae27` |
| `services/VFXComposer.Broker.Tests/WindowsServiceProcessAttestationTests.cs` | `f2f5a4239eec0a241168302f85a1ccaa6cee86c75e05807729c55adcb45ac6fe` |

The changed-source manifest is ordinally sorted by repository-relative forward-slash path, uses lowercase SHA-256, two spaces, literal LF, UTF-8 without BOM, and excludes `bin/`/`obj/`:

- `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/changed-source-manifest.sha256`
- SHA-256: `f4dee3f195026f85901d63e48eeee4a0e770f930d394a3f227497d338b8c1954`

## Bounded security model

- A PID is only a locator. The only production `WindowsServiceProcessAttestation.TryObserve` overload privately creates the sealed native pin, with query/synchronize-only process rights, `inheritHandle: false`, and an independent inheritability check before fact derivation. Production metadata contains no `IWindowsServiceProcessAttestationPin` and no caller-injectable pin, factory, or delegate overload.
- The native pin derives/replays exact PID, creation epoch, process liveness, Windows session, token user/groups, and a native image-path observation. The configured service SID must occur exactly once in `TokenGroups`, with `SE_GROUP_ENABLED` set and `SE_GROUP_USE_FOR_DENY_ONLY` clear. `TokenUser` is retained privately and is never treated as the service SID.
- The image result is only a private `\\Device\\...` OS path observation from `QueryFullProcessImageNameW`. `HasExecutableContentIdentity` is explicitly false. No executable byte/content identity is claimed.
- `HostBootstrapAttestationAdmission` remains a one-use, in-memory, non-authority correlator. It returns only an opaque native observation and never a policy, listener, ACL, registration, native handle, serializable DTO, secret, verdict, or authority result. It consumes bootstrap material before observation and retains a local candidate until it rechecks state, time, and full static correlation under its gate; an untransferred candidate is disposed in `finally` exactly once.
- There is **no fabricated successful live service attestation**, no synthetic active-pin lifecycle, and no claimed executable-content identity. Deterministic tests operate only on non-authoritative structural facts; the only OS-local observation test is a current-process negative/fail-closed check and is not an installed service or issuer receipt.

## Injection-surface P1 closure

The prior internal injectable-pin seam was removed rather than relabeled. The production DLL/source test verifies all of the following:

- no `IWindowsServiceProcessAttestationPin` metadata type;
- exactly one `TryObserve(expectation, out observation)` overload and one non-injectable admission correlate entry point;
- sealed, non-public native pin type with no public constructor;
- no pin/factory/delegate parameter on either entry point;
- the native pin open call is confined to the attestation primitive; and
- the admission retains the post-observation state/correlation recheck and candidate `finally` disposal.

## Frozen replay and final aggregates

The receipt replay excludes exactly the three source/test additions above. All six registered starting aggregates, the four frozen Unity files, `Program.cs`, and `BrokerPolicy.cs` match.

| Root | Count | Final SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 32 | `e5b7fad903c584422c8c5afa2e810a9fdfeebf34b3d765a527aea935a5f9f3ac` |
| `services/VFXComposer.Broker.Tests` | 14 | `f345f0ba8b8b32619a8bf6fde1cd9702a3bd390f89b488e6c49a9066b23456e2` |
| `services/VFXComposer.Broker.HandleProbe` | 4 | `5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8` |
| `src/VFXComposer.Protocol` | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| `src/VFXComposer.Protocol.Tests` | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| `docs/schemas/desktop` | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |

`Program.cs` remains `e2cbc1feb5143a8067d630e2eb39c28e5170747eecbccdbef59b5c9b5ddbbb0a`; `BrokerPolicy.cs` remains `38719aaf670134b36d9bf53e5901a52b11c44bac5f769a8930f8ed3a39c5f321`. The final source aggregate receipt is `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/final-source-aggregates.txt` / `e7e7832fbb51c9a2d0ecf7ff24a0df0484527f483160747da49f0fb54fd38b42`.

The baseline/frozen replay receipt is `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/baseline-and-frozen-replay.txt` / `9156587dc6f6cbc79996fd76d900f76cc610d3af9ec01014dc1202bd69d7f6ff`, with `overallPassed=True`.

## Validation and durable receipts

1. `dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, 0 warnings, 0 errors. stdout `a2026cabd6330a054b5d41544370a15ac823f794289d743c0e3d9f49f0231800`; empty stderr; exit receipt `9a271f2a916b0b6ee6cecb2426f0b3206ef074578be55d9bc94f6f3fe3ab86aa`.
2. `dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=broker-live-attestation-p1-no-injection.trx" --results-directory .codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/p1-no-injection/test-results` — exit `0`; **54/54 passed**, 0 failed, 0 skipped. stdout `7b0aad376f5208e1437579655605a055cf0020d4a89d011e1b744941a361d9e4`; empty stderr; exit receipt `9a271f2a916b0b6ee6cecb2426f0b3206ef074578be55d9bc94f6f3fe3ab86aa`; TRX `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/p1-no-injection/test-results/broker-live-attestation-p1-no-injection.trx` / `21ba84b4110b75e1cf80fe33af15dae17335f0145e4eaae71b877db865bb60d5`.
3. `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, 0 warnings, 0 errors. stdout `1718cdbfc450ee2795769f8ac3d4b203a50f6d427df7fbc44a4bf6245c86ebb9`; empty stderr; exit receipt `9a271f2a916b0b6ee6cecb2426f0b3206ef074578be55d9bc94f6f3fe3ab86aa`.
4. Controlled smoke: `dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll` — expected exit `23`, stdout 0 bytes, stderr `W24FS001`. stderr `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06`; exit receipt `076320a2a08267b4c026d06573bba408ea68841e73cdc20e62cce59de165ece3`.
5. Static/call-order review passed: no injection interface/delegate/factory parameter, exactly one native observation/open call, retained candidate recheck/finally, no listener/network/project/environment/SCM/registry/raw-handle surface, and frozen `Program`/policy gate ordering. Receipt `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/static-surface-and-call-order.txt` / `62afa1244b17587123be7554a1505e1c6ab12cf71fcfeac0d072050470c3fc6f`.
6. Native ownership review passed: sealed/private-constructor native pin, non-inheritable process/token checks, query/synchronize-only process rights, exact service-SID predicate, interlocked unique close, observation revocation close, and admission candidate cleanup. Receipt `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/native-handle-lifecycle-scan.txt` / `5e4c3ab157b6a7ca7f83896663cbd8bf4b92a05e451cc38732e8bf89911a411f`.

The final evidence manifest excludes itself and this handoff, has 18 replayed entries, and is `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/validation/receipt-closeout/evidence-artifact-hashes.sha256` / `2fca4efa60a55f01e6fabfa61bd71ee4ab5210d178e90ebd6e7d82755882cb4c`.

## Findings and remaining blockers

- P0: none found in this bounded dormant package.
- P1: closed for the prior caller-injectable pin path; production metadata has no such interface or overload, and the candidate transfer remains fail-closed.
- P2: none found in this bounded package. The absence of a successful installed-service proof is intentional and is not represented as a pass.
- Remaining production blockers: an independently privileged installed host/service and issuer; SCM/install lifecycle; durable authenticated profile identity; successful live service process/token observation under the real configured service SID; a trustworthy executable-content identity bound to the loaded image; live service/session supervision; actual named-pipe ACL application; policy activation; listener/peer admission; project access; Worker/Desktop integration; authority; L3; and L4.

This handoff intentionally does not hash itself. **STOPPED.** Do not extend this package into service installation, policy loading, listener creation, ACL application, project I/O, Worker/Desktop/Unity integration, or authority without a separately authorized package and independent audit.
