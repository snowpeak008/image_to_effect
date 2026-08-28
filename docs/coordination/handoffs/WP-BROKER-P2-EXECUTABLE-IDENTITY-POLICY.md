# WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY handoff

Status: **STOPPED — DORMANT_EXECUTABLE_CONTENT_IDENTITY_POLICY_FOUNDATION_ONLY.**

Model: `gpt-5.6-terra`; reasoning: `max`.

The retained implementation window is `2026-08-27T11:32:35Z` through
`2026-08-27T11:40:56Z`. This package defines only internal, immutable,
non-wire, supplied-in-memory correlation state. It does not observe executable
bytes or a loaded image, and it does not claim an installed service, live
attestation, Authenticode, signing, certificate validation, or production
capability.

## Starting replay and frozen boundary

Before the first source write, all four package targets were absent. The
required baseline aggregates then matched exactly. The final replay excludes
only this package's two Broker additions and one Broker.Tests addition, and
reproduces that same starting state:

| Root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 34 | `a82899f692fd3dcb568eb5bf820f90ecf92f4054b515f8d3621a2e34c361772a` |
| `services/VFXComposer.Broker.Tests` | 15 | `c6864861a756cd0458e40c886855ee775ef15cf3d04a5129550cf7e324d74835` |
| `src/VFXComposer.Protocol` | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| `src/VFXComposer.Protocol.Tests` | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| `docs/schemas/desktop` | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |
| `services/VFXComposer.Broker.ServiceHost` | 8 | `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103` |
| `services/VFXComposer.Broker.ServiceHost.Tests` | 6 | `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c` |

The replay also matches `VFXComposer.sln` at
`b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`,
all four frozen legacy Unity files, `Program.cs`, `BrokerPolicy.cs`, the three
W24 control documents, ADR-002/003, the Phase plan, and the Phase 2 report.
The detailed receipt is
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/baseline-and-frozen-replay.txt`
(`3af8f025b5f482af09ef1c596d6d4df65c14c8cc8270398c52240b9dabe97ee5`),
with `overallPassed=True`.

## Scoped design

- `WindowsServiceExecutableContentIdentity` composes the already-frozen
  `WindowsServiceInstallationIdentity`, adds an exact positive `long` byte
  length, and accepts a supplied `TypedHash` only when its type tag is exactly
  `vfxcomposer.executable-content/1` by `StringComparison.Ordinal`.
- `Matches`, `HasExactIdentityBinding`, and
  `WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate`
  each independently require both candidate and expected content identities to
  have that exact domain before the fixed-time comparison. Each also requires
  both process-image domains to remain exactly the frozen process-image domain.
- The composed installation identity preserves the frozen profile-reference,
  Broker generation, service-SID, and process-image correlation semantics.
  The new layer additionally requires exact content identity and exact byte
  length. Zero, negative, and `long.MinValue` length inputs reject at
  construction.
- No new product type is public, serializable, mutable, or wired into
  `Program.Main` or `BrokerPolicy.TryLoadProduction`.

## Exact authored source manifest

Only the registered three source/test files were created. The manifest is
repository-relative, forward-slash, ordinally sorted, lowercase SHA-256, two
spaces, LF-terminated, and UTF-8 without BOM. It excludes this handoff:

```text
4b1b3f60ce0258d6094ab49e20a327556bc6c6db9a4c86156ab2df1a06f36aa8  services/VFXComposer.Broker.Tests/WindowsServiceExecutableIdentityPolicyTests.cs
9d2f844450f8e441fe0ab08db4209d84f1e8fcfcf516e722b3e4db96d74bed8a  services/VFXComposer.Broker/Configuration/WindowsServiceExecutableIdentityPolicy.cs
2fafd5619a7555292d55448715dace25566feccb77ffd54b8a2e5c070d013151  services/VFXComposer.Broker/Security/WindowsServiceExecutableIdentityPolicyValidator.cs
```

Receipt:
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/manifests/changed-source-manifest.sha256`
with SHA-256 `ae2033ae6402d7ac40c27844ca17c9fa432cbafd840637227142a0e006c8bddb`.

Final root aggregates are recorded in
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/manifests/final-source-aggregates.txt`
(`13b66f0e888a69b514755d9f8fd86ff041aa60409e6748c4286a056648bbc1c4`):

| Root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 36 | `2a8805352c20259f1c2b06b00102f83e7982f178b4de550072595cb126af98c1` |
| `services/VFXComposer.Broker.Tests` | 16 | `1ff9e8b497598c6a0f0a620657e24bbad04542e5e048974308cb6754393fac75` |
| Protocol, Protocol.Tests, desktop schemas, ServiceHost, ServiceHost.Tests, and solution | frozen | replayed exactly as above |

## Validation and durable receipts

1. `dotnet build services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, `0` warnings, `0` errors.
2. `dotnet test services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=broker-executable-identity-policy-final.trx" --results-directory .codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/test-results` — exit `0`; **68/68 passed**, 0 failed, 0 skipped. The seven new tests cover valid exact binding, cross/stale profile/generation/SID/process-image/content/length rejection, non-positive length rejection, same-wrong-domain/same-digest construction rejection, explicit dual-domain entry boundaries, internal immutable shape, and product-surface absence. Final TRX SHA-256: `7d356d4732f336d12e70d1f6e56aea55cf1928c668a499b65d0435298bd63153`.
3. `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, `0` warnings, `0` errors.
4. `dotnet services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll` — exit `23`; stdout is 0 bytes; stderr is exactly `W24FS001` followed by CRLF. The empty stdout hash is `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; stderr hash is `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06`.
5. The product source scan found 0 matches across 34 forbidden operational/API tokens; it also confirms that `Program` and `BrokerPolicy` remain unwired and that all three correlation entry points contain explicit candidate/expected domain checks. Receipt: `validation/forbidden-source-scan.txt` / `bb1c246fff135795e00863940c3bdcfaec62c31d42f93371ac1152567ee888ac`.
6. The compiled-assembly scan found exactly three new internal sealed types, with 0 public constructors, mutable non-literal fields, writable properties, and P/Invoke methods. Receipt: `validation/forbidden-pe-scan.txt` / `2a9de257e0918d3c813a8e94dd033e1da029e0ad58860d3cf308c53625d7b422`.

Per-command stdout, stderr, and exit receipts are under
`.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/`; their paths
and SHA-256 values are bound by the final 24-entry evidence manifest:

```text
.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/validation/evidence-artifact-hashes.sha256
a9417349bfa4c8ff0b9ceb3cafd81aa918a5e267c2c716793e04b5b4e38fe880
```

## Boundaries proved and explicitly not proved

Proved only: supplied in-memory identities can be represented and exactly
correlated within this internal dormant model, including the fixed
executable-content domain and byte length. The package stays detached from the
production entry point, and the controlled smoke remains fail closed before an
operational Broker path.

Not proved or enabled: byte observation; equivalence to a loaded image;
executable location or name; Authenticode, signer, certificate, or chain
validation; process or token observation; service installation, registration,
configuration, or start; SCM or registry mutation; ACL application; production
issuer, policy activation, listener, connection, project access, Worker/Desktop
integration, command handling, mutation, machine/visual/user verdict, L3, L4,
or authority.

## Self-audit and remaining blockers

- P0: `0`. No production wiring, operational API, service mutation, listener,
  project access, or authority path was added; direct Broker smoke remains
  `W24FS001`/`23`.
- P1: `0`. Candidate and expected typed-hash domains are independently enforced
  at construction and at every correlation entry point; same wrong domain plus
  same digest cannot construct either identity.
- P2: `0`. New types are internal, sealed, immutable, non-wire pure in-memory
  policy/correlation state; scans and tests find no forbidden surface.

Remaining blockers include an independently privileged issuer, successful live
service/token attestation, a trusted executable-content observation and its
separate verification gate, service lifecycle/SCM work, ACL application,
policy activation, authenticated production sessions, project gate, and every
later authority gate. Final independent read-only audit: `P0=0 / P1=0 / P2=0`.
This writer does not begin a next slice.

This handoff intentionally does not hash itself. **STOPPED.**
