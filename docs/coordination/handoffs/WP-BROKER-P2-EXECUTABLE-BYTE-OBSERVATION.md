# WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION handoff

Status: **CLOSED / SCOPED GO — DORMANT_PINNED_EXECUTABLE_HANDLE_HASH_OBSERVATION_ONLY; final independent audit P0=0/P1=0/P2=0.**

Model: `gpt-5.6-terra`; reasoning: `max`.

The historical original implementation window was `2026-08-27T14:49:14Z` through
`2026-08-27T15:06:00Z`. This package creates only a bounded Windows
observation fact for an already-open borrowed file handle. It makes no claim
that the handle identifies a loaded service image, that its bytes are signed
or trusted, or that it confers any production capability.

## Historical original closeout and frozen boundary

All four allowlist targets were verified absent before the first source write.
The final replay excludes only this package's two Broker and one Broker.Tests
additions, and reproduces the required starting state exactly:

| Root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 36 | `2a8805352c20259f1c2b06b00102f83e7982f178b4de550072595cb126af98c1` |
| `services/VFXComposer.Broker.Tests` | 16 | `1ff9e8b497598c6a0f0a620657e24bbad04542e5e048974308cb6754393fac75` |
| `src/VFXComposer.Protocol` | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| `src/VFXComposer.Protocol.Tests` | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| `docs/schemas/desktop` | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |
| `services/VFXComposer.Broker.ServiceHost` | 8 | `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103` |
| `services/VFXComposer.Broker.ServiceHost.Tests` | 6 | `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c` |

The solution, four frozen Unity files, Program/BrokerPolicy, W24 control,
Registry, Evidence, ADR-002/003, Phase plan, and Phase 2 report also replay
exactly. The detailed receipt is
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/baseline-and-frozen-replay.txt`
(`37ae31c38dbb13aec8aa1450781b1ef302523a4619980f5dc4a224ead6bfaa4d`),
with `overallPassed=True`.

## Historical original implementation

- `WindowsPinnedExecutableContentObserver.TryObserve` takes only a borrowed
  `SafeFileHandle`. It brackets that source with `DangerousAddRef`/
  `DangerousRelease` but never closes, disposes, duplicates, or moves it.
  `ReOpenFile` obtains the independently owned `GENERIC_READ`,
  `FILE_SHARE_READ` handle; inheritance is cleared and rechecked on that new
  handle. There is no path input, output, reopen, fallback, or final-path API.
- The owned handle must be a disk file and must reject directory/reparse,
  non-single-link, zero-length, oversize (`>67108864`), invalid/closed, and
  share-conflict cases. Before, between, and after two reads it replays volume
  serial, 128-bit file ID, attributes, link count, size, and last-write time.
- Both reads start at position zero on the owned handle and stream the exact
  `TypedHash.Compute` byte encoding for `vfxcomposer.executable-content/1`.
  Each requires exact expected bytes plus an EOF probe; both hashes and all
  three metadata snapshots must match before a result is returned.
- The output is internal, sealed, immutable, and contains only a `TypedHash`,
  byte length, and opaque private-field native file identity. It exposes no
  path or handle. Exceptions fail closed without native status or caller data.
- `HostBootstrapExecutableContentCorrelation` remains an internal Boolean-only
  non-authority comparison. It first invokes the complete existing
  `WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate`
  binding, then fixed-time-compares the observed hash and exact byte length
  against both the candidate and expected identity.

## Historical original authored source manifest

Only the registered three source/test files were created. The manifest is
repository-relative, forward-slash, ordinally sorted, lowercase SHA-256, two
spaces, LF-terminated, and UTF-8 without BOM; it excludes this handoff:

```text
f3a9e44a74a7751238b1b0f84113717a2995efbbafab23471feca54c36fcdf1a  services/VFXComposer.Broker.Tests/WindowsPinnedExecutableContentObserverTests.cs
611ed53610ad306de04cb3afc87d2847e58544cd8eea343f9806569216f8bfaa  services/VFXComposer.Broker/Configuration/HostBootstrapExecutableContentCorrelation.cs
6f74c75b83ea3deebca3fbc2ab22b2d573557f7abc77ea8f0b51a73314e1920d  services/VFXComposer.Broker/Native/WindowsPinnedExecutableContentObserver.cs
```

Receipt:
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/manifests/changed-source-manifest.sha256`
with SHA-256 `3d533354cf6c4ccea36f32cfb94c07af9305dd82b137d6d024919375e1cb3e4c`.

Final source aggregates are recorded in
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/manifests/final-source-aggregates.txt`
(`6f4d89adae78ccc217ed866b5c564c8901330b15c3834c16f413aa5381bf0ea5`):

| Root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 38 | `3eaef0a33edce8f9a8d0d34fe3986291fc44b52c80a8513b05cb6171043ca04b` |
| `services/VFXComposer.Broker.Tests` | 17 | `5ef2d6d3fe8baf533b003221141315c9564153de5f9cf6ffa7095ec4c2985c35` |

All other aggregate roots and the solution remain frozen as recorded in that
same receipt.

## Historical original validation and receipts

1. `dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, `0` warnings, `0` errors.
2. `dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=broker-executable-byte-observation-final.trx" --results-directory .codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/test-results` — exit `0`; **76/76 passed**, 0 failed, 0 skipped. The eight new tests cover exact golden parity; borrowed ownership/position preservation; invalid, closed, directory, zero, oversize, and share-conflict rejection; hard-link rejection; hash/length/full-policy mismatch; immutable opaque Boolean-only surface; bounded product source; and native ABI imports/layout.
3. `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, `0` warnings, `0` errors.
4. `dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll` — exit `23`; stdout is 0 bytes and stderr is exactly `W24FS001` followed by CRLF. Stdout SHA-256 is `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; stderr SHA-256 is `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06`.
5. The source/cleanup scan found 0 matches across 27 forbidden operational tokens, verifies `ReOpenFile`, exact EOF, the single-link/reparse predicates, full policy binding, no Program/BrokerPolicy wiring, no recursive cleanup token, an intentional `DeleteOnClose` sentinel, and zero test scratch residue. Receipt: `validation/receipt-closeout/forbidden-source-and-cleanup-scan.txt` / `63f24d96e4353956fed146b3917d8f9ccb626639e09879370eed21008bc7fef8`.
6. The compiled PE/ABI scan reports exactly four new internal sealed types, zero public constructors, exactly eight required native imports, and native layouts `8`/`52`/`24`. Receipt: `validation/receipt-closeout/forbidden-pe-and-abi-scan.txt` / `8eb16d2c5c1620223e7913d61e87bb21009cc11942ae0a4efa2df9e7c5295d8f`.

The final TRX is
`validation/receipt-closeout/test-results/broker-executable-byte-observation-final.trx`
(`3a432d02ec3c58cd085dc3c9690dcb30ceb8d686e8a8b238d58f4d7361c562a4`).
All command stdout, stderr, exit, binary, scan, manifest, and collector
receipts are bound by the deterministic 24-entry evidence manifest:

```text
.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/evidence-artifact-hashes.sha256
ec566dad7b82fdfc97c853447e1a67a479b960ad4b8cad20f8c99f50fca07f9c
```

The fixture uses exact known-leaf removal, a `DeleteOnClose` sentinel, and the
existing `PinnedScratchTreeCleanup.DeleteExactEmptyTree`; it performs no
recursive deletion. `DeleteOnClose` is deliberately not placed on an observed
file because the observer's required independent reopen is read-share-only and
a delete-access fixture would require `FILE_SHARE_DELETE`.

## Historical boundaries proved and explicitly not proved

Proved only: the supplied borrowed handle can produce one bounded dormant
content observation if the independent reopen, double exact read, and stable
metadata conditions all hold; that fact can be Boolean-correlated with the
already-existing in-memory policy binding. The direct Broker entry remains
fail-closed before an operational path.

Not proved or enabled: equivalence to a loaded service image; image location or
name; signature, Authenticode, signer, certificate, or chain validation;
process/token observation; service installation, registration, configuration,
or start; SCM/registry mutation; ACL application; production issuer, policy
activation, listener, connection, project access, Worker/Desktop/Unity route,
command, mutation, verdict, L3/L4, or authority.

Coverage residual: the hard-link negative is exercised with a real exact
nonrecursive runtime setup. A runtime reparse-point negative is not claimed:
the required source predicate is present and statically scanned, but a safe
privilege-independent reparse setup/cleanup was not assumed. It remains for
the independent audit to classify rather than being represented as tested.

## Historical self-audit and remaining blockers

- P0: `0` self-audit findings. No Program/BrokerPolicy wiring, production
  surface, operational listener, service mutation, project route, or authority
  result was added; direct Broker smoke remains `W24FS001`/`23`.
- P1: `0` self-audit findings. Observation fails closed unless both exact
  streams and all snapshots agree; correlation requires the complete policy
  binding plus exact hash and length.
- P2: `0` self-audit findings. New product types are internal, sealed,
  immutable/non-wire and the source/PE/ABI/cleanup scans pass. The explicitly
  recorded runtime-reparse coverage residual is not a finding or a success
  claim.

Required next dependency is a fresh independent read-only audit of this exact
package, including the residual classification. The user-reported quota is
stale after this atomic closeout; no next writer or broader behavior is
started here.

## P1 remediation closeout

The fresh audit found two P1 issues in the historical closeout: `ReOpenFile` had no native local-versus-remote volume-device fact, and exact scratch leaf cleanup used `File.Exists`/`File.Delete` paths.

Before remediation source writes, replay passed the historical source manifest `3d533354cf6c4ccea36f32cfb94c07af9305dd82b137d6d024919375e1cb3e4c`, the 24-entry evidence manifest `ec566dad7b82fdfc97c853447e1a67a479b960ad4b8cad20f8c99f50fca07f9c`, prior handoff `1ed326ff081c6da72d824dfb0b33fa06925ed3184b93f213112a16194e9914aa`, Broker 38/`3eaef0a33edce8f9a8d0d34fe3986291fc44b52c80a8513b05cb6171043ca04b`, and Broker.Tests 17/`5ef2d6d3fe8baf533b003221141315c9564153de5f9cf6ffa7095ec4c2985c35`. All frozen roots, solution, Unity files, control documents, ADRs, phase documents, Program, and BrokerPolicy also matched. Refreshed frozen replay receipt: `baseline-and-frozen-replay.txt`, SHA-256 `547c1ceca284cde53b5d942c202f1a08179d645c46dd4eaae18a73246ed12744`, `overallPassed=True`.

The observer now invokes `NtQueryVolumeInformationFile(FileFsDeviceInformation)` only on its owned `ReOpenFile` handle. Its ABI-correct `IO_STATUS_BLOCK` and `FILE_FS_DEVICE_INFORMATION` accept only `STATUS_SUCCESS`, successful I/O status, `FILE_DEVICE_DISK`, and no `FILE_REMOTE_DEVICE` bit; query failure, remote device, and unknown device type fail closed. No path, final-path, volume-name, drive-type, or network probe was added, and callers still provide only a borrowed `SafeFileHandle`. The local scratch-file success exercises the native query; a deterministic predicate test rejects remote/unknown values and ABI coverage verifies the ninth import plus `IO_STATUS_BLOCK` (`2 * IntPtr.Size`) and the eight-byte device layout.

`ExactScratchTree` now records each created leaf's exact segment, native file ID, attributes/non-reparse state, and expected link count. It pins physical scratch/repository/project directories; opens every leaf using one-segment `NtOpenFile` relative to the project pin with `OBJ_DONT_REPARSE` and `FILE_OPEN_REPARSE_POINT`; completes all preflight with all pins held; then requests deletion with `SetFileInformationByHandle`. Cleanup attempts every action and aggregates failures. The hard-link test preflights both exact names as the same identity with two links. There is no `File.Exists`, `File.Delete`, or recursive `Directory.Delete` cleanup; existing `PinnedScratchTreeCleanup.DeleteExactEmptyTree` remains directory-only.

Current authored source manifest:

```text
98bf30ce1fd5aefe22b289134216ec5c5b73704254e4a6ebce7d5ce9ac8ea2b3  services/VFXComposer.Broker.Tests/WindowsPinnedExecutableContentObserverTests.cs
611ed53610ad306de04cb3afc87d2847e58544cd8eea343f9806569216f8bfaa  services/VFXComposer.Broker/Configuration/HostBootstrapExecutableContentCorrelation.cs
034f7f4ea5e010bae699b821c6098dd87f4f87a61140ecad0265be0739c3c265  services/VFXComposer.Broker/Native/WindowsPinnedExecutableContentObserver.cs
```

The manifest SHA-256 is `571974b214be8f9146103a1bc06b7e98fe1a47eab10dae97166290d6731ac979`; final aggregates are Broker 38/`8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760` and Broker.Tests 17/`66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`; all frozen roots and solution remain byte-identical.

Validation: Broker.Tests Release locked/no-restore build exit `0` with 0 warnings/errors; Broker.Tests **78/78 passed**, TRX SHA-256 `c5a5cb0336e1b83c0cb003e1af03388fc886f7679b7f4ff4b96b6ade985a19d8`; full Release solution build exit `0` with 0 warnings/errors; Broker smoke remains stdout empty/stderr exact `W24FS001`/exit `23`. Product/cleanup scan matched 0 of 30 forbidden product tokens and proves volume gating, no Program/BrokerPolicy wiring, explicit unsafe cleanup rejection, and zero residue: `670ad28bb342c2f08e40c78c03a77a095d70e46fef24c77032ee0741c848b699`. Compiled ABI scan proves four internal sealed types, zero public constructors, nine imports, and layouts `8`/`52`/`24`/`16`/`8`: `d6b5471e2036b35da372e096e182f432c3a61c9098040582ee9a46772dc43bc9`.

The refreshed deterministic 24-entry evidence manifest is `.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/validation/receipt-closeout/evidence-artifact-hashes.sha256`, SHA-256 `cb88bd5b42cf2807a314c820a93c2e133833fe2028129c4e353c93a866545886`.

## Final independent audit and controller closure

Pre-controller-status handoff SHA-256: `2981449f9c0808e24c3a268b6c2faa82267ae226eb3f6ca6aa9a65a789314ac2`.

The final independent read-only audit of these exact remediation bytes returned P0=`0`, P1=`0`, and P2=`0`. It accepted only the bounded dormant observation and Boolean-only correlation boundary: the input remains a caller-supplied already-open handle, and no loaded-image, image-path, signature/Authenticode/signer/certificate, service-installation, SCM, production-wiring, project-access, authority, or Phase 2 claim is proved or enabled.

The runtime reparse-point negative remains an explicit coverage residual. The source predicate and static surface evidence are present, but no safe privilege-independent reparse setup/cleanup was assumed; it is not represented as a runtime success claim.

The user-reported `14%` quota is stale after this final `STOPPED`. No later package may be published until the percentage is refreshed; the `<=7%` no-new-writer and `<=5%` hard-stop rules remain in force.

This handoff intentionally does not hash its final status delta. **STOPPED.**
