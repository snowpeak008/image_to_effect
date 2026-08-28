# W24 Phase 2 Unity Worker handle-admission report

Date: 2026-08-26  
Status: `TEST_ISSUED_SINGLE_SESSION_OPAQUE_HANDLE_ADMISSION_LIFECYCLE_SCAFFOLD_SCOPED_GO / PRODUCTION_SESSION_ISSUER_PENDING / PRODUCTION_ACK_NO_GO`  
Authority: none. This report grants no production session, ACK, transport, project read/write, Worker command, machine/visual verdict, user sign-off, L3, L4 or Publication authority.

Independent frozen-byte audit: `P0=0 / P1=0 / P2=0` for this test-issued, single-session lifecycle scaffold only. It is not the Phase 2 gate or production authority.

## 1. Delivered boundary

This slice adds a bounded Unity Worker-side owner for the three already-duplicated directory handles represented by a sealed `WorkerProjectHandleGrant`. It is a test-only admission and lifecycle scaffold, not a production connection.

- Admission starts from a private, test-issued authenticated session. A no-`UNITY_INCLUDE_TESTS` build has no test issuer or test hooks, and its session constructor fails closed with `WORKER_SESSION_ISSUER_PENDING`.
- The session claims each exact grant self-hash and raw-handle tuple before native handle ownership is attempted. Exact sequential or concurrent replay in that session returns the same opaque lease while it remains attached; a grant bound to another session is rejected before raw-handle touch. A failed admission remains consumed within that session so a stale raw value cannot be retried after close. Global single-live-session/process ownership remains a production-issuer responsibility and is not claimed by this scaffold.
- Admission, session revocation and disposal are linearized. In-flight admission is included in revocation; session disposal waits for candidate cleanup and every attached lease close. Lease replay and disposal share one gate, and repeated lease/session disposal waits for the winning close to finish.
- The opaque lease owns exactly the volume, repository and project-root handles. Its public/internal instance surface does not expose a raw handle, `SafeHandle`, byte payload, absolute path, route, verdict or authority.
- Each handle must be a non-inheritable, non-reparse NTFS directory on the same volume. The Worker independently replays volume serial, `FILE_ID_128`, final handle identities and the typed project identity used by Broker. Identity drift fails closed.
- The scaffold reads only native handle metadata. It does not enumerate or read project content, construct a caller path, acknowledge a grant or revoke, create a pipe/listener, call Unity project APIs, or mutate any project file.

The Broker remains the intended owner of authenticated peer facts and handle duplication. This slice does not create a production session issuer and does not connect the Worker to Broker.

## 2. Replay, ownership and revocation invariants

The session admits at most `MaxAdmissionsPerSession=1024` distinct grant hashes, so successful leases are bounded by 1024 and claimed raw-handle strings by 3072. The cap is checked before a fresh tuple is adopted or touched. The admission gate is held from pre-wrap validation through candidate creation and lifecycle attachment. Session disposal first waits for that admission gate, then marks the session unusable, snapshots attached leases under the lifecycle gate, and closes them before returning. A losing or invalid candidate closes its uniquely owned handles before releasing admission.

`W24S6WorkerProjectHandleLease.Dispose()` serializes with identity replay, closes project-root then repository then volume, and detaches only after close. `W24S6WorkerAuthenticatedSession.Dispose()` serializes all callers, stops new admissions, waits for in-flight work, snapshots attached leases, closes them outside the lifecycle lock, and returns only after those closes complete. This is a local lifecycle invariant only; it is not an ACK protocol or proof of an authenticated transport.

## 3. Validation performed

Current Release validation:

- `dotnet build VFXComposer.sln --configuration Release --no-restore`: 9 projects, 0 warnings, 0 errors.
- Release tests: Protocol 78, Client 8, Desktop 9, Broker 14; total 109/109, 0 failed, 0 skipped.
- `eng/verify-phase2-schemas.py`: 19 schemas, 10 Phase 2 schemas, 10 positive cases, 139 negative cases, PASS.
- Unity recovery compile completed with outer exit 0 before the accepted focused runs.

Accepted Unity r48 receipts:

| Exact filter | PID | Passed | Failed | Skipped/inconclusive | XML SHA-256 | Log SHA-256 | Outer receipt SHA-256 |
|---|---:|---:|---:|---:|---|---|---|
| `VFXComposer.Tests.EditMode.W24S6WorkerHandleAdmissionTests` | 34260 | 13 | 0 | 0 | `55765ac111054aca280305eee554116e09ea2a74e2d1679ca4f3ab4127ce3866` | `72900d32a93314230bef6467cc43ce27d5d0e167d818ad31a3b5786703494158` | `48927fa6a521da12788590bb0f6a95e0e9bdcc28e2549cc6d7f03fc91abd580c` |
| `VFXComposer.Tests.EditMode.W24S6WorkerProtocolTests` | 43616 | 6 | 0 | 0 | `fef936451d9d1a39cffa78d4b25f8b7146b2dd04433e8abde192c8ae9e1f2c26` | `16cfa5c68301c87753d7cf434662f77496ab2002eaae53ba05c6aa6195bea27c` | `c975e03091f2c94fe96924bac5b3e56ebeec87e52f03fbab52c4fb2c8fc6d770` |

Both accepted logs bind the exact filter and PID, save the result, then show Input shutdown, licensing disconnects, mono cleanup and application-shutdown cleanup. Earlier r43/r44 runs reached passing XML but did not complete the outer process before the bounded timeout; they are rejected as acceptance receipts. Prior-source r45/r47 natural exits are historical and are superseded by the current-test-source r48 receipts.

The 13 admission cases cover success and exact replay, cross-session rejection before touch, permanently consumed failed admission, inheritable-handle rejection, local FSCTL junction rejection without a network target, session revocation, revoke-during-admission, concurrent exact replay, changed grant using claimed raw values, concurrent session-dispose completion, registry-cap rejection before fresh-handle touch, and duplicate raw values with unique-value single-close accounting.

## 4. No-tests production-surface receipt

A current Unity Editor response file was derived by removing exactly `-define:UNITY_INCLUDE_TESTS` and redirecting output/ref-output. Compilation completed with exit 0.

| Artifact | SHA-256 |
|---|---|
| Source Bee response file | `f7fba154ae9ad32648e8bd71ddc6c7eaf9413a412bfaf7bba219a03312b08821` |
| Derived no-tests response file | `5789298a43ec146afaf8e8d51f970db10b6dfae778e72dd945c831c04448430a` |
| No-tests Editor DLL | `8bc3503950dbcbc7b79a36b56e3978b1165dac5f8f42be647ca8f367f81b06f2` |
| No-tests reference DLL | `1c83233d4125d5eb1cb2235989cd0c2789cf4b57d46c2496faf82206c1071e89` |
| No-tests PDB | `e862eae2dcec5c7f8d20757b758af1bbeace758f5eaf5253961ac23a4f29773b` |
| Metadata-audit DLL | `8c3c72b2204bfda389a31c30222409f36a298b236bac341a70c158c1da1f53ee` |
| Metadata-audit JSON | `ab5cb8cc9dad3de180ce9579146ee09cd9179f8e816ef4d4785705306231c924` |
| Compile receipt | `3e87882e4c1d2445e1e03894807b83bab89cf92386948b538c1dd7b5220839b7` |

The PE/CLI metadata audit does not load the target assembly. Within the Worker namespace it finds no `Acknowledgement`, `TestIssuer` or `ForTests` token. The authenticated-session and lease constructors remain private; named test issue/hooks/counters are absent; the production codec still exposes only grant/revoke decoding and contains no ACK-specific model or sealing entrypoint. Candidate creation is fixed to a session-private issuer token rather than a caller-supplied delegate.

## 5. Frozen source boundary

`W24_PHASE2_WORKER_HANDLE_ADMISSION_SOURCE_MANIFEST.sha256` enumerates the exact 15-file source/vector/test/meta set as `<lowercase SHA-256><two spaces><forward-slash repo path><LF>`, sorted with `StringComparer.Ordinal`. Its physical SHA-256, also the aggregate of those exact lines, is:

`245c308ecfa9b3a0a1bf4ba6405598b82905ebc7cf3d014f5fa5b1f978e7ab2e`

Key identities:

| File | SHA-256 |
|---|---|
| `W24S6WorkerHandleAdmission.cs` | `896940160d1eaf9f786b717dbc153007af0201537d24761a136a886135c7cb77` |
| `W24S6WorkerHandleAdmissionTests.cs` | `6962b6ed9de1d5d0039dbd4348416e78afc989cad9cbfdd4889daf75af863b18` |
| `W24S6WorkerProtocolCodec.cs` | `236659a73dc501376d2bb5fbfd94fd2478c8a7323dd4cc71673fd64bc3aef354` |
| `W24S6WorkerProtocolModels.cs` | `ff8b74c1e07d549b2c1411f9cc6e6f6fb0bdbfc0275faf647589551c676f7ff7` |
| `ProjectRegistrationStore.cs` | `bde9109cf1d45f1308b7820efe71d10fd6adceacf6568706351a767583b2f08c` |
| `FileIdentity128.cs` | `75d430f2f2449362ba6a501988b85cc9a506e40ad1bf6cc7895e70397904ea44` |
| `ProcessEpoch.cs` | `a365b6b25a2ecbf4a448119a2fa147079841db42e8ee23deb8caa92e7f446035` |

The frozen Unity UI files remain unchanged: `VfxStudioWindow.cs` is `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`; `VfxStudioModels.cs` is `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`. No r31/r32/r35/r36 receipt is rebound to this Worker slice.

## 6. Explicit blockers

1. No production authenticated Worker-session issuer or authenticated named-pipe loop exists.
2. No production grant ACK/revoke ACK issuer, ordered transport, retry/tombstone integration or Broker↔Worker live wiring exists. The future issuer must enforce one current session/global raw-handle ownership domain per exact Worker process epoch before issuing any grant.
3. No supervisor connects session/process termination to the Broker lifecycle state machine.
4. No handle-relative, no-follow allow-listed Library/Manifest/Contract/Trace content reader exists; this slice performs metadata replay only.
5. Client/Desktop remain disconnected and Broker production policy still returns `W24FS001` before listener creation.
6. Restart, backpressure, cancellation, service SID/ACL, DOS-device remap, installer and recovery threat gates remain open.
Therefore this is not the Phase 2 gate. Production connection, global process/raw-handle ownership, ACK, project read/write, Worker commands, Preview/Review, machine/visual/user authority and L3/L4 remain fail closed.
