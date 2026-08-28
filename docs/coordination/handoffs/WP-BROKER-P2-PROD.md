# WP-BROKER-P2-PROD handoff

Status: **STOPPED** — the bounded Broker-only trust/ACL foundation is complete; no production issuer, listener, registration activation, handle admission, or project-content path was activated.

Model: `gpt-5.6-terra`; reasoning: `max`.

Time window: `2026-08-27T01:26:08.3989839Z` (first retained source write) through `2026-08-27T01:48:52.8569583Z` (evidence freeze).

## Baseline and connection reconciliation

The originally assigned, pre-edit aggregate replay matched all six frozen inputs:

- `services/VFXComposer.Broker`: `25` / `083a5998dfebe604278c781d6b88394d81707aa9f19d499f72c606822ccea14a`
- `services/VFXComposer.Broker.Tests`: `11` / `def1118b96a72dcd13c379c37f5bc3fcb956d0fa46842308f96c516cf7a7a47f`
- `services/VFXComposer.Broker.HandleProbe`: `4` / `5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8`
- `src/VFXComposer.Protocol`: `67` / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`
- `src/VFXComposer.Protocol.Tests`: `21` / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`
- `docs/schemas/desktop`: `33` / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`

A remote connection failure interrupted the first attempt after a partial, allow-listed patch. A fresh replay therefore showed Broker `28` / `baffd16f253985e53e9074b65ba72c5fa11de483b86b48c4dcb4ccc92b891d24` and Broker.Tests `12` / `f60f243e49742413ad6197313038e315a88feafb3fe2bffa8a071d02ed13b63a`; the four frozen dependency aggregates still matched. The controller reconciled those exact extra files as this package's own partial patch, authorized them as the intermediate baseline, and the full six-root intermediate replay then matched before resuming. No out-of-scope authored drift was accepted.

Final aggregate algorithm: repository-relative forward-slash paths, ordinal sort, per-file lowercase SHA-256 followed by two spaces and the path plus LF, UTF-8 without BOM, excluding `bin/` and `obj/`. Receipt: `.codex_tmp/WP-BROKER-P2-PROD/final-source-aggregates.txt` (`751c10f6f9821313b8ebbadeba5499ab834e51d6e610a2b5fe744fe70319925b`).

- `services/VFXComposer.Broker`: `28` / `522a0f21738fa902df35f920fb9478943f3be722133e47bd6451cc291bde7284`
- `services/VFXComposer.Broker.Tests`: `12` / `f70050154f26b5594a301b1f2cd0da6fbc8d6ff2e37580c42c63035c117124f1`
- Frozen HandleProbe, Protocol, Protocol.Tests, and desktop schemas replayed at their assigned hashes above.

## Selected bounded design

The new dormant model has no issuer or listener path:

- `WindowsSid` accepts only canonical binary/text Windows SID material and distinguishes an exact service SID (`S-1-5-80-...`) from an exact account/user SID (`S-1-5-21-...` or `S-1-12-1-...`). OS-observed peer facts now retain the canonical SID in addition to the existing typed identity hash.
- `ProductionTrustProfile` is immutable: it freezes exact role-to-approved-image identities, service and user SIDs, pipe/broker tokens, and broker generation; it validates PID/generation-bound canonical process-epoch and session shapes rather than storing or proving a particular live epoch/session. A missing/wrong role, SID, image, process ID/epoch, session, or generation fails admission matching.
- `CanonicalNamedPipeAcl` creates and validates only one protected canonical Windows DACL: service owner/group, exactly one non-inherited user read/write ACE and one non-inherited service full-control ACE. It rejects textual non-canonical forms, wrong owner/group, unprotected/missing DACL, SACL, deny/broad/unknown/inherited/opaque ACEs, masks, ordering, and SID-role mismatches. `CurrentUserOnly` is explicitly not described as a production ACL.
- `BrokerPolicy.TryLoadProduction` remains explicit `policy = null; return false;`. `Program.Main` therefore writes `W24FS001` and returns `23` before a pipe, request, caller path, registration, or project-content operation.

Production deliberately remains pending: this checkout has no independently privileged installed Windows service, installer/bootstrap issuer, or separate principal that can supply and attest the actual service/user/image/generation material and apply/verify the ACL on a real production pipe. An in-process factory or ordinary static token would violate the trust boundary, so none was added.

## Exact authored source manifest

The manifest excludes this handoff by design. Receipt: `.codex_tmp/WP-BROKER-P2-PROD/changed-source-manifest.sha256` (`3a2af548e159098108a280f554cb2c8f6824886f4c33bd0b65f48d8ea2f2e9b1`).

```text
7045d08011fe4834915c2d962c5954663a2507d7a75c97c904e4be2f781edf20  services/VFXComposer.Broker.Tests/ProductionTrustProfileTests.cs
38719aaf670134b36d9bf53e5901a52b11c44bac5f769a8930f8ed3a39c5f321  services/VFXComposer.Broker/Configuration/BrokerPolicy.cs
96c83dff067b3e2078801e1caea13c0bef0beae19a6b88f21e7fdc146c230ac4  services/VFXComposer.Broker/Configuration/ProductionTrustProfile.cs
5069c2884ecbe8bc6a9b78be795414cde1eb4eaa917f9520744a7747b142d524  services/VFXComposer.Broker/Ipc/ObservedPeerFacts.cs
bd7b07a19a153ac0ad32646d74d09599e91006634c815ee53bd254b53a9aed29  services/VFXComposer.Broker/Ipc/WindowsNamedPipePeerFactsSource.cs
ccaccb2be4d15f45f3ba248ee291bad598c9acf3a58625a4bb4015ffd40937d7  services/VFXComposer.Broker/Security/CanonicalNamedPipeAcl.cs
5ada2c1f77fb5d801b1b8aed7d3233c3edd266dfec6052f960dc3d3a7e33bf5b  services/VFXComposer.Broker/Security/ProcessEpoch.cs
438ff2fe3f1d74fc33ebe910d0b8525a312f9ec71af62186e60f67f02bbf7f09  services/VFXComposer.Broker/Security/WindowsSid.cs
```

## Validation and receipts

1. `dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, 0 warnings, 0 errors. Stdout `5c376919d14472674e37f02b90c2fe7406f0838aa9ac32154e737c07536f28ff`; result record `1e7ea59760868e5bd8d415f755ad5853f1115fef707ac8c75a30c3d177fe01f7`; stderr was empty.
2. `dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger 'trx;LogFileName=broker-p2-prod.trx' --results-directory .codex_tmp/WP-BROKER-P2-PROD/test-results` — exit `0`; 40 passed, 0 failed, 0 skipped. TRX `cfac707f97990f5ae14eb82d5e2bce11afdc95ad78e2a0c7a8282b29308ac666`; stdout `8abdbac428252aa9db54a087d904b37cc77944ff84e035f7b95642cf4673ff8b`; result record `9d66f12e89d493551241f45140fd0c65a83684a6dca6dd4b45189d7a42e53f52`.
3. `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, 0 warnings, 0 errors. Stdout `f0eebcebff37d3449d0b870f153fa7099c081a220d4a83da91a671d20335e6ca`; result record `65e1ca04842d7d6ef259dd6f4493b58c6408de809b6a2bde69b3f6ed129e99f7`; stderr was empty.
4. Controlled smoke: `dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll` — exit `23`, stdout 0 bytes, stderr exactly `W24FS001`. The paired source call-order check found no listener/network/project/path/environment surface in `Program.Main` or `TryLoadProduction`, confirmed the explicit false policy body, and confirmed the first gate returns `23`. Smoke result `fea6782d76a222f18f77e0c0e6b3d87b292d2dd1c5bd2e14d4cdc207b4231cd0`; stderr `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06`; stdout empty SHA-256; static companion `55fbd89bfa4518a2b83ba1b9c9acaa2579f26379c670aca1247e97c014886d67`.
5. Public/private and forbidden-surface scan: no HTTP/TCP/socket/stdio MCP listener, no production test hook/bypass, no public top-level foundation type, no product instantiation of `ProductionTrustProfile`, no product construction of `NamedPipeBrokerHost`, and the fail-closed call-order assertions passed. Existing `CurrentUserOnly` appears only in the pre-existing scaffold and remains unreachable from `Program.Main`. Receipt `7a0bde64ab817bed53a103bc212095d052e3ef72031f76842393b5b2bb342121`.
6. Independent ACL verification: the passing test `CanonicalPipeAclHasOnlyTheExactProtectedServiceAndUserEntries` parses the produced SDDL using `System.Security.AccessControl.RawSecurityDescriptor`, rather than using the implementation validator as its assertion oracle. It verifies service owner/group, protected DACL, no SACL, exactly two ACEs, exact SID order, allow-only ACE types, no ACE flags, and exact masks. The five new trust/ACL tests are recorded in `.codex_tmp/WP-BROKER-P2-PROD/broker-tests-trx-summary.txt` (`b01f6c20d9167835f0082d5b03f9059e2a0c46e4a5ff2155f797f2fcaf66bce6`).

Built artifact hashes:

- Broker DLL: `947610051db29dd4bf6cf3c5966970ca8d439065a8a1ad1db99ff9419c0005a6`
- Broker.Tests DLL: `f9cc99b728ded8b4505299d377b7fda5c04780794d248f065f6d8f29b47dd665`
- Frozen HandleProbe DLL: `f8f6e5a8c1e99d413257c91eceee6cb79fc324c8dada396dbbc11dfc4874f00e`

All listed receipt/DLL/TRX hashes are collected in `.codex_tmp/WP-BROKER-P2-PROD/evidence-artifact-hashes.sha256`, whose SHA-256 is `91640c4246dea170791127af630c5a3183c87c64419633ccc0e3926e712ebb58`. The handoff itself is intentionally not self-hashed; the controller must hash it externally.

## Proved boundaries

- Canonical immutable SID, image, generation, process-epoch, and session matching is represented and exercised by positive and negative synthetic tests.
- The dormant profile's canonical SDDL has independent parser verification and rejects the specified broad/inherited/deny/malformed/noncanonical negatives.
- Production entry remains fail-closed before any listener/request/path/project operation, confirmed by source-order check and actual DLL smoke.
- Existing Broker regressions and HandleProbe compilation did not regress in the required build/test scope.

## Not proved / blockers

- No real production service installation, independent bootstrap issuer, SID/image attestation source, or real production named-pipe ACL application was implemented or proved. This is the remaining Phase 2 blocker 1/8 boundary.
- No production registration, caller admission, handle duplication/admission, project-content read/write, or Worker activation was enabled.
- Matching is a dormant policy shape, not authority: it must be fed only by a future independently privileged issuer and live OS observations before it can participate in production admission.

## Self-audit

- P0: none found within this bounded slice. The production load path is still explicit false; no listener is reachable.
- P1: none found within this bounded slice. New foundation types are internal; no test-only production bypass/factory or public network surface was added.
- P2: none found within this bounded slice.

STOPPED. Do not extend this package into production activation without a separately authorized service/installer/issuer work package.
