# W24 standalone desktop — Phase 1 report

Date: 2026-08-26  
Status: `PHASE_1_GO / DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY / PHASE_2_NOT_STARTED`  
Authority: none. This report grants no Broker registration, IPC session, project read/write, Unity Worker action, machine or visual verdict, user sign-off, L3, L4, Publication, or production transport authority.

## 1. Outcome

The bounded Phase 1 implementation and its local machine receipts are complete:

- `VFXComposer.Protocol` is a pure-C#/.NET 8 protocol library with strict JSON, exact numeric canonicalization, typed/self hashes, version and capability negotiation, request correlation, disconnected/job models, five distinct status DTO families, and nine Draft 2020-12 schemas.
- `VFXComposer.Client` is deliberately disconnected. It has no transport implementation, listener, project registration, project path, filesystem access, mutation command, or authority issuer.
- `VFXComposer.Desktop` is an Avalonia 11.3.2 MVVM shell with Dashboard, Library, Create, Preview, Patch, Review/Evidence, Jobs/Logs, and Settings/Diagnostics navigation. It starts without Unity or Broker and presents `Disconnected / No registered project`.
- Avalonia compiled bindings are enabled and every XAML root and binding-bearing template has an explicit data type.
- The Desktop/Client no-project-access gate scans the complete product assemblies, including non-public types, private method bodies and resolved IL member/type/string references. A private test-only `System.IO.File.Exists` fixture proves that the scanner observes private IL rather than only public API shape.

This is the independently audited Phase 1 snapshot, not permission to treat Phase 2 capabilities as implemented. The final frozen-byte audit reports `P0=0 / P1=0 / P2=5` and grants only `DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY`; Phase 2 remains `NOT_STARTED`.

## 2. Implemented boundary

| Component | Implemented in Phase 1 | Explicitly absent |
|---|---|---|
| Protocol | strict JSON limits and exact shapes; exact decimal normalization; canonical/self/typed hashes; handshake/capability negotiation; request/idempotency identities; stable diagnostics; disconnected/job models; machine/visual/user/L3/L4 presentation DTOs; nine schemas | Unity/Avalonia dependency, socket/pipe/HTTP/stdio transport, project path or content API, authenticated issuer, authority promotion |
| Client | in-process disconnected connection and request correlation | Broker client, Worker client, IPC, listener, project registration/query, filesystem access, mutation |
| Desktop | independently startable MVVM shell, eight navigation areas, in-memory diagnostics/error boundary, explicit disconnected and status presentation | project access, transport, listener, Worker action, preview media, evidence mutation, installer, authority/signature workflow |
| Unity package | preserved compatibility/diagnostic baseline only | no new Worker API or Unity UI work in Phase 1 |
| Broker | none | no process, service, issuer, registration store, native handle or authenticated named pipe |

The five status DTO families are presentation contracts only. They distinguish machine, visual, user verdict, L3 and L4 state, but no Phase 1 component authenticates their provenance, signs them, mints a verdict, or promotes one domain into another. Their existence is not authority evidence.

## 3. Machine receipts

The final receipt root for this report is:

`.codex_tmp/w24-phase1-final-receipts-20260826T064843Z`

The receipts establish only the following bounded facts:

- Debug solution build: six projects, `0` warnings and `0` errors.
- Debug tests: Protocol `68/68`, Client `8/8`, Desktop `9/9`; aggregate `85/85`, zero failed and zero skipped.
- Locked cold-cache offline verification: SDK `8.0.420`, `39` packages, `6` lock files, `39` package signatures verified, Release build `0/0`, and Release tests `85/85`.
- Schema verification with Python `jsonschema 4.26.0`: nine schemas, 36 positive cases accepted and 68 negative cases rejected.
- Disconnected Desktop smoke: normal close and exit `0`; title `VFX Composer`; `Disconnected / No registered project`; zero observed TCP/UDP listeners, watched-root filesystem events, Unity Editor processes, and Broker processes.

| Receipt file | SHA-256 |
|---|---|
| `debug-build.log` | `28903cd12d7122ad0d11496766f65549e289bbfe230c657f61c27cd9513b91e6` |
| `debug-tests.log` | `f134bb8eca87e3d875ebcaed29c01bbc0b0458d9d1a95a42d4e87d634f6e550c` |
| `disconnected-smoke.json` | `f89e331b6852fafc413b0b0d16b8eeac3951f354c4c7053323271daea6c21968` |
| `disconnected-smoke.log` | `f89e331b6852fafc413b0b0d16b8eeac3951f354c4c7053323271daea6c21968` |
| `offline-verification.json` | `9096de989b6c8fd8a391c8478d3338c37a896c261f53cea33cc47b66096ae326` |
| `offline-verification.log` | `c403350f22fc4d25c89853550b8d5b80c4643ef3b42c4e207d95e3a0ad005b56` |
| `phase1-debug_net8.0_20260826144856.trx` | `0bcf9ba05108a55d76f7a3d6197922a8468d13ccafbc599637e4d9a69eafbdcc` |
| `phase1-debug_net8.0_20260826144857.trx` | `aa9e0fba55007156a39b5606f85c5cc3e1d394df2b5020c41467d482683c8505` |
| `phase1-debug_net8.0_20260826144858.trx` | `3a5631b5d44b048c2da5b8a8ff96aa593a85bc62a860bc944149c40e73adb5f6` |
| `receipt-manifest.json` | `1ae7bc0a4a58da0c290d75c176ff41d5aea75e8e208e018b4be435855782a84a` |
| `schema-verification.json` | `f899c487463e16999d44aaa6faac7b18271c873ec62529bbe19ebe86f137dbbc` |
| `schema-verification.log` | `f899c487463e16999d44aaa6faac7b18271c873ec62529bbe19ebe86f137dbbc` |

The primary structured binding is `receipt-manifest.json` at SHA-256 `1ae7bc0a4a58da0c290d75c176ff41d5aea75e8e208e018b4be435855782a84a`; its exact scope is `DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY`, and its embedded `files` array binds the 11 generated receipts other than the manifest itself. Including that manifest, the 12-file on-disk receipt-root aggregate is `beafb472db61675f779e34db3c0858367015ab743074c8c6f00a3c80943f4128` under the manifest rule in section 4.

The accepted offline receipt is self-bound to run ID `af85aa21d76941a49dcbdc3b600cffb6`, verifier `d9e67b6c27e0eb1fc161e4ff92dd5bef0c4ae467b14839ea6705d16200fae269`, NuGet configuration `3b063d349c436a58e8c0a5bdd5269f54bc3aaa63ef81d1a925a14e3a5354ab9e`, and six-lock set `9702c94a1f1282a7ece18d409c3e61c920f9120ebd1a1194ee6acd8896c24fe9` using its recorded `ordinal-relative-path-sha256-lines-v1` encoding.

The accepted smoke receipt is self-bound to PID `7336`, executable `fe8a6afaa5afb2e680d9ff9ef0a435a72a76497049dfb6566b55cf891740db90`, verifier `d2b9e28fb641365b5bea90e3844ac278b70ad2384fedc555bdc54f748f633225`, four forbidden-process checkpoints and zero watcher errors. It ended normally with zero observed listeners and watched-root events.

## 4. Source and gate identities

Aggregate hashes in this section use the following reproducible rule: enumerate the stated file set, normalize paths to repository-relative `/` form, sort with `StringComparer.OrdinalIgnoreCase`, create UTF-8 rows `relative-path<TAB>lowercase-file-sha256`, join rows with LF and no trailing LF, then SHA-256 the resulting manifest bytes. The enumerated sets contain no case-insensitive path collision.

| Source set | Files | Manifest SHA-256 |
|---|---:|---|
| `src/VFXComposer.Protocol` | 32 | `bc319f70c269d2ba06811fb165066a0d0017329a4a4de57b705276e2165f08a5` |
| `src/VFXComposer.Protocol.Tests` | 16 | `dd363e9c7fd37781e50e376960778765364ba0eb7b27417d611b9b887292e3c1` |
| `docs/schemas/desktop` | 9 | `436f1d6f444546119780fb01cab50ac039596d7f97a74f491609a6956a8133ad` |
| Protocol + tests + schemas | 57 | `6427aead34e9634f0c4a86b8cfed39d5d9a98ee1e03121275b93e096d929c454` |
| `src/VFXComposer.Client` | 7 | `9541411501cf44346bc6e5eb3437e07a766b3e34eaf65162f5ba82bedcd6adb4` |
| `src/VFXComposer.Client.Tests` | 4 | `6cb725188b81a660a96e274b0655dc3ee7e48f6941de9f4c7b6de8ae3bf4c731` |
| `apps/VFXComposer.Desktop` | 41 | `1b47d61d280b51e2c1b11f50efbf38910e8f00388f9cce5e14aaee9515c3be12` |
| `apps/VFXComposer.Desktop.Tests` | 7 | `702a9dd5aa4e26ff9ff2b975949e4a702ad1603140186ffefbfad66c7f2847e1` |
| Client + Client.Tests + Desktop + Desktop.Tests | 59 | `9dced10ef3589d9ce1afa514459458f73f1fb61fc5b76186041b6a2a38a867ff` |
| Compiled-binding/assembly-scan hardening overlay | 12 | `753589d03b43ffab33675476684f573edb83d13d7b6a115ac9c68853753116a6` |

The Protocol integrity remediation is additionally bound at file granularity:

| File | SHA-256 |
|---|---|
| `src/VFXComposer.Protocol/Hashing/CanonicalJson.cs` | `8ef829552f38207d4950e4ebfd3364350e1d68aadda48bfce7b007d94264c4f6` |
| `src/VFXComposer.Protocol/Json/ExactDecimalNormalizer.cs` | `2df817c1023f18d0031b78c13e9c4193e7e53ac33b6e7530353fadc95431044c` |
| `src/VFXComposer.Protocol/Json/StrictJsonReader.cs` | `37ab25d64c9094929af4810f891295651b17cea171b23188ed3ccf030e89dff9` |
| `src/VFXComposer.Protocol/Json/StrictWireCodec.cs` | `b23ccc6aaeb3d67c959044979b850414b3ffec584457292c4bcc9f7f7f25fe5a` |
| `src/VFXComposer.Protocol/WireSchemaRegistry.cs` | `3b647127e85ab88bb6c8ce5c623a44a829033a3ee955cd872c7b5fd9f43b6732` |
| `src/VFXComposer.Protocol.Tests/CanonicalNumberTests.cs` | `e4a7a316e52a32b3a980d25986bce932218ace3412fc1831e1b6666151c70790` |
| `src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs` | `e24948d438aa51114715485b198fe51edd23e9641c8dba5604c9efc9662eeb1a` |

The final Desktop hardening overlay contains these exact files:

| File | SHA-256 |
|---|---|
| `apps/VFXComposer.Desktop.Tests/NoProjectAccessSurfaceTests.cs` | `d19d3fdca1870270e01c3b43bfd4983ce50d7ea6fc1a88f74977a6b957dca931` |
| `apps/VFXComposer.Desktop/App.axaml` | `805bd418085a88bf5b63ee58dd857c1788e942f9f3a4338e00c734d07248bca4` |
| `apps/VFXComposer.Desktop/VFXComposer.Desktop.csproj` | `d9cde6085b25e7519249a2941cf82ebc4cc0ec4cc5a1774821aa99e4b82b9a45` |
| `apps/VFXComposer.Desktop/Views/CreateView.axaml` | `bd3ac3145ba043229d71837e23ff43b32249c25ff47b629bb0b98c10aff22bb6` |
| `apps/VFXComposer.Desktop/Views/DashboardView.axaml` | `2412c52963e84875015d2d0fd5ff741e9738898350d9e25c8d7a79506ae55ea1` |
| `apps/VFXComposer.Desktop/Views/JobsView.axaml` | `bdfb4e2c15821f923946441f55b5068f246a533fe815c9096e13cdc101ac2d8f` |
| `apps/VFXComposer.Desktop/Views/LibraryView.axaml` | `b0a3b784cf8796d81cf3177adf0a3d59f91223e01a55162028ff7d55f12484ac` |
| `apps/VFXComposer.Desktop/Views/MainWindow.axaml` | `f85bade7b5c253337da4c19ef7eb79f977826538bfcd221bd7fcd1322adb2d4b` |
| `apps/VFXComposer.Desktop/Views/PatchView.axaml` | `0304926b3896fd6f946be1676a6d348be0be5fd3a700fdb18595fe3093d531bb` |
| `apps/VFXComposer.Desktop/Views/PreviewView.axaml` | `b170e60e7ea1aea5825bb0568e874561263c44c7c100e5e3922a1eb0f6833862` |
| `apps/VFXComposer.Desktop/Views/ReviewView.axaml` | `769bd6adf19c0d83b02f1166bec5d6782dca8cc91470eef8b95ec7c1e7d3692d` |
| `apps/VFXComposer.Desktop/Views/SettingsView.axaml` | `08ae4e76efc12d808231580d415f6db37b75ccab8909ca2566342a8b3938d7fe` |

The ten-file root build/gate set has manifest SHA-256 `17751fa033154897893dcb2b506fedcf87ececa4793cf1697edd6f27bb1eb646`:

| File | SHA-256 |
|---|---|
| `Directory.Build.props` | `894a759f2b4a0b9be7cd2a313d51f6af226743334bf065e51b8096903af72384` |
| `Directory.Packages.props` | `c5d74b3fe345052b6047ad72f7c0a9839afd2177450eb22d92aa64a0315d7dc9` |
| `eng/approved-packages.json` | `fe5e7d1545f4127ba5198092e112a67d890f1f9bafac3a63ad928f0527908410` |
| `eng/prepare-approved-feed.ps1` | `f8c08b7d03ec0922ee51be221394be3d8b9af25af9b99f67624ae23b919f75cf` |
| `eng/verify-disconnected-desktop.ps1` | `d2b9e28fb641365b5bea90e3844ac278b70ad2384fedc555bdc54f748f633225` |
| `eng/verify-offline-restore.ps1` | `d9e67b6c27e0eb1fc161e4ff92dd5bef0c4ae467b14839ea6705d16200fae269` |
| `eng/verify-phase1-schemas.py` | `b8c254932b6365883243f34cd09ca528e6151c35d13baab69b8249c85102e022` |
| `global.json` | `a2a97af890b9dbc08397f399c6b3802116f618f72d0873b4ea218d90765d2b3a` |
| `NuGet.config` | `3b063d349c436a58e8c0a5bdd5269f54bc3aaa63ef81d1a925a14e3a5354ab9e` |
| `VFXComposer.sln` | `e9e3d14972828f57dfc01d48597c34c99220ad6da019943b5313133d3ffd32a6` |

These identities are Phase 1 identities only. They do not identify a Broker, Worker endpoint, IPC session, project, Unity build, visual artifact, authority record, installer, or production deployment.

## 5. Dependency and offline-gate limitation

The offline receipt binds the approved package manifest SHA-256 `fe5e7d1545f4127ba5198092e112a67d890f1f9bafac3a63ad928f0527908410` and solution SHA-256 `e9e3d14972828f57dfc01d48597c34c99220ad6da019943b5313133d3ffd32a6`. It proves locked restore/build/test from the separately approved local package set and verifies the cached package signatures covered by that gate.

It is not a NuGet vulnerability/advisory receipt. The Phase 1 offline source does not itself provide a current advisory data result, and the current build baseline does not establish `NuGetAudit` coverage. A separately sourced and recorded NuGetAudit/advisory review remains pending. Until it exists, this report must not be read as “no known vulnerable dependencies.”

## 6. Smoke and visual limitation

The disconnected smoke receipt is an accessibility/structural and process/network/filesystem-sentinel check. It confirms that the expected window/state can be observed and that the bounded sentinels remained quiet during that run. It is not:

- a rendered-pixel snapshot comparison;
- human Desktop visual QA;
- Unity effect Preview evidence;
- proof of contrast, typography, layout quality or visual polish;
- a machine, Visual QA, user, L3 or L4 verdict.

Desktop rendered snapshots, keyboard/focus accessibility coverage and human presentation review remain Phase 4 gates. Unity effect quality remains governed by the existing visual and user-signature workflow.

## 7. Preserved evidence and non-rebinding

The old Unity UI, Models, tests and r31/r32/r35/r36 receipts remain predecessor compatibility/diagnostic baselines. None of those receipts is rebound to the Protocol, Client, Desktop, Broker, IPC, installer or future Worker API. Conversely, this Phase 1 receipt does not alter, replace or upgrade any historical Unity, production-read, visual or authority conclusion.

No old Unity UI file was deleted or demoted. It remains available as the compatibility/diagnostic layer until Desktop parity, later gates and explicit user confirmation permit a separate disposition.

## 8. Remaining blockers and next gate

- Independent final Phase 1 frozen-byte audit: complete, `P0=0 / P1=0 / P2=5`, scoped GO only for the disconnected Desktop and shared Protocol boundary.
- Broker/Worker ownership, authenticated named-pipe transport, native-handle registration and production read: absent and fail closed.
- Production real-read remains `REGISTRATION_ISSUER_PENDING / W24FS001` without an independently trusted Broker issuer.
- Project access, command transport, Preview/Review evidence mutation, installation and recovery remain Phase 2–5 work.
- Visual pass, user verdict, L3 and L4 remain ungranted.
- full110 formal projection remains blocked on the exact immutable QA `model-version-id`; this report neither supplies nor infers one.

Phase 1 is closed at this bounded gate. Phase 2 has not started; its Broker/Worker/IPC/registration capabilities remain absent and fail closed until separately implemented and admitted by the Phase 2 gate.
