# WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR — FINAL STOPPED

## Identity and bounded result

Package: `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR`  
Scope: `PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`

- Writer model: `gpt-5.6-terra`.
- Writer reasoning: `max`.
- Receipt-remediation start: `2026-08-27T21:42:00.5000561Z`.
- Receipt-remediation end (evidence close): `2026-08-27T21:47:40.5816675Z`.
- Final independent audit: `P0=0 / P1=0 / P2=0`.
- Final-audit external handoff pre-status SHA-256 (audit input only, not a self-hash): `0a95a335e982527d5f1b5f9193547c40b0093c5832c01b6f4b88d8ee2b12698c`.
- Final state: `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`; final package state `STOPPED`.

This remediation changed no C2 product, test, vector, schema, or verifier byte.
The final controller-status delta is limited to this handoff and the three
designated W24 coordination documents; it adds no product capability.

This package adds only the pure C# Worker project locator and locator
acknowledgement wire contract. It is not a locator issuer or locator-ACK issuer,
project reader, transport, Worker connector, session issuer, handle grant, lease,
command, or authority surface.

The locator kind is `worker.project.locator`, the acknowledgement kind is
`worker.project.locator.ack`, and the vocabulary-only capability is
`worker.project-locator.v1`. The locator's typed self-hash domain is
`vfxcomposer.worker-project-locator/1`; the acknowledgement's is
`vfxcomposer.worker-project-locator-ack/1`. The acknowledgement binds the
exact locator self-hash and every shared request, registered-project,
generation, Worker-session, and Worker-process-epoch correlation; its sole
disposition is `LOCATOR_ACCEPTED`. It is structurally distinct from every
handle grant/revoke acknowledgement.

## Frozen pre-edit boundary

The required controller inputs were replayed before the first edit and again
at final freeze:

```text
4dafcb7c56c0d3ac7bc261efa86df20dc5385b694721fcd0ef428548c2d96790  docs/coordination/W24_PROGRAM_CONTROL.md
bd087941946363b6b2023fcc25bfada4e815fdb67216152c79068fe8ec963333  docs/coordination/W24_WORK_PACKAGE_REGISTRY.md
d24ce35043d1776f92f576e72ddcd3d8d1ada2e80aa8db5c421f48308fc98805  docs/coordination/W24_EVIDENCE_INDEX.md
e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141  docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md
```

All nine C1→C2 overlay pre-hashes matched the ADR-004/DAG-rebase ledger before
their first write. All seven new leaves were absent and had zero
case-insensitive collisions before their first write.

## Exact 16/16 source manifest

The manifest encoding is lowercase SHA-256, two spaces, repository-relative
forward-slash path, LF, UTF-8 without BOM, and `StringComparer.Ordinal` path
sort. Its final SHA-256 is
`f6095bb4324b33d7d196f0c0c858d52b879c79a079906b18c778781aceaade26`.

```text
4adc05e6f1c521d7cfeea3d55af1730cbec912669bd3768900b9d42ea783f65c  docs/schemas/desktop/vfxcomposer-worker-project-locator-ack-v1.schema.json
1df2d5628c9d363ab7ffdfa940f1e681c5b3ab5cb7c994a86298304bf5b6ff56  docs/schemas/desktop/vfxcomposer-worker-project-locator-v1.schema.json
e4f7a5cc78385d7401dc6a6903b5b18f7941b8af6ec55c398932ffa0c980765b  eng/verify-phase2-schemas.py
c72639a10d30238d01b224b44e9ce1f503a1bcf68d1326756234ebec73e5c2ec  src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs
a9ae21eae96749df7cdfeea4916be74926e27e55d19af8340f0ccb9d6bf8a492  src/VFXComposer.Protocol.Tests/GoldenVectors/desktop-phase2-worker-project-locator-v1.json
11724dfae7be4c1038b79cd73380835734257b4d1f12e12a13299401ea0d97a3  src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs
581dc8d69bed17925b53193e0b96a2353ba31c7bd96dd456c9779f678cc71b38  src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs
4faa08f41bf167f8f87560ee10f4fa73e09650c16d7ce57e81da1ddb4ea9a4de  src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs
1c479c10685a71092fd1fffe49924e2ca398c545b67e4debd99112fc8c35545c  src/VFXComposer.Protocol.Tests/WorkerProjectLocatorGoldenVectorTests.cs
593e622adb60eb887e0061193c4708915db5697d8a3345146d8e4cc886181ecf  src/VFXComposer.Protocol.Tests/WorkerProjectLocatorTests.cs
6e6b6b7a752f886a1a1893716dbc9ad8773cc6d791ba5206e8269cc93b115431  src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs
dce963b4cb2ac945216af8324bbc8ec2904a537b4337de31a754bc8804f64898  src/VFXComposer.Protocol/Json/StrictWireCodec.cs
3f963d94898ed82fa6df02154cf446130238849ca104ad269647f2e02048173f  src/VFXComposer.Protocol/MessageKinds.cs
4529c68ecc6c77d5e75a98d61739bc3652a48d3d491db90127066cbf73fcf3eb  src/VFXComposer.Protocol/Registration/WorkerProjectLocator.cs
2204c811e8fa5395dd80c5622b1d3a3a2134dd6ed9f21c9d8bdf1a5050053653  src/VFXComposer.Protocol/Registration/WorkerProjectLocatorAcknowledgement.cs
1dbb89307a61706c3e35693ae0b55d319b546d77ed039b24f4dd5697527f354a  src/VFXComposer.Protocol/WireSchemaRegistry.cs
```

There was no csproj, lock, solution, Broker, Client, Desktop, Unity, or product
documentation edit. The final status metadata is limited to this handoff and the
three designated W24 coordination documents. Generated outputs are limited to
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/**` and ordinary
`bin/obj` outputs.

The durable manifest is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/changed-source-manifest.sha256`
with the unchanged 16/16 SHA-256
`f6095bb4324b33d7d196f0c0c858d52b879c79a079906b18c778781aceaade26`.
It is lowercase SHA-256, two ASCII spaces, repository-relative forward-slash
paths, LF, UTF-8 without BOM, and `StringComparer.Ordinal` sorted. The freeze
receipt is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/remediation-source-freeze.json`
with SHA-256 `22ce27fa36e91f3de2e7afa3106773e8fc11da3bf71fb27d1a42cc8804ab92fa`.

## Sequential-overlay provenance

```text
727bb2c30dc7ba13fbb1425277942a04b1d67fc3d0053b6a80a2f48cf11190f7 -> dce963b4cb2ac945216af8324bbc8ec2904a537b4337de31a754bc8804f64898  src/VFXComposer.Protocol/Json/StrictWireCodec.cs
ef9d472566288ba926992a7b893ca3d4e837286abebb8a2bf905886588d2edde -> 3f963d94898ed82fa6df02154cf446130238849ca104ad269647f2e02048173f  src/VFXComposer.Protocol/MessageKinds.cs
9ca4a574e9631045240bd9cd07869fa9c9c3bb8910b8d6927f51977894b77934 -> 1dbb89307a61706c3e35693ae0b55d319b546d77ed039b24f4dd5697527f354a  src/VFXComposer.Protocol/WireSchemaRegistry.cs
1240071df65e980a2db27d66d7acebaa7958626e7a27dd9438c96ca392e74c70 -> 6e6b6b7a752f886a1a1893716dbc9ad8773cc6d791ba5206e8269cc93b115431  src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs
5e7c8f80064c931151aea189cf6fe4224e5fe4d23c3308b1018f4d2ea48105b9 -> 581dc8d69bed17925b53193e0b96a2353ba31c7bd96dd456c9779f678cc71b38  src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs
2acdb05f0cecc51476fed9997542c9b698fbd3a4eb4421f47d310ef939c0aa3d -> 4faa08f41bf167f8f87560ee10f4fa73e09650c16d7ce57e81da1ddb4ea9a4de  src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs
0a8b0781e25607aec6976338673d13971970478b139b48ec6b0199ba1e6ead74 -> c72639a10d30238d01b224b44e9ce1f503a1bcf68d1326756234ebec73e5c2ec  src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs
6ce16811d756c279e916c14773ad398aa0078be9791c67b6ff7d33596705641b -> 11724dfae7be4c1038b79cd73380835734257b4d1f12e12a13299401ea0d97a3  src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs
5f58482d48480fa55eba031e87177be283f9f3ccdd66b24af391d6057a4ee320 -> e4f7a5cc78385d7401dc6a6903b5b18f7941b8af6ec55c398932ffa0c980765b  eng/verify-phase2-schemas.py
```

## Provenance replay resolution

The retained C1 changed-source manifest still hashes to `27854e8504ec8e7da12e510db906efd9f2f780577d7a1b49e55c039d7da23600`,
and its self-excluded 10-payload receipt manifest still hashes to
`7402e367014a05b1d45d28df497de8f24563ce68d4cebed523745141583e1eef`.
All ten C1 payload hashes replay. Every one of the nine ADR-004 overlay
pre-hashes exactly equals the matching C1 manifest row, and every C2 final
hash equals the C2 source-manifest row.

Using the normative lowercase-SHA256/two-spaces/forward-slash/LF/UTF-8-no-BOM/
Ordinal encoding, deleting the seven C2 leaves and restoring the nine C1
pre-hashes reconstructs all C1 roots exactly: Protocol
`68/bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605`,
Protocol.Tests `22/672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03`,
and schemas `34/d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc`.
Thus no C1 baseline reapply or source edit is needed. The previously reported
Protocol/Protocol.Tests virtual-root discrepancy is not reproducible from the
retained C1 receipts under the required encoding; it requires a different
input set or noncanonical aggregation.

The durable report is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/c1-to-c2-provenance.json`
(`b0c3cb54686742613ab2caa3704c04d5fcb08dcc2c37469208d4f3233acdda85`),
with text companion
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/c1-to-c2-provenance.txt`
(`1c61aaad1346ae6ea47b7bcc3d82751e7e0598475f0d447ad917fcbecfa4b305`).

## Contract and negative boundary

The strict ingress, closed schema registry, DTO/schema parity, and frozen
golden-vector tests cover exact required/unknown fields; invalid UTF-8/BOM and
decoded duplicates; version/kind/type/domain/hash failures; nonpositive or
overflow generations; self-hash drift; cross-document request/project/session/
epoch/generation correlation; and path-shaped or authority-shaped additions.
The vector envelope is test data only, copied by the existing
`GoldenVectors/**/*.json` glob; it is not a runtime schema.

The locator and acknowledgement contain no caller location text, URI, drive or
volume GUID text, directory/root text, raw/native handle, lease, grant,
permission, status, Boolean acceptance, session issuance, command, or authority
wire field. Reflection and schema checks assert the exact allowed surfaces. The
product DTO files have zero forbidden I/O/network/process/native/Broker/Client
dependency matches.

## Validation and final replays

```text
dotnet build src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true
  PASS: exit 0; 0 warnings; 0 errors

dotnet test src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=protocol-worker-project-locator.trx" --results-directory .codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/test-results
  PASS: exit 0; 104 passed; 0 failed; 0 skipped

python eng/verify-phase2-schemas.py
  PASS: exit 0; 22 total schemas; 13 Phase 2 schemas; 14 positives; 236 negatives

dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true
  PASS: exit 0; 0 warnings; 0 errors

dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll
  PASS: exit 23; stdout 0 bytes; stderr exact UTF-8 `W24FS001\r\n` (10 bytes)
```

For each of those five literal commands, the validation root contains
`<prefix>.command.txt`, `<prefix>.started-at-utc.txt`,
`<prefix>.ended-at-utc.txt`, `<prefix>.stdout.txt`,
`<prefix>.stderr.txt`, and `<prefix>.exit.txt`. The first four exits are
the exact bytes `0\n`; the smoke exit is the exact bytes `23\n`. All five
exit files are LF-only and are checked by
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/exit-receipt-encoding-check.json`
with SHA-256 `6a976c0b49514a619a1d777fdb9d9e2ae91655c785d889baa1f3464f0a37f8d9`.

The exact literal commands, UTC starts/ends, expected/observed exits, and
stdout/stderr/exit paths, byte counts, and SHA-256 values are in
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/validation-summary.json`
with SHA-256 `d29108ac0e843bb144b12435dbca0f617c1def47fd1a41c358539518291daa4a`.
The regenerated TRX is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/test-results/protocol-worker-project-locator.trx`
with SHA-256 `f3d89df88aca8491df8a6ded77dd45a4e4d8e13c0938292216c8548f420c8ca1`.

Final ordinal aggregates (same encoding as the manifest) are:

```text
70  0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4  src/VFXComposer.Protocol
25  d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622  src/VFXComposer.Protocol.Tests
36  0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538  docs/schemas/desktop
```

Frozen C1 and non-C2 roots replay exactly:

```text
40   f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692  services/VFXComposer.Broker
18   16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297  services/VFXComposer.Broker.Tests
8    0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103  services/VFXComposer.Broker.ServiceHost
6    69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c  services/VFXComposer.Broker.ServiceHost.Tests
1    b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689  VFXComposer.sln
769  506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f  project/Packages/com.vfxcomposer.unity
```

The aggregate receipt is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/final-ordinal-aggregates.json`
with SHA-256 `2e69695c720cb481f0112627c195716a4e144b8739adf595fea6738ac9d0b1bc`.
The product forbidden-surface and schema-shape receipt is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/forbidden-surface-scan.json`
with SHA-256 `a0ccc6968b0cd07917730256c10f372bd177934630accdb733c6d8598d182a70`.

The self-excluded receipt manifest is
`.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/receipt-manifest.sha256`.
It lists all 39 validation payload paths and SHA-256 values, excludes only
itself, is ordinal/LF/no-BOM, and independently replays with zero missing,
extra, or hash-mismatched payloads. Its SHA-256 is
`7e6b9d2c3a963f5d5dcc1d083418e3a812152cb26e74468ff1b94ca617341cd4`.
It intentionally excludes this handoff too; neither receipt nor handoff has
a self-binding cycle.

The final residue inventory found exactly the 16 product/test/schema/verifier
paths plus this handoff; all other newly generated files are inside the allowed
validation root or `bin/obj`. No unexpected source residue was found.

## Final independent audit, limits, and stop

| Severity | Result |
| --- | --- |
| P0 | `0` final independent-audit findings. No runtime, I/O, transport, locator issuer or ACK issuer, handle, lease/grant, command, or authority surface was added. |
| P1 | `0` final independent-audit findings. Actual C1 manifests/receipts, all nine governed pre-hashes, virtual C1 reconstruction, C2 roots, schema shape, and frozen roots replay exactly. |
| P2 | `0` final independent-audit findings. Build/test/schema/solution/smoke each have literal command, UTC bounds, stdout, stderr, real LF exit, and a 39-payload self-excluded receipt manifest. |

The final independent audit is closed. It used external pre-status SHA-256
`0a95a335e982527d5f1b5f9193547c40b0093c5832c01b6f4b88d8ee2b12698c` as an
audit input only. Remaining blockers are unchanged: production remains
`W24FS001`/23; Phase 2, runtime, transport, locator issuer/ACK issuer, project
I/O, handle/grant, command, and authority remain NO-GO; D1 remains STOPPED/NO-GO.
Active package is `NONE / NEXT NOT PUBLISHED`; W1 and D1R are not started or
published. C2 grants no permission to begin W1 or any later package.

This handoff intentionally has **no self-hash**: including its own digest in its
bytes would invalidate the value. The external pre-status SHA-256 recorded above
is an audit input only; this final status-updated form is intentionally not
self-bound.

**FINAL STOPPED — C2 is `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`; no continuation into W1, D1R, or any other package occurred.**
