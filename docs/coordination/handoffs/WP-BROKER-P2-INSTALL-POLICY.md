# WP-BROKER-P2-INSTALL-POLICY handoff

Status: **STOPPED — INDEPENDENT FINAL AUDIT ACCEPTED (`P0=0 / P1=0 / P2=0`) — DORMANT_SCM_INSTALLATION_POLICY_FOUNDATION_ONLY.**

Model: gpt-5.6-terra; reasoning: max.

This same-package remediation closes the second independent-audit P1. The previous correction guarded the validator only, leaving the internal HasExactIdentityBinding → WindowsServiceInstallationIdentity.Matches route capable of correlating two same-digest identities from the same wrong typed-hash domain. The correction moves the domain boundary to the immutable identity itself and retains validator defense in depth. It remains internal, non-wire, pure in-memory Boolean/argument validation; it does not install, register, configure, start, or delete a service, and remains unwired from Program and BrokerPolicy.

Original retained allowed-source creation: 2026-08-27T09:28:18Z.
Prior P1 evidence freeze used as this remediation's starting snapshot: 2026-08-27T10:07:27.1939396Z.
P2 core-boundary validation/evidence freeze: 2026-08-27T10:43:20.9902440Z.

## Starting replay and frozen boundary

Before this P2 source edit, the then-published bytes replayed exactly:

- authored source manifest: 3/3 / 7f178ca5edea6d4ccef85c29af3d8bc31d35a25cc3ded3679456a8e45435cb1b;
- evidence manifest: 22/22 / bf9bbb7aab6df9e2bbd950631cdb86800234508a3e6004d363f560c5d69e7003;
- Broker aggregate: 34 / 24c6a422b88e28e98129cd6723edb0968339e8b2f781e2fc35c5a48e8a68d761;
- Broker.Tests aggregate: 15 / debd1f1168f0bc34eb264bec7c0a0595f44b69806572f6e438c3b87721e9cb1d.

The final P2 replay excludes exactly the three allowed source/test files and reproduces every frozen dependency root, solution, and registered Unity byte:

- .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/manifests/baseline-and-frozen-replay.txt / f528b22149d6837cbbeec983991dbe5d366a353a4dd553d213581802a9d843e1;
- overallPassed=True.

This includes Broker 32/e5b7fad903c584422c8c5afa2e810a9fdfeebf34b3d765a527aea935a5f9f3ac, Broker.Tests 14/f345f0ba8b8b32619a8bf6fde1cd9702a3bd390f89b488e6c49a9066b23456e2, HandleProbe 4/5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8, Protocol 67/0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56, Protocol.Tests 21/aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5, desktop schemas 33/f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9, ServiceHost 8/0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103, ServiceHost.Tests 6/69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c, solution VFXComposer.sln/b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689, and all four frozen Unity files.

## Scope and core P1 closure

The source manifest covers the full three-file package allow-list; only the policy and its tests changed in this P2 correction, while the validator remains byte-identical to the prior P1 remediation:

    07c164d90a716d59a57e1f5bd04338d69fcfbdcd08504a54f9162d62c618a038  services/VFXComposer.Broker.Tests/WindowsServiceInstallationPolicyTests.cs
    1b843e8e92230fcbe5d4ed79334ee9e9d23dc19ac3c20bd7176d307125462415  services/VFXComposer.Broker/Configuration/WindowsServiceInstallationPolicy.cs
    62a46fce01b2f2db8e81944f65d4216edac5d6d241d45260c5f46799a041a6aa  services/VFXComposer.Broker/Security/WindowsServiceInstallationPolicyValidator.cs

The source manifest is repository-relative, forward-slash, lowercase SHA-256, two-space separated, ordinally path-sorted, LF-only, and UTF-8 without BOM:

- .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/manifests/changed-source-manifest.sha256 / 45eb51eac83ef1f8144be1a9f6e443048415d15d92fcf686e4547345b761530b;
- conformance receipt: manifests/changed-source-manifest-conformance.txt / 9684a42c72795966fa4d3cd6ae13ff916c45e46ed6944cd2b696f26f769bb0f4.

WindowsServiceInstallationIdentity now rejects any serviceImageIdentity.TypeTag other than PeerHello.ProcessImageIdentityType using StringComparison.Ordinal, with a stable ArgumentException whose parameter is serviceImageIdentity. This constructor boundary applies equally to candidate and expected identities. Matches independently requires both stored TypeTags to equal that same constant before the existing reference, generation, service-SID, and fixed-time typed-hash checks. The validator's existing two TypeTag checks are retained.

The tests directly exercise a normal Matches and HasExactIdentityBinding success. For the required same-wrong-TypeTag, same-digest negative, they construct two equal wrong-domain TypedHash values, prove their fixed-time equality, then explicitly assert that both candidate and expected identity construction fail with the stable exact argument boundary. No reflection, deserialization, or fabricated impossible state is used.

The source/PE scan finds only the policy and validator as installation-identity and exact-binding references; it records three core process-image constant references, three internal compiled types, no public type/constructor, mutable non-literal field, settable property, P/Invoke, or forbidden operational surface:

- .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/forbidden-surface-source-and-pe-scan.txt / 9962d292fb4d44b203dbbab76f9e8f6e9fd20871e972fd08aec3d7bd25fe3a5f;
- findings 0, status PASS; Broker DLL cbd65d2304c1e0dea275c86fb95399690564d9752b236274e19d8e662a39ab4b.

## Final aggregates and acceptance receipts

Final aggregates are in .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/manifests/final-source-aggregates.txt (ba31ddf0a9f9de517068c99365599d2e48b644452564fb02ccf4398f108e091f):

| Root | Count | SHA-256 |
|---|---:|---|
| services/VFXComposer.Broker | 34 | a82899f692fd3dcb568eb5bf820f90ecf92f4054b515f8d3621a2e34c361772a |
| services/VFXComposer.Broker.Tests | 15 | c6864861a756cd0458e40c886855ee775ef15cf3d04a5129550cf7e324d74835 |
| all other registered roots and solution | unchanged | replayed above |

1. dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true — exit 0, 0 warnings, 0 errors. stdout a2026cabd6330a054b5d41544370a15ac823f794289d743c0e3d9f49f0231800; empty stderr; exit receipt 9a271f2a916b0b6ee6cecb2426f0b3206ef074578be55d9bc94f6f3fe3ab86aa.
2. dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=broker-install-policy-p2-final.trx" --results-directory .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/final-test-results — exit 0; **61/61 passed**, 0 failed, 0 skipped. TRX final-test-results/broker-install-policy-p2-final.trx / a315c1962c428043d66f7bc96bf6a4ccb782f90f25067c65cfccb1a11a111b5c.
3. dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true — exit 0, 0 warnings, 0 errors. stdout 9b9102223a540c0eb186a8828d9af78109224f0ae2de8663a7ebc4231ddba306; empty stderr; exit receipt 9a271f2a916b0b6ee6cecb2426f0b3206ef074578be55d9bc94f6f3fe3ab86aa.
4. dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll — exit 23; stdout exactly 0 bytes; stderr exactly W24FS001 with CRLF, SHA-256 4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06.

Broker.Tests DLL: 2bf5851f95027a96492908a5913aa77bcb1dfc67ac3832c2a16a0fcbb1d29ad6.
The SDK constant check is sdk-header-constant-validation.txt / 1b47a6305b0975593b30b94e49fc5649ea0a34ccbf8d5f8d1840374cbc71b778, status PASS against local SDK 10.0.26100.0.
The command summary is command-summary.txt / 5bfc7255db31c088cebc1f53f42bc16169689cc1e610dc168e52a4af78ea5b56.

The final evidence manifest has 22/22 replayed entries, excludes itself and this handoff, and is ordinally path-sorted/LF/UTF-8 without BOM:

- .codex_tmp/WP-BROKER-P2-INSTALL-POLICY/p2-remediation/evidence-artifact-hashes.sha256 / 505db45b99bcffa9dbd9f77c2e91b92b92e7b91dd8a8bd93a53122c8daceaf39.

## Boundaries, self-audit, and blockers

Proved only: the immutable internal installation-policy vocabulary, exact correlation shape, and core candidate/expected process-image typed-hash-domain boundary. Not proved or enabled: installed/registered service; installer entrypoint; binary payload/location; executable-content identity; real account provisioning; SCM, registry, process, filesystem, environment, listener, network, project, Unity, Desktop, Worker, ACL application, registration, policy loading, or authority/verdict path.

- P0: none found. The Broker remains fail-closed at W24FS001/23 before an operational path.
- P1: the validator-only typed-domain check was bypassable through direct internal binding. The constructor now rejects the invalid state and Matches independently rejects either wrong domain; the deterministic no-reflection test covers the stable boundary. No further P1 is found in this atomic scope.
- P2: none found. The candidate remains dormant, internal, and non-wire.

Remaining blockers are an independently privileged host/issuer, actual SCM installation lifecycle, trusted executable-content identity, live attestation, ACL application, production policy activation, authenticated sessions, project gate, and all later authority gates. This handoff intentionally does not hash itself.

**STOPPED.** Do not extend this package into installation, SCM, registry, process, policy loading, listener, project, Unity, Desktop/Worker, or authority work. The independent final audit accepted closeout at `P0=0 / P1=0 / P2=0`.
