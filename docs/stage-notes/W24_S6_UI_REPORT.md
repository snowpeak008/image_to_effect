# W24 S6 UI report — VFX Studio integration

Date: 2026-08-26
Status: the current source includes a dormant project-registration lease scaffold with no production issuer. The isolated r36 current-source gate passed registration `6/6`, filesystem `36/36`, envelope/Inspector `41/41`, Studio Models `9/9`, and Editor callback/integration `12/12`, for `104/104` with zero failed, skipped, or inconclusive cases; all five Unity processes naturally exited `0`. The filesystem executor remains removed. The internal Windows read scaffold is limited to `W24S6_LOCAL_FILESYSTEM_READ_SCAFFOLD_ONLY / NO_TRANSPORT / AUTHORITY_NONE`; production remains `DORMANT / REGISTRATION_ISSUER_PENDING / REAL_READ_NO_GO` and returns `W24FS001` before request parsing, drive/path queries, or file opens. External transport and all visual/user authority remain deferred.

## Delivered

- Studio Library now projects read-only W24 data from Recipe, authoritative BuildManifest, and (only when file hashes match) the persisted formal Contract and Implementation Trace. It exposes effect identity, contract path/file hash/canonical hash/revision, trace path/hash, carriers, lifecycle, build/capture/evidence-corpus identities, source Recipe, Runtime Entry, manifest ownership count, budget, and explicit missing/invalid reasons. Evidence corpus and verdict-record labels require their respective S5-safe path scopes plus byte-hash verification; a verified record file is still not displayed as an L4 decision.
- Library filters cover capability, carrier, lifecycle, maturity label, and working status. Missing evidence stays explicit; the conservative normal status is `VISUAL_PENDING`, commercial eligibility is always false in Studio, and a manifest display value cannot grant L3 or L4. The UI does not relabel a manifest claim as a production candidate.
- Detail view renders requirements with their evidence authority and visual locations, layers/carriers, forbidden substitutions, and per-requirement trace mappings with authority/cross-evidence references. Machine evidence, Visual QA evidence, and user verdict record presence are shown as separate non-equivalent fields.
- No Studio UI signs L4, creates a user verdict, writes a migration decision, or upgrades machine/QA evidence to L3/L4. The prior misleading “user-signed” review control was renamed to non-authoritative review evidence and its output explicitly remains `VISUAL_PENDING`.
- Preview opens or enters Play Mode only after checking the exact scene hash, the capture profile's exact prefab-manifest reference and byte hash, and a fresh strict parse of those same Manifest bytes. Every click also re-reads the indexed Contract and Trace through the S5 safe Formal-path/reparse-point/current-byte-hash boundary both before scene-replacement confirmation and immediately before `OpenScene`. Studio indexing and Preview share `W24StrictJsonText` plus the one segmented Runtime Entry validator for the exact effect-owned path, and Preview requires `HasRuntimeEntry`, exact `PrefabPath`/Manifest Runtime Entry equality, and a currently loadable `GameObject` Prefab before comparing the freshly parsed Runtime Entry/build identity with both the index and Trace. The generic Studio S7/S10 preview route is no longer used, and no alternate camera or sampler fallback is available.
- Create/Validate/Formal Build displays the current S5 production-gate result without accepting raw contract JSON. Studio now copies rather than writes raw Recipe drafts or Patch JSON; Formal Build refuses to build raw draft JSON and Patch application remains blocked pending a formal post-patch S5 plan with new immutable bindings.
- Added deterministic EditMode model tests for ordering/defaults, the new filters, status/maturity separation, forged L3/L4 display data, and missing contract/trace/evidence defaults.
- `VfxStudioWindow.OnGUI` now resolves its five real `Draw*` callbacks through one production dispatcher. A minimal internal test seam resolves those exact delegates without invoking IMGUI layout, and another internal seam invokes the real read-only Refresh path. This is callback/integration coverage only; batchmode does not prove pixels or visual rendering.
- Studio Recipe scanning and source-Recipe draft cloning now use `W24StrictJsonText`; decoded-equivalent duplicate Recipe keys fail closed instead of relying on Json.NET's replacement behavior. Persisted Contract/Trace objects are parsed from the same S5-pinned, strict-UTF-8 byte read that established their file hashes, rather than a second file open.
- Authoritative Preview now asks Unity's normal modified-scene confirmation before replacing the current scene. A refusal cancels without opening anything or clearing a dirty scratch Scene. The Preview build binding also compares the Manifest's raw 64-hex `buildHash` with the index while comparing its `sha256:` canonical form with the formal Trace.
- Removed `W24S6LocalReadOnlyExecutor`, public repository/project-root arguments, caller-supplied result paths, and all result persistence. There is no public filesystem execution or write surface. `W24S6LocalDocumentInspector` still accepts only an immutable in-memory byte request. The new internal `W24S6LocalReadOnlyFilesystemAdapter` can be issued only by a private `UNITY_INCLUDE_TESTS` binding for fixed-drive scratch coverage; its production entry has no issuer and returns `W24FS001` before parsing or I/O. Its result is fixed to `authority: none`, `machineGatePassed: false`, and `scope: local-filesystem-document-inspection-only`, and contains no document bytes or absolute/result path.
- Added `W24S6LocalProjectRegistration` as a source-only, no-input production resolver. It always returns `REGISTRATION_ISSUER_PENDING`, a null lease, and `W24FS001`. Its opaque `W24S6RegisteredProjectLease` contains no path or handle, is not serializable, and has a private issuer/constructor. Its internal identity, generation, usability, and revoke state plus public idempotent `Dispose` grant no authority; only `IssueForTests` is compiled under `UNITY_INCLUDE_TESTS`. The test lease is not accepted by `InspectProduction`, no `Register`/provider setter or registration JSON loader exists, and the existing synthetic path binding retains a separate test issuer.
- The operation envelope is now v2 and explicitly a non-admission structural comparison with no reviewed-plan authority or execution ticket. It has strict exact `FromJson`/`ToJson` boundaries that use camel-case schema names and string enums, reject missing/extra/wrong-type fields, and round-trip against the v2 schema. Its plan hash uses length-prefixed binary fields, and request/path validation rejects controls, `|`, traversal, drive/UNC syntax, apply tokens, and unknown operations. No trusted issuer currently exists, so no execution claim is made.
- Studio automatic review no longer trusts telemetry or Visual-QA booleans in the Trace. Ownership Manifest, Visual QA, strict budget, idempotence, and playback/reset remain unchecked pending reuse of an S5 verifier that checks referenced bytes, schemas, effect/contract/build/capture identities. A Trace Visual-QA claim may only display as `UNVERIFIED_TRACE_CLAIM_PRESENT`; it never sets `HasVisualQaEvidence`. Runtime Entry discovery now uses only the Manifest's exact canonical, segmented, effect-owned prefab path; the first-Prefab fallback was removed. Selection changes and refreshes reset all review state.
- Request, Recipe, Contract, envelope, Studio/Preview Manifest, and inspector Manifest loading now share `W24StrictJsonText`, a recursive text preflight before Json.NET. It rejects comments, single quotes, trailing commas, non-finite/out-of-range numbers, extra roots, decoded-equivalent duplicate keys, and isolated surrogates in property names or values while accepting valid raw/escaped mixed surrogate pairs. It permits at most 64 containers and 100,000 counted nodes (each container, property, and scalar value is one node), checks the limit before recursion, and also sets `JsonTextReader.MaxDepth = 64`. Contract conversion additionally uses decimal number parsing and finite checks.
- Inspection-request source text is rejected before lexical parsing above 5,596,504 characters: the exact 5,592,408-character maximum canonical base64 representation of a 4 MiB document plus a 4,096-character envelope allowance. Encoded length is checked before base64 decoding.
- Added focused source tests for structural non-authority, canonical plan hashing, request/path controls and UNC rejection, hash mismatch, invalid UTF-8, size limits, immutable/exact non-authority results, strict Contract parsing, Studio pending behavior, and review reset. The new read-scaffold test source additionally covers production zero-I/O closure, whole-plan effective-DOS-length validation before drive query/open, Windows lexical rejection, strict final-path namespace rejection, pinned parent/target identity replay, directory/local-junction/hardlink/size guards, writer sharing, mtime identity drift, unchanged scratch-tree hashing, and exact declared cleanup. Inspector source tests cover 47 Manifest/Contract object/array/null/string/boolean/number mutations with fixed diagnostics. These cases are bound by the accepted r36 receipts below.

## Isolated verification

The pre-Editor-integration S6 source baseline was re-synchronized to the isolated shadow and
verified in r12. These accepted results are historical regression evidence only: they predate the
current Preview and Editor-integration changes and do not verify the current source snapshot:

- `W24S6McpOperationEnvelopeTests`: `39/39 passed / 0 failed / 0 skipped / 0 inconclusive`,
  Unity process naturally exited with code `0`; XML UTC `2026-08-25 16:40:13Z`,
  `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r12/s6-mcp-envelope-current.xml`,
  `sha256:3e23a8ac052aecb262ac75d19763fde217358e041d54843ee1bd4589d7d2adaf`.
- `W24S6StudioModelsTests`: `9/9 passed / 0 failed / 0 skipped / 0 inconclusive`, Unity process
  naturally exited with code `0`; XML UTC `2026-08-25 16:40:30Z`,
  `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r12/s6-studio-models-current.xml`,
  `sha256:ab70c48ebba081347b3fe2bead78779cf0e6802f7422278a3cb661ef01adc26d`.

The Studio Models filter was rerun against the current `VfxStudioModels.cs` identity in accepted
r32. `VFXComposer.Tests.EditMode.W24S6StudioModelsTests` recorded `9/9 passed / 0 failed / 0 skipped
/ 0 inconclusive` in PID `37108`, at XML UTC `2026-08-26 02:16:34Z` (`0.0974363` seconds). The
process naturally exited with code `0`, and its `-batchmode -nographics` log reaches normal
`Cleanup mono`. Exact evidence:

- XML: `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r32-w24-s6-studio-models-regression/s6-studio-models-regression.xml`,
  `sha256:97a1f7052e6d141f8a9925f2ef122c1c9494e04075f3c567a81f0e9a99768839`;
- log: `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r32-w24-s6-studio-models-regression/s6-studio-models-regression.log`,
  `sha256:88c64aa31330b8838733b544fd9e1b8300200ae95b71c41385a747e80faadc25`.

r32 is current model-behavior regression evidence only. It does not replace the historical r12 MCP
envelope result and does not prove Editor pixels, visual QA, Player UI, or MCP transport.

Rejected attempts remain separate from the accepted r12 evidence:

- r10 recorded `38/39` and exited with a test failure. The failed assertion incorrectly searched the
  serialized document for the substring `Passed`, which also occurs inside the legitimate property
  name `machineGatePassed`; the test was narrowed to reject only exact authority fields. XML:
  `sha256:f76f1684432c32823e99a9ff019b1f1614c67f40ca371c8de74e76c3e1c10046`.
- r11 recorded `39/39`, but the Unity process did not naturally terminate and was stopped by the
  exact-PID watchdog after 600 seconds. It therefore failed the process gate despite its passing XML:
  `sha256:d26956a1fa5e50a853fa962cd9a68045a777a60072d56076a9a1d81a6ef541c7`.

The earlier durable focused-evidence receipt remains historical evidence for the smaller pre-inspector
test surface:
`D:/WorkWork/Assist/image_to_smart/artifacts/vfx-evidence/w24-stage-focused/run-20260825T143716Z/receipt.json`
(`sha256:de258073b98e2cec9642eb4400543cee0f16231463a8a4a737c7424ac5c74fcc`).
It must not be substituted for the current r12 source snapshot. The current S5 production-gate
regression used by the UI passed `26/26` with exit code `0`; its exact evidence is recorded in the S5
production-integration report.

These runs used only the isolated shadow and did not start, stop, or write through the canonical project.

## Current source/static verification and isolated runtime binding

- Completed at `2026-08-26T04:38:24Z`, recompiled `VFXComposer.Editor` and
  `VFXComposer.Tests.EditMode` with Roslyn and sanitized copies of the existing Bee response files.
  The current registration, binding, Windows reader, adapter, filesystem tests, and registration
  tests were supplied explicitly because the preserved response files predate them. Both compiler
  invocations exited `0`; Roslyn emitted only informational `USG0001` because the Unity additional
  file was absent. Outputs under `.codex_tmp/w24-s6-local-static/` are disposable compile products,
  not formal evidence. The subsequent r36 Unity Test Runner receipts below supersede this
  static-only boundary for the synchronized current source.
- Python Draft 2020-12 schema meta-validation plus complete positive and production-rejected emitted-shape
  instances passed for `w24-s6-local-filesystem-inspection-result-v1.schema.json`; two negative
  instances were rejected. r36 binds the current registration and read-scaffold source snapshot.
- No registration JSON/attestation schema was added: a project file must not self-authenticate a
  future lease. The current source-only audit reran Draft 2020-12 meta-validation over the four
  unchanged S6 envelope, document-request, document-result, and filesystem-result schemas; all four
  validated with exit `0`.
- The current focused filter is `W24S6LocalProjectRegistrationTests` (6 cases).
  It covers no-input pending acquisition, adapter zero-I/O closure, opaque/nonserializable API
  shape, lack of an adapter lease input, generation/revoke/dispose behavior, invalid identity/
  generation rejection, and source deny-lists for broker, transport, filesystem, drive, mutable
  provider, and Unity mutation APIs. r36 also reran
  `W24S6LocalReadOnlyFilesystemAdapterTests` (36), `W24S6McpOperationEnvelopeTests` (41),
  `W24S6StudioModelsTests` (9), and `W24S6EditorIntegrationTests` (12), for `104/104` current cases.

### Dormant project-registration scaffold — accepted isolated r36; production still W24FS001

`W24S6LocalProjectBinding.TryCreateProduction` now delegates only to the no-input registration
resolver. The resolver accepts no root, identity, descriptor, JSON, provider, or transport and
always returns a null lease plus `W24FS001`. The binding defensively disposes any future non-null
lease and still returns false until a separately reviewed binding integration exists. Test path
bindings and test lifecycle leases use distinct private issuers, and neither creates a production
adapter. Consequently this change does not solve the hostile DOS-device-remap bootstrap described
below and does not admit a real read.

Current source-only Roslyn output identities (disposable, non-formal):

- Editor DLL `sha256:ddf67ab6da46394e6917112c62c28835d706c50972bc6c8611939188170d665c`,
  ref DLL `sha256:16088484e2e715d4b90159c61230bda06be52feb6f5f960bb5bebc68f3252dd9`;
- EditMode DLL `sha256:2b44ab1fd6abc61795d3f456a7333b7bba93670fbe8f0cb64dd130b95b367ddc`,
  ref DLL `sha256:8c895b47043c85d67d70f3c54ebda74cdeb75bdcbfbeb300348a8d6b3e7c19f3`.

### Local filesystem read scaffold — accepted isolated r36; production real-read NO-GO

The exact filesystem filter is
`VFXComposer.Tests.EditMode.W24S6LocalReadOnlyFilesystemAdapterTests`, with 36 expected
parameter-expanded cases (23 standalone tests plus 13 lexical parameter rows). r36 passed all
`36/36` with zero failed, skipped, or inconclusive cases. Its local NTFS junction case uses
`FSCTL_SET_REPARSE_POINT`, contains no real UNC/SMB probe and no `Assert.Ignore`, and requires the
junction segment to stop traversal before any leaf open or inspector invocation.

r36 exact runtime receipts under `.codex_tmp/w24-stage-regression-results/r36-s6-registration-final/`:

- registration: PID `25640`, outer exit `0`, XML UTC `04:49:26Z`, duration `0.0821619`,
  `6/6`; XML SHA-256 `4c3525e0a1d84fb21609bb83e187475c40b8ba2c00f4452defe00dc9bbe1c020`,
  log SHA-256 `d3c1c5c76374c3ea85124c30e5f9dd5b9a86bdce0089c492fe445b93796391c7`;
- filesystem: PID `41228`, outer exit `0`, XML UTC `04:49:49Z`–`04:49:50Z`, duration
  `0.9360987`, `36/36`; XML SHA-256 `2cad8a4be28f6c0f842857c2852e9a2b16830ce0449c43f5d20b0988000220a5`,
  log SHA-256 `07a1ad5cdb72c869663fe12ad4e0c06dadd25a84b791913a5a33433ea36fb20e`;
- envelope/Inspector: PID `6580`, outer exit `0`, XML UTC `04:50:10Z`–`04:50:11Z`,
  duration `1.0967089`, `41/41`; XML SHA-256 `69bb5e5538d7cae30408a5e8e785d0728662c2e232ee7d1402ed14188a678dae`,
  log SHA-256 `c6968d563c2ba0a9bbf79b7e057c64e8139954790dd88f8b47c6fd9bbb293636`;
- Studio Models: PID `41320`, outer exit `0`, XML UTC `04:50:35Z`, duration `0.0853603`,
  `9/9`; XML SHA-256 `577ad931f7bc1d417b76f9d854c5abfebc24c798f7cbdb97aaf251db80e0d05a`,
  log SHA-256 `c29c7ab6b5f004d9ab433bc42811b4cbdb8780b26e1c4e64e1e6b580c64065c0`;
- Editor integration: PID `37980`, outer exit `0`, XML UTC `04:50:51Z`–`04:52:11Z`,
  duration `80.1018115`, `12/12`; XML SHA-256 `34f43d66b552a87091c24226d023f8c7bc6052c527ce81ef140a20ff6376cf47`,
  log SHA-256 `a4fa104d300cf533ee7a7c97374ec7e1e21174d7dd20271d5be63941f11bad17`.

Each r36 log records result write, Input shutdown, three licensing disconnects, `Cleanup mono`, and
`Application.Shutdown.CleanupMono`, with no C# compile error, fatal error, or crash marker. After r36,
Unity process count and shadow lock count were zero; local-read scratch and all 17 Editor-integration
scratch paths were absent in canonical and shadow. The 26 relevant source/test and matching `.meta`
files were byte-identical between canonical and shadow. r35 remains accepted predecessor evidence but
is superseded by r36 for the current registration/read/model/callback source snapshot.

Rejected history was retained rather than overwritten: r33 stopped all `36` cases in fixture setup
because the initial scratch path assumed a missing `.codex_tmp` parent (XML `9893437d36c7811fb966c025b6d61886c53c4b7c94bb7fdf92346c28efb5f6fd`);
r34 reached the bodies and passed `35/36`, with one false-positive API-name guard because
`IsReservedDeviceSegment` contains the letter sequence `Serve` (XML `cda350b10dcaab9c935d4fad6703783e21ca83c3634eef57ecde8e32b2e5a0fc`).
The final test uses exact action prefixes and a scratch GUID root whose parent already exists; neither
change weakens path containment, no-write policy, or cleanup.

After whole-plan lexical/length validation, only the trusted fixed DOS drive bootstrap is opened by
absolute `CreateFileW`. Every registered-root, target-parent, and target-leaf component is opened as
one counted name with `NtOpenFile`, an already pinned `OBJECT_ATTRIBUTES.RootDirectory`,
`OBJ_DONT_REPARSE`, and `FILE_OPEN_REPARSE_POINT`; no absolute-prefix target open remains. All parent
handles stay pinned through the read and post-read path/volume/file-index/attribute replay. Handles
share read only, target reads are capped at 4 MiB, and target metadata rejects directories, reparse
points, and `nlink != 1`. Native final paths accept only strict `\\?\X:\...` DOS-drive form. The
adapter deep-copies and validates the full envelope and all operations before drive query or pinning.
No path-based `FileStream` fallback, write API, process,
network/server/stdio/HTTP transport, `AssetDatabase`, Scene API, authority issuer, or result persistence
was added.

This does not establish a generally trusted drive bootstrap. `X:` plus `GetDriveTypeW(DRIVE_FIXED)`
cannot formally eliminate a hostile DOS-device remap race before `CreateFileW`; the claim begins only
after a trusted fixed-drive bootstrap handle exists. Production registration remains pending and
production therefore stays at `W24FS001` zero-I/O closure.

The r36 runtime-bound current SHA-256 set (source / matching `.meta` where applicable) is:

- registration `7095dfab8cec75eb1b3eac2c291ad313910076517af246f8795da12883908b1d` / `65e2ea979e20fcfb47e9786912a2ce144da05d28ae0f159ce9277f11f1a85420`;
- binding `38ed953138b92f01a4ee3e9b6068a3048a811e7fa7e58531705a2a49ee18234e` / `2c777194fc9191a0da4b2af82dcb1f7453c4c2b70c74e6705aea670b938405a3`;
- Windows reader `cb9a3abdda8e8cfcd63cd8295341e3569ae9e3f84f985230978b2a2335bcaabd` / `f9c7ce72fcd19f17d0a766c8195ea38725c997b1dcf7489b337939d85f83510b`;
- adapter `3cce8a8d4b5b5cb70c094732659c0c6a2bb00b0fc02efeb9887f4d32952db723` / `c2adb00a2c9d0172059d09a999ce640c81e90df2a5c6f92942921500ab9cacee`;
- envelope `00046f1d5bac50eb79386ba915cc401a65a34aa8177917d0613343da822238f9` / `626971b1b8ba64401e4b70f8c01b9930fa04c303c9a74c4193cbccbb1542b33f`;
- inspector `9591449602c8a2641557c61647608a1979455828cea1b1c7502f8076811db07d` / `44e742e28351751b0541dfa5fda77c8734398c6dbe7e2d314f630c36db6e3662`;
- strict JSON dependency `c9480177fce26b4b6aa55ba4b10fdc2352eeaef7a3a4bcfbcff752fe8dff20ec` / `02bed09284ffaaf0f24dd011969b75f7b0cde2ce062f4173b19790979ae6498d`;
- Contract dependency `c430ad1a66946bbd75a6c98eb1032718039745020fea778e176a02ff5c750486` / `79ee723e43bf977e566047e305853563d0a3fe5b99b1a839272e11b08c4eb559`;
- filesystem tests `7aeb77603a42e40bdc081fd758b158665fd513512708cdfe0c78757903c2d88b` / `8fe2ac92248a39d6d0b801d122351d1e97a2f5699042d6401667cee65fcb0b4f`;
- envelope/inspector tests `6b75d2ba2d9d312c336c63b7670891d5c5ae3359a95c2d7deb3f20107afa4a46` / `27db3bb8f533e84a192b02e4a31af1163310f5ccba9e7cf05326e6ef23e880e0`;
- registration tests `6cd21e24d6434a323434b85b0ca62bbb27be76835c02bdcb996a08ac71721ee4` / `550ced706f3436d283db0971b8a4e6811adf3f244cb6af760455af3f0bbc65b0`;
- filesystem-result schema `aa0c8745cc8f51a9ae278bb5a9a7a8815aa6cf7b0bdc5df44e157c9416d05cab`, envelope schema `92460cc7806f045387125cfb3436a7f371329574437a973b6c5163f9c34f1bff`, inspector-request schema `ec748b76ee679daf18cd603bdf7b3fdea48b7b40f357edc67a817c5841ed49ff`, inspector-result schema `1b29bde2f4db6b3ce09626966e5ae719a4e5c912d5063084eedd4dc96f4a7ee1`.

### Editor callback/integration gate — accepted isolated r31 and reconfirmed r36

The exact new filter is `VFXComposer.Tests.EditMode.W24S6EditorIntegrationTests`, with 12 expected
parameter-expanded cases:

- 1 real `AssetDatabase` Library Scan plus real window Refresh case, including a decoded-equivalent
  duplicate-key Recipe that must not be indexed;
- 2 real-scan forged-status cases (`L3` and `L4`), both required to remain `VISUAL_PENDING`,
  `UNASSESSED`, and commercially ineligible;
- 5 tab cases that resolve the actual `DrawLibrary`, `DrawCreate`, `DrawPreview`, `DrawPatch`, and
  `DrawReview` callbacks used by `OnGUI`, without invoking IMGUI or claiming a pixel render;
- 1 current-Contract-byte tamper guard, 1 missing Runtime Entry Prefab guard, 1 dirty scratch-host
  user-cancelled scene-replacement case, and 1 positive case that opens only an allow-listed scratch
  target Scene from an allow-listed clean scratch host Scene.

The one-time fixture allow-list contains exactly 17 files: valid L3/L4 scratch Recipes and their
`.meta` files, one duplicate-key scratch Recipe and its `.meta`, scratch host/target Scenes and their
`.meta` files, one real minimal Runtime Entry Prefab plus its `.meta` and generated-folder `.meta`,
two scratch BuildManifests, and pinned scratch Contract/Trace byte files. Before/after SHA-256 tree snapshots cover
`project/Assets`, `project/Packages`, `project/ProjectSettings`, and repository `docs`. Fixture setup
may differ only by those 17 paths; each test requires an identical before/after hash map; teardown
must restore the exact baseline. This proves the tested Refresh/callback/blocked/open operations have
no final source-tree change; it does not claim that every Studio action is globally write-free or
that no same-byte transient rewrite could ever occur. The tests never invoke review-evidence writing,
MCP transport, a filesystem executor, or an authority-granting path.

The fixture is hard-limited to `Application.isBatchMode`. At one-time setup it accepts either no
loaded Scene or exactly one clean, unsaved default batch-runner Scene whose name Unity reports as
either empty or exactly `Untitled`; Unity Test Runner transient roots are permitted only in that
initial non-user Scene, which the fixture explicitly replaces with the allow-listed saved scratch
host before any additive fixture authoring. Dirty, saved, differently named, or multi-Scene initial
state is rejected before Scene replacement. After
setup, every Single-mode replacement accepts only the exact scratch
host/target paths; teardown may discard dirtiness only from those scratch paths and reloads the
clean saved host. Final cleanup replaces only those scratch paths with an empty batch runner before
deleting them. Interactive execution is ignored before any write, and no canonical project Scene is
opened, saved, restored, or replaced.

The current canonical and r31 shadow source copies were rechecked byte-for-byte identical at these
exact SHA-256 identities:

- `VfxStudioWindow.cs`: `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`;
- `VfxStudioModels.cs`: `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`;
- `W24S6EditorIntegrationTests.cs`: `d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6`.

The isolated r28 attempt produced XML/log but is rejected as gate evidence: all 12 cases inherited
one `OneTimeSetUp` failure because Unity Test Runner's clean default `Untitled` Scene contained its
Main Camera and Directional Light roots. No test body ran. The fixture now treats roots in only that
clean, unsaved, sole batch-runner Scene as transient and still rejects dirty, saved, or multi-Scene
state.

The isolated r29 attempt is also rejected: all 12 cases inherited a `OneTimeSetUp` failure because
the same clean, unsaved default batch Scene reported `Scene.name == string.Empty`, rather than
`Untitled`; again, no test body ran. The initial and cleanup guards now accept only those two Unity
default-name representations.

The isolated r30 attempt is rejected with 9/12 passing: both forged-status Library cases and the
Library/Window refresh case could not find their valid scratch items, while all Preview and tab
callback cases passed. The exact shared parse failure was an `InvalidOperationException` from
indexing `palette` on a JSON string `style` token; `Scan` caught it and correctly omitted the item.
Studio now type-checks `style` as an object before looking for a palette, preserving its existing
string-style support and strict duplicate-key rejection.

The isolated r31 rerun is accepted callback/integration evidence. The exact filter
`VFXComposer.Tests.EditMode.W24S6EditorIntegrationTests` recorded `12/12 passed / 0 failed / 0
skipped / 0 inconclusive` in PID `45436`, from XML UTC `2026-08-26 02:13:56Z` through
`2026-08-26 02:14:30Z` (`33.9305551` seconds). The process naturally exited with code `0`; its log
reaches normal `Cleanup mono`. Exact evidence:

- XML: `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r31-w24-s6-editor-integration/s6-editor-integration-r4.xml`,
  `sha256:3a55396510e2f61b7eb301220ff4826816ee837a6bc59560ec3b869101653dda`;
- log: `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r31-w24-s6-editor-integration/s6-editor-integration-r4.log`,
  `sha256:0634b3ac5183cae6182716a3e6994dc00283e2c7eb27cc09b39b7fbcd103b41f`.

A post-run read-only check found all 17 declared scratch fixture paths absent in both canonical and
shadow trees. The log records `-batchmode -nographics`, a null graphics renderer, and the exact
filter. Therefore r31 proves the real Editor library/Refresh paths, exact callback dispatch,
fail-closed guards, scratch Preview integration, and final tree restoration covered by these tests;
it does not prove IMGUI pixels, visual appearance or QA, Player UI, or any MCP adapter/transport.

The historical accepted r12 focused evidence proves only the then-current Studio data/filter/status models, strict document
inspection, and MCP structural-comparison envelope. It does not prove an Editor-window render,
Player UI, external adapter/transport, network behavior, Unity mutation, or rollback behavior.

## Not claimed / deferred

- As of the standalone-desktop architecture stop-line, this Unity Editor UI is frozen as a
  compatibility and diagnostic baseline. No new five-tab, Player-UI, visual-polish, or embedded MCP
  feature work is authorized here; the source and r31/r32/r35/r36 receipts remain for regression
  comparison until the independent desktop reaches gated functional parity.
- No effect-level visual machine pass, real visual QA route, user verdict, L3, or L4 was created.
- No migration plan or token was displayed as a decision control or applied.
- No new build path was implemented; the UI calls the existing S5 evaluator only for persisted current bindings and reports its blockers.
- The embedded AI chat window remains unimplemented/low priority.
- No Editor-window pixel-render or visual-QA evidence, Player-build UI evidence, external MCP
  transport, network access, admitted production filesystem/Unity/project execution, result
  persistence, or rollback evidence was created. The internal read scaffold and in-memory document
  inspector are not executors, execution authority, machine gates, or formal-evidence producers.
  Production project registration and all production real reads remain fail closed. Installing or connecting a
  third-party MCP/Coplay provider still requires separate user authorization; the structural
  boundary cannot execute, write, grant visual authority, or alter L3/L4 status.
