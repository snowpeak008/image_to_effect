# WP-BROKER-P2-ISSUER-HOST handoff

Status: **STOPPED** — scoped GO candidate for a dormant Broker issuer/service-bootstrap source-and-test foundation only. Production issuer activation, installation, SCM registration, listener creation, registration, project I/O, handle admission, Worker command path, Desktop integration, Unity UI, and authority capability remain disabled.

Model: `gpt-5.6-terra`; reasoning: `max`.

Initial implementation window: 2026-08-27T03:32:54Z through 2026-08-27T03:42:44Z. Independent-audit P1 closure and final evidence refresh completed 2026-08-27T03:50Z through 2026-08-27T03:55Z.

## Exact scope and authored files

Only these source/test files were added or changed:

| Path | SHA-256 |
|---|---|
| services/VFXComposer.Broker/Configuration/HostBootstrapMaterial.cs | 6afd644df61d95da2142eecbb4d1082127442fafff1bc55c6b5b4adaa39a5dc4 |
| services/VFXComposer.Broker/Security/WindowsNamedPipeAclProvisioningIntent.cs | 51a19b3a601b1134761537bf4d102d431ef04b56abd884a3180c9f79e3f777fc |
| services/VFXComposer.Broker.Tests/HostBootstrapMaterialTests.cs | 4132f8a766facd28a6ce820ab1ad8bc2c9e638b16358bdef118faef126155c9c |

The only non-source authored file is this handoff. Generated validation outputs are confined to .codex_tmp/WP-BROKER-P2-ISSUER-HOST/ and normal bin/obj directories. No Protocol, schema, Client/Desktop, Unity, solution/root build, lock/configuration, or coordination-registry file was modified.

The source manifest is forward-slash, lowercase SHA-256, two-space separated, LF-terminated, and StringComparer.Ordinal sorted. It excludes this handoff to avoid self-reference:

- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/changed-source-manifest.sha256
- SHA-256: c480c3b3f445f3b113bf8556c408503eb415f70c226cb4c34247c34efec3117b

## Dormant foundation and independent-audit P1 closure

The foundation is internal, non-wire capability state. It does not introduce a public bootstrap DTO, a production trust root, a token, an issuer, or any production activation path.

- WindowsServiceProcessIdentity shape-validates a supplied Windows service SID, image typed hash, PID, canonical process epoch, generation, and service session. It does not observe a process or claim OS attestation.
- HostBootstrapIssuerProvenance records supplied issuer identity. HostIssuedBootstrapMaterial bounds canonical material IDs and injected-time lifetime to 300 seconds, rejects a same-SID issuer, and binds a target service and canonical named-pipe ACL intent.
- HostBootstrapMaterialValidator is an internal in-memory validator/lease boundary that enforces one-use material IDs within one validator instance, detached from Program.Main and BrokerPolicy.TryLoadProduction.
- WindowsNamedPipeAclProvisioningIntent snapshots the existing canonical SDDL and can create a fresh Windows PipeSecurity descriptor. It never creates, listens on, or applies ACLs to a pipe.

The audit P1 was that static SID/generation/ACL comparison did not bind the approved Desktop/Worker image set held by ProductionTrustProfile. The minimal dormant correction stores and checks the exact ProductionTrustProfile object identity in both HostIssuedBootstrapMaterial and WindowsNamedPipeAclProvisioningIntent. Matching requires ReferenceEquals for that profile before issuer/service/ACL correlation succeeds.

HostBootstrapMaterialTests.MaterialRequiresTheExactFrozenProfileInstanceIncludingPeerImagePolicy deterministically proves all three outcomes:

1. Same static values in a distinct profile instance are rejected.
2. A profile with changed Desktop and Worker approved image identities is rejected.
3. The exact original profile instance is accepted.

This is deliberately an in-memory identity binding only. No durable profile hash, serializable profile identity, or durable attestation claim has been invented; that remains a production activation blocker.

## Baseline replay and frozen dependencies

Before the first edit, documented current roots replayed exactly. Final replay excludes exactly the two Broker additions and one Broker.Tests addition above; all frozen dependencies still match:

| Root | Count | SHA-256 |
|---|---:|---|
| services/VFXComposer.Broker, baseline excluding this package additions | 28 | 522a0f21738fa902df35f920fb9478943f3be722133e47bd6451cc291bde7284 |
| services/VFXComposer.Broker.Tests, baseline excluding this package addition | 12 | f70050154f26b5594a301b1f2cd0da6fbc8d6ff2e37580c42c63035c117124f1 |
| services/VFXComposer.Broker.HandleProbe | 4 | 5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8 |
| src/VFXComposer.Protocol | 67 | 0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56 |
| src/VFXComposer.Protocol.Tests | 21 | aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5 |
| docs/schemas/desktop | 33 | f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9 |

The frozen Unity UI hashes also replay exactly:

- project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioWindow.cs: 0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587
- project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioModels.cs: cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68
- project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6EditorIntegrationTests.cs: d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6
- project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6StudioModelsTests.cs: 28188ffcbd31471876c137be3023aa668f93327a506dd4862c06d355e372d33d

Replay receipt: .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/baseline-replay.txt / 98b8e808059a685155f2df3c1931caefd2ff7c576af4bac0443266ca1262f5ac.

Final source aggregates use the same ordinal, LF-normalized algorithm:

| Root | Count | SHA-256 |
|---|---:|---|
| services/VFXComposer.Broker | 30 | 10b285e11f0229d0d4ba6286559d0ab0d6da217a9f73fc2412ac9d82cc52d95e |
| services/VFXComposer.Broker.Tests | 13 | 212a4aa7c471a5e80d339178fca493a69b3b75f6613579eb2e82020fca4f74ce |
| services/VFXComposer.Broker.HandleProbe | 4 | 5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8 |
| src/VFXComposer.Protocol | 67 | 0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56 |
| src/VFXComposer.Protocol.Tests | 21 | aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5 |
| docs/schemas/desktop | 33 | f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9 |

Aggregate receipt: .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/final-source-aggregates.txt / 43a2079ba5636b8496ea63294294a8fa49b2fcc38e4aeeee68d0b7c720faf590.

## Validation, commands, and durable evidence

All build/test commands used the checkout's locked/no-restore form:

1. dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true — exit 0; 0 warnings, 0 errors.
2. dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger trx;LogFileName=broker-issuer-host-final.trx --results-directory .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/test-results — exit 0; 48/48 passed, 0 failed, 0 skipped/not executed.
3. dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true — exit 0; 0 warnings, 0 errors.
4. dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll — exit 23; stdout is 0 bytes and stderr is exactly W24FS001.

Separate stdout, stderr, and exit receipts are retained for all four commands under .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/. The exit receipts are real UTF-8 LF-terminated values, not literal backtick-n text:

- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/exit-receipt-encoding-check.txt / 97807e729b29b6737474fc78421ac966a590ede910a926cbc426956d7b78744e
- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/broker-tests-trx-summary.txt / 9a2fcb6d6c83cbc47f1ae317c96c35228dd68d8ba4312e16a14ad35c7b0aa6b3
- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/test-results/broker-issuer-host-final.trx / bdf0c6e9e39c1350121815f93eed12c8cde476b015090b2adae55f665f4068b8
- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/broker-smoke-summary.txt / d3b5b9b9de761901b5bb28e2511fc9349b4f2e2dfc8de0c5a9e63d1e16122129

The product-source forbidden-surface scan found no network/listener/project read-write/environment/caller-path/authority activation surface. The unchanged production closure source check confirms TryLoadProduction at index 152, W24FS001 at 237, and return 23 at 299; Program.cs and BrokerPolicy.cs retain their frozen hashes.

- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/new-foundation-forbidden-surface-scan.txt / 91758677b958677097eec97ae02575302ae89d6ccc5492222d45dea644a3895d
- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/production-closure-source-check.txt / 1c70408d07a81ecd5c6b605f321a7e783db3e52c75d3aebc46ad5a94f8fcc0fe
- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/profile-identity-binding-check.txt / a77cef2fddd209d522a2e9a6697a5ecab2ab35c0113863a50b0c28f50fc9d85f

The final evidence manifest has 26 entries, is StringComparer.Ordinal sorted with repository-relative forward-slash paths, and excludes itself and this handoff:

- .codex_tmp/WP-BROKER-P2-ISSUER-HOST/validation/evidence-artifact-hashes.sha256
- SHA-256: 0bcc7a045b03f3f275bd80d6a3e65271a52d5371d78e0993498a5e4cd69812eb

## Security invariants and carefully limited claims

- Within one validator instance, validation requires current injected time, a one-use material ID, exact profile object identity, exact issuer/service SID-image-PID-epoch-generation-session correlation, and exact canonical ACL intent before a lease is returned.
- SID inequality is only a supplied-identity consistency check. It does **not** prove an independently privileged host, distinct PID, distinct process epoch, live service identity, or OS attestation.
- Within one validator instance, replay stays rejected after release or revoke; stale/cross-session/cross-generation/wrong-SID/wrong-image/wrong-ACL/wrong-profile material fails before a lease is returned. Dispose invalidates that instance's active leases.
- The only ACL operation builds a fresh PipeSecurity descriptor from validated canonical SDDL. No pipe, listener, service, registry, Worker, project, or authority operation occurs.
- Program.Main and BrokerPolicy.TryLoadProduction remain unchanged and production remains W24FS001/exit 23 before listener, request, path, project, or environment I/O.

## Remaining production blockers

- There is no independent installed host/service, SCM registration, installer, service SID/image/PID/epoch/session attestation, trusted monotonic clock, real named-pipe ACL application, or live OS validation.
- Reference identity is process-local and deliberately non-durable. A future authorized production package needs a durable, authenticated profile identity plus independent host attestation; no such profile hash is claimed here.
- The validator's consumed-ID and active-lease registries are deliberately per-validator-instance and process-local, with no single-owner, process-global, cross-validator, cross-restart, persistent, or bounded-eviction guarantee. They are test-foundation state, not an activation-ready production replay design.
- No production policy loading, issuer, named-pipe listener, peer/session admission, handle duplication/admission, Client/Desktop connector, Worker session, project content access, command execution, mutation, authority, L3, or L4 is enabled.

## Self-audit

- P0: 0 in this dormant, source-only slice. Production remains explicitly false and smoke-confirmed.
- P1: 0 after closing the profile peer-image binding finding with exact in-memory profile identity and a deterministic negative test.
- P2: 0. Handoff claims are limited to internal/non-wire state; the validator registry and absence of durable profile identity are recorded as explicit production activation blockers; exit receipts were revalidated as real LF.

This handoff intentionally does not hash itself. **STOPPED.** Do not continue this work package into issuer activation, installation, listener construction, peer admission, project I/O, Worker/Desktop integration, or any other Phase 2 blocker.
