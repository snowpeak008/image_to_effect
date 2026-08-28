# WP-P2-PROTOCOL-PROJECT-SELECTION — STOPPED

## Package

- Objective: add only the `PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`
  no-path registered-project selection wire contract. Decoding is correlation data
  only; it neither issues a lease nor creates I/O, trust, or authority.
- Writer model / reasoning: `gpt-5.6-terra / max`.
- First source-write proxy: `2026-08-27T19:26:18.2630375Z`.
- Validation close: `2026-08-27T19:32:42.6377912Z`.
- Final state: `CLOSED / SCOPED GO — PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`.
- Final independent audit: `P0=0 / P1=0 / P2=0`.

## Pre-edit freeze

The complete current Program Control, Registry §12 C1 contract, Evidence Index,
ADR-004 C1 row, Phase 1 report, and both P3 handoffs were read before editing.
The following control inputs matched exactly:

| Input | SHA-256 |
|---|---|
| `docs/coordination/W24_PROGRAM_CONTROL.md` | `8c27971bff53a42e7675f3efa6e4d0cf1ef963859ed736351648bf6bf052172b` |
| `docs/coordination/W24_WORK_PACKAGE_REGISTRY.md` | `f139313da8e3244f09fba86169705a621a6be277e1fceb637e752ebc0dcea179` |
| `docs/coordination/W24_EVIDENCE_INDEX.md` | `7ec5782519d860a505f7e9c29d767a334067972f6758a3ff822584997cfd5e2d` |
| `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md` | `7849105a1d8038592b744816662fdf60b18d5760e462d3b71fabde51882afd75` |

Before the first edit, the ordinal `<sha256><two spaces><repo-relative
forward-path><LF>` replay (excluding `bin/obj`) matched Protocol
`67/0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`,
Protocol.Tests `21/aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`,
and desktop schemas `33/f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`.

The frozen non-C1 replay also matched Broker
`40/f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692`,
Broker.Tests `18/16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297`,
ServiceHost `8/0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`,
ServiceHost.Tests `6/69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`,
solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`,
and Unity package `769/506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f`.

`RegisteredProjectSelection.cs`, `RegisteredProjectSelectionTests.cs`, and
`vfxcomposer-registered-project-selection-v1.schema.json` were absent with zero
case-insensitive sibling collisions before the first edit. The durable preflight
record is
`.codex_tmp/WP-P2-PROTOCOL-PROJECT-SELECTION/validation/baseline-replay.json`
(`e7af4ed8dd64cdc6f560be189ad68cb5abaafadc7a17ad7ef2d3a917468406f4`).

## Exact source manifest

Exactly the Registry §12 twelve author-controlled product/test/schema/verifier
files changed; no project, lock, solution, Broker, Client, Desktop, Unity, or
other documentation file changed.

```text
224060f9a3a42e634d68b33ebe31f9230131ac950528b6017e5f4a524c9c27ea  src/VFXComposer.Protocol/Projects/RegisteredProjectSelection.cs
727bb2c30dc7ba13fbb1425277942a04b1d67fc3d0053b6a80a2f48cf11190f7  src/VFXComposer.Protocol/Json/StrictWireCodec.cs
ef9d472566288ba926992a7b893ca3d4e837286abebb8a2bf905886588d2edde  src/VFXComposer.Protocol/MessageKinds.cs
9ca4a574e9631045240bd9cd07869fa9c9c3bb8910b8d6927f51977894b77934  src/VFXComposer.Protocol/WireSchemaRegistry.cs
1240071df65e980a2db27d66d7acebaa7958626e7a27dd9438c96ca392e74c70  src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs
a37221229440226e4f486a0d62fed67bb33c204347f034e2bac2b6b3ae2e68b4  src/VFXComposer.Protocol.Tests/RegisteredProjectSelectionTests.cs
5e7c8f80064c931151aea189cf6fe4224e5fe4d23c3308b1018f4d2ea48105b9  src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs
2acdb05f0cecc51476fed9997542c9b698fbd3a4eb4421f47d310ef939c0aa3d  src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs
0a8b0781e25607aec6976338673d13971970478b139b48ec6b0199ba1e6ead74  src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs
6ce16811d756c279e916c14773ad398aa0078be9791c67b6ff7d33596705641b  src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs
5f58482d48480fa55eba031e87177be283f9f3ccdd66b24af391d6057a4ee320  eng/verify-phase2-schemas.py
40e94a1e9f29917e3186c65f12a29feddeff7bcd9cfbe7f7d2dfca55a209074c  docs/schemas/desktop/vfxcomposer-registered-project-selection-v1.schema.json
```

The durable source-manifest path is
`.codex_tmp/WP-P2-PROTOCOL-PROJECT-SELECTION/validation/changed-source-manifest.sha256`
with SHA-256 `27854e8504ec8e7da12e510db906efd9f2f780577d7a1b49e55c039d7da23600`.
This handoff is deliberately excluded from that manifest and has no self-hash.

## Implemented contract and limits

- `RegisteredProjectSelection` is immutable and has exactly these seven wire
  properties: `protocolVersion`, `messageKind`, `requestId`,
  `registeredProjectId`, `projectIdentity`, `brokerGeneration`, and
  `registrationGeneration`.
- It fixes version `vfxcomposer.protocol/1.0`, kind
  `project.registered.selection`, capability vocabulary `project.selection.v1`,
  typed project-identity domain `vfxcomposer.project-identity/1`, bounded opaque
  tokens, and positive signed 64-bit generations.
- The sole strict ingress rejects root/nested missing, extra, decoded-duplicate,
  wrong-type, wrong-version, wrong-kind, wrong-domain, zero/negative/overflow,
  BOM, raw path-shaped token/value, and authority-shaped-field attempts with
  stable non-echoing diagnostics.
- The Draft 2020-12 schema has the same seven property and required sets with
  `additionalProperties:false`; it contains no self-hash or authority-bearing
  field. Golden canonical bytes, codec negatives, reflection surface checks, and
  independent schema negatives are covered by the C1 tests and verifier.

Not proved or enabled: a transport sender/receiver, an authenticated peer,
project registration/admission, lease issuance or enforcement, project/content
I/O, a Broker/Client/Desktop/Unity runtime route, command execution, production
listener, policy activation, trust decision, or any authority. The new
capability token is vocabulary only; this package supplies no runtime consumer
or issuer for it.

## Validation

| Command / check | Result | Durable receipt |
|---|---|---|
| `dotnet build src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true` | exit `0`; `0` warnings / `0` errors | `protocol-tests-build.log` — `396ede37c05d56f47814d14f455cafd5a76fadd22ea4997398c7e41cfcd44e66` |
| `dotnet test src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=protocol-project-selection.trx" --results-directory .codex_tmp/WP-P2-PROTOCOL-PROJECT-SELECTION/validation/test-results` | exit `0`; `95/95` passed, `0` failed, `0` skipped | `protocol-tests.log` — `c891fbc5af8336d426d8570863f157d4a70c458796af6dfbb6516116f948bcfd`; TRX — `3c9554548ed59d41031372947545f12f1b77231509d9b13a77cb98a037660dec` |
| `python eng/verify-phase2-schemas.py` | exit `0`; exact `20` total schemas, `11` Phase 2 schemas, `12` positives, `170` negatives | `phase2-schema-verifier.log` — `204bee8fa7daa9ed1e4f336d04a1ac80244dc9b70e12db50c010909c298bdd06` |
| `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` | exit `0`; `0` warnings / `0` errors | `solution-build.log` — `9948966e9ecf54b5349076ab285b13832469f8ef3a52e96ed5c294de06eef428` |
| Process-captured `dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll` smoke | exit `23`; stdout empty; stderr exact `W24FS001\r\n` | `broker-smoke.json` — `aa3b652ca102aa5de2a1a7adfbb16b0953973801010c9d392f6997a96c88cd52` |
| Product forbidden API, exact DTO property, and schema shape scan | PASS; no I/O/network/process/native forbidden API match; seven DTO/schema properties exact; nested/root extras closed | `forbidden-surface-scan.json` — `0e2187d2f9e82f7cb1ca5d79f96eda444b3ef6c6912a262ddebae36eec47c801` |
| Final C1 aggregate and frozen Broker/Broker.Tests/ServiceHost/ServiceHost.Tests/solution/Unity replay | PASS; C1 Protocol `68/bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605`, Tests `22/672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03`, schemas `34/d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc`; all frozen roots exact | `final-ordinal-aggregates.json` — `67a52419816709ccb07a8ffbce69a6c86edef126f1be08206432be78ee888b50` |

The receipt manifest lists its ten payload receipts but not itself and hashes to
`7402e367014a05b1d45d28df497de8f24563ce68d4cebed523745141583e1eef`:
`.codex_tmp/WP-P2-PROTOCOL-PROJECT-SELECTION/validation/receipt-manifest.sha256`.

The final independent audit used the external pre-status SHA-256
`8c35cae3759cd17f89d366184d74d843f8cc5904ca0a894aa4aae7da074759ef` for
this handoff. That external value is an audit input, not a self-hash of this
status-updated handoff; this handoff deliberately has no self-hash.

## Self-audit, blockers, and stop

| Severity | Result |
|---|---|
| P0 | `0` open findings. No production, path, project-I/O, or authority surface was introduced. |
| P1 | `0` open findings. Strict codec/schema/domain/range and frozen-byte replays passed. |
| P2 | `0` open findings. Receipt and exact-manifest accounting is complete; this handoff intentionally has no self-hash. |

Remaining blockers are unchanged: D1 remains frozen `NO-GO`, production remains
`W24FS001`/23, Phase 2 remains `NO-GO`, and all runtime/production/authority
nodes require their separately published dependency gates and a fresh writer and
auditor. C1 is not permission to start any of them.

**STOPPED — C1 reached only `PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`; no continuation into another package.**
