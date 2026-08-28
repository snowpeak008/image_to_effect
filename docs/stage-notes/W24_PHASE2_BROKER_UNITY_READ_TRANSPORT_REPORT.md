# W24 Phase 2 Broker-to-Unity read transport checkpoint

Date: 2026-08-26  
Status: `BROKER_TO_UNITY_TEST_PIPE_READ_QUERY_SCAFFOLD_SCOPED_GO / PRODUCTION_PROJECT_READ_NO_GO / PHASE_2_GATE_NO_GO`  
Authority: none. This checkpoint grants no production connection, registration, project read or write, Worker command, machine/visual verdict, user sign-off, L3, L4 or publication authority.

## 1. Exact scope

This slice joins two previously audited test-only boundaries: the actual Unity 2022.3 Editor lifecycle connector and the Worker handle-relative fixed document reader. The non-publishable .NET HandleProbe creates a new GUID-owned scratch repository, pins its native roots, completes grant/ACK over one authenticated `CurrentUserOnly` named pipe, sends four fixed read queries plus one expected-content mismatch query, then completes revoke/ACK.

The four positive queries are exactly Library index, Manifest, Contract and Trace. The Broker sends only registry identity, lease identity/generation, kind, document ID and an optional typed expected-content hash. It never opens project content. Unity maps those identifiers to the previously frozen fixed relative targets and reads from the already-admitted opaque repository/project handles. The result returns no path or handle.

This remains a test scaffold:

- the Desktop session is created in-process by HandleProbe and is not a Desktop transport;
- the project is a fresh GUID scratch tree, not a registered real project;
- the entire Unity connector, including query framing, is compiled only with `UNITY_INCLUDE_TESTS`;
- HandleProbe remains non-publishable and is not a production Broker host;
- production Broker policy still returns `W24FS001` before listener creation;
- Client/Desktop have no query route and no result consumer.

No Unity main-window, five-tab, Player UI or MCP-in-Unity-window source changed.

## 2. Transport and fail-closed ordering

`WorkerReadQueryTransport` replays the Desktop session, registration, lease, Worker session and exact query identity before reserving the connection. It then acquires the same exclusive exchange gate used by grant/revoke, replays the route again, writes one bounded frame, strictly decodes one `ReadDocumentResult`, checks request/project/kind/document correlation and allowed fixed diagnostics, and replays the route after the response.

- Grant, read and revoke state transitions cannot reorder around the connection exchange gate.
- A malformed result, wrong correlation, pipe failure or strict-codec failure closes the connection and returns no result.
- Desktop/session/registration drift overlapping the response discards the bytes. The connection is left only for the ordered revoke path.
- A successful response must bind its content bytes to `vfxcomposer.document-content/1`; if the query supplied an expected hash, the response must match it.
- A rejected response contains no bytes and may use only the fixed `VFXP0007`, `VFXP0008` or `VFXP0009` catalog entries.
- The frame is bounded by the existing 1 MiB pipe limit; decoded document bytes remain bounded at 512 KiB.

The focused Broker tests add three cases: an exact successful read before revoke, wrong-correlation connection closure, and deterministic proof that read and revoke share one exclusive exchange. Broker Release tests now pass `25/25`.

## 3. Test-owned scratch and cleanup

The helper creates only these four files beneath a unique absent temp root:

- `repository/project/ProjectSettings/VFXComposer/LibraryIndex.json`;
- `repository/project/ProjectSettings/VFXComposer/BuildManifests/effect_fire.manifest.json`;
- `repository/docs/vfx-contracts/effect_fire.contract.json`;
- `repository/docs/vfx-traces/effect_fire.implementation-trace.json`.

Before cleanup, a second native root chain pins every owned directory by single-segment `NtOpenFile` with no-follow directory semantics. Each file is removed only while its exact parent handle replays; directories are removed bottom-up, non-recursively, only after the target handle is replayed and released while its parent remains pinned. Reparse points, missing owned entries, identity drift or extra directory content fail cleanup and suppress the helper `PASS` receipt. The accepted run leaves zero matching scratch directories.

## 4. Accepted evidence

### 4.1 Unity r11

Exact filter: `VFXComposer.Tests.EditMode.W24S6WorkerBrokerSessionTests`  
Runtime: Unity `2022.3.62f3c1`, Windows EditMode, `-batchmode -nographics`  
PID: `21512`  
Result: `2/2` passed, `0` failed/skipped/inconclusive  
UTC: `2026-08-26 14:24:23Z` to `2026-08-26 14:24:23Z`; XML duration `0.5201203` seconds  
Outer exit: `0`

| artifact | SHA-256 | bytes |
| --- | --- | ---: |
| `.codex_tmp/w24_phase2_worker_connection_r11/results.xml` | `c8bcf1c7222479d5f4e11be96d4365350f830744fbe858d23518931234c19563` | 4,088 |
| `.codex_tmp/w24_phase2_worker_connection_r11/unity.log` | `554ee9797bc40f8a196cf556677d54776f9d59741d4acd2853060976d732aca9` | 92,889 |
| `.codex_tmp/w24_phase2_worker_connection_r11/outer-exit.txt` | `10dcc6198f67fe72e0ce630f050cb0fd5f5ec621f766499b2cb4466804678875` | 22 |

The log binds PID `21512`, the exact filter and result path, then records result write, Input shutdown, licensing disconnects, `Cleanup mono` and the complete `Application.Shutdown.CleanupMono` path. After exit: Unity count `0`, HandleProbe count `0`, `project/Temp/UnityLockfile` absent and helper scratch count `0`.

r8 is rejected even though its XML is `2/2`: it stalled after results/licensing disconnect, never reached `Cleanup mono`, and exact PID `38200` was force-stopped after the shutdown timeout. Its zero-byte stale Unity lock was removed only after Unity count reached zero. r9 and r10 naturally exited with `2/2`, but their launch wrappers did not persist a valid outer exit receipt; they are diagnostics, not accepted evidence. r11 is the only accepted final receipt for this source slice.

### 4.2 .NET and schemas

The final Release solution build completed all nine projects with `0` warnings and `0` errors. Release tests passed `122/122`:

| assembly | result | TRX SHA-256 |
| --- | ---: | --- |
| Protocol | `80/80` | `9e89898c0964ee87aaa90b32f4a451d9b875bf1dc126400e6f5a9ae3b6e88f83` |
| Client | `8/8` | `8a47f7fecce4d30b5fbc1a70e01167f0191bf73aea72a77be7ba76b1b499f367` |
| Desktop | `9/9` | `7db572dfbcae2d9ed2968c900eb5ecfa89092332dadba9d186c47f2d2d000c68` |
| Broker | `25/25` | `0b7922c46845880f93912d8dced7aaac9a976bc19fa29fd8137560ef2cff83f2` |

Draft 2020-12 verification also passes unchanged: Phase 1 `9 schemas / 42 positive / 68 negative`; Phase 2 `19 total / 10 Phase 2 / 11 positive / 144 negative`.

### 4.3 Production-surface compile

The current Bee Editor response file has 476 lines. The derived no-`UNITY_INCLUDE_TESTS` response file has 475 lines, removes only that define, redirects output/ref-output, and has normalized diff count `0`. Compilation exits `0` with empty stdout/stderr.

| artifact | SHA-256 | bytes |
| --- | --- | ---: |
| current Bee Editor RSP | `f259bc0cbef5c58c2567d336c202491c9d0feb1f07a197932596a51421a5a483` | 41,210 |
| derived no-tests RSP | `66f100406a7c9edb3c56d06c52a4498e2775e0f2b133b8d6eb1d56a626774ea3` | 41,756 |
| no-tests Editor DLL | `307520645cbea4462a486e2ad823c76d3197bb240461a1fcdba065e05c87047d` | 1,667,072 |
| no-tests Editor ref DLL | `33cdc10e1fc0e9e2418249177f4746e6739d1b22c9c3e3d95301c2e8e896340d` | 276,480 |
| no-tests Editor PDB | `6b94527607b31ea7f82f60fd0b751b73937d0857470698b138258f472856c237` | 402,728 |
| metadata audit | `13fa8044165c790a483b47c438760a57eea1b67208e1deeb4056d1b1b86660c3` | 5,974 |

The metadata audit reports zero Worker-namespace `Acknowledgement`, `TestIssuer` or `ForTests` matches. Since the connector's callable entrypoint is test-named and the complete connector is guarded, no production connector/query pipe entrypoint is present. The dormant read codec/handler and opaque handle owner remain compiled but have no production session issuer or route.

Current binary identities:

- Unity Editor DLL: `817d6ec3570d9990bf29ec46ca13fcad81bf8cb146794283b0a2a0e4b24f20c7`;
- Unity focused tests DLL: `b99cfb28c35c18eae1bafaaff8c9bfde7ec0d4df81faf3ecee95780cf8efa106`;
- Broker DLL: `4718689f5454d36b2b5c34ea23a68bab379dd87adb06a80dc502410699ed9406`;
- Broker tests DLL: `0071a828bb5272dcb0fb5055c42c0d4361fa4e1e9b36d688d66b4c1d03dc9fbd`;
- HandleProbe DLL: `a41c2d823d7b5d9f33f32d270d14336002dd35fa18001e5aa16543c0aad74cb6`.

## 5. Source boundary and STOP-THE-LINE

`W24_PHASE2_BROKER_UNITY_READ_TRANSPORT_SOURCE_MANIFEST.sha256` enumerates the exact seven source/meta files as lowercase SHA-256 plus two spaces plus forward-slash repo path plus LF, sorted with `StringComparer.Ordinal`. It is 1,029 bytes, seven LF, zero CR, and its physical SHA-256 is `dede4a6abd0ca0b8ac2f0343c83bff7ded93f10c60378b808702fdc91470b17c`; replay issues are zero.

The old UI compatibility hashes remain unchanged:

- `VfxStudioWindow.cs`: `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`;
- `VfxStudioModels.cs`: `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`.

No r31/r32/r35/r36 or other old UI receipt is rebound to this transport.

## 6. Open blockers

This checkpoint does not close the Phase 2 gate:

1. no trusted production Broker policy/ACL/service issuer exists;
2. the Unity connector, session issuer and ACK owner remain test-conditional and absent from production;
3. no production supervisor owns Worker start/restart/termination, unresolved published handles, cancellation or recovery;
4. no bounded production query queue/backpressure policy exists;
5. no Desktop/Client project selection, query transport or result presentation exists;
6. no real registered project or trusted Library-index producer has been exercised;
7. no production installer/service lifecycle or cross-restart generation gate exists;
8. independent frozen-byte audit of the eventual full Phase 2 production implementation remains pending.

Independent frozen-byte audit completed on 2026-08-26 with `P0=0 / P1=0 / P2=0` strictly for `BROKER_TO_UNITY_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`. This scoped GO closes only the non-publishable GUID-scratch test route and grants no production connection/read, Client/Desktop route, command, mutation or authority. Blockers 1–8 remain open for the Phase 2 gate.

Therefore production project read remains fail closed. Worker commands, mutations, Preview/Review, machine/visual/user authority, L3 and L4 remain not granted.
