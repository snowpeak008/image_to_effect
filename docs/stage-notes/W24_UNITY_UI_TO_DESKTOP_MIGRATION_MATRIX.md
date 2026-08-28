# W24 Unity UI → Desktop migration matrix

Date: 2026-08-26  
Status: `PHASE0_DOCUMENT_ONLY / STOP_THE_LINE / NO_DESKTOP_IMPLEMENTATION_EVIDENCE`  
Authority: none. This document does not authorize project reads, writes, Unity execution, Broker admission, MCP transport, Visual QA, L3, L4, Publication, or user sign-off.

## 1. Stop-the-line decision

Authoritative STOP-THE-LINE summary received for this Phase0 slice:

> 立即暂停所有Unity Editor主界面新增UI、五标签页、Player UI、美化及嵌入MCP入口；保留现有UI+r31/r32/r35/r36为兼容/诊断基线，Desktop对等前不删；目标=.NET8 Avalonia MVVM Desktop主界面 + pure C# Shared Protocol + Unity Package Worker + optional independent Broker(authenticated named pipe Windows first)。Desktop不得直写Unity工程；broker/worker未冻结继续fail closed；目录建议 apps/VFXComposer.Desktop, src/VFXComposer.Protocol, src/VFXComposer.Client, services/VFXComposer.Broker；IA Dashboard/Library/Create/Preview/Patch/Review/Jobs/Settings；旧证据不得冒充Desktop/Broker。

The current Unity Editor UI is frozen as a compatibility and diagnostic baseline. Effective immediately:

- do not add Unity Editor main-window UI, five-tab behavior, Player UI, visual polish, or an embedded MCP entry;
- preserve the current Unity UI source and the r31/r32/r35/r36 receipts until the independent Desktop has passed gated functional parity; do not delete the baseline first and reconstruct its behavior later;
- permit only separately approved compile-, corruption-, or security-critical fixes to that baseline; each such fix must freeze new hashes, obtain new compatibility receipts, and retain the prior bytes/receipts as predecessor evidence;
- target a .NET 8 Avalonia MVVM Desktop main interface, a pure-C# shared Protocol, a Client library, a Unity Package Worker, and an optional independent Broker using an authenticated Windows named pipe first;
- the Desktop and Client must never directly write the Unity project or invoke Unity APIs;
- until Protocol, Client, Broker registration/admission, and Worker command boundaries are separately frozen, every execution/read/write route remains fail closed;
- r31, r32, r35, and r36 are Unity compatibility evidence only. They are not Desktop, Avalonia, Client, Broker, transport, or authenticated-pipe evidence.

This Phase0 slice creates only this matrix. The suggested directories below do not currently exist and are not created by this document:

- `apps/VFXComposer.Desktop`
- `src/VFXComposer.Protocol`
- `src/VFXComposer.Client`
- `services/VFXComposer.Broker`

The Unity Worker remains a future role inside `project/Packages/com.vfxcomposer.unity`; this matrix does not add a Worker endpoint or admit any existing Unity code as one.

## 2. Frozen baseline and evidence classification

### 2.1 Current source baseline

| Key | Frozen compatibility source/test | Current SHA-256 |
|---|---|---|
| `U-WIN` | `project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioWindow.cs` | `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587` |
| `U-MODEL` | `project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioModels.cs` | `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68` |
| `T-MODEL` | `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6StudioModelsTests.cs` | `28188ffcbd31471876c137be3023aa668f93327a506dd4862c06d355e372d33d` |
| `T-INT` | `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6EditorIntegrationTests.cs` | `d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6` |

The authoritative descriptions and limitations are in `docs/stage-notes/W24_S6_UI_REPORT.md`, `docs/stage-notes/W24_S5_PRODUCTION_INTEGRATION_REPORT.md`, and `docs/vfx-reviews/W24_S5_MACHINE_FAILURE_PRODUCER_STAGE_REPORT.md`.

### 2.2 What the existing receipts do and do not prove

| Evidence | What it proves for the frozen Unity baseline | What it must never be cited as proving |
|---|---|---|
| r31 | `W24S6EditorIntegrationTests` 12/12: real Unity Library scan/Refresh, exact five `Draw*` callback resolution, guarded scratch Preview behavior, and declared final-tree restoration. | IMGUI pixels, visual appearance, Player UI, Avalonia/Desktop behavior, Client/Broker/Worker protocol, MCP transport, or production project execution. |
| r32 | `W24S6StudioModelsTests` 9/9 against the then-current/current-matching model identity: deterministic indexing, filters, conservative defaults, forged L3/L4 rejection, and review reset. | Editor rendering, Desktop view models, Desktop pixels, transport, visual QA, or any authority. |
| r35 | Accepted predecessor evidence for the S6 registration/read scaffold. It is retained for regression history and is superseded by r36 for the current source snapshot. | Current-source supremacy, Desktop/Broker readiness, transport, trusted registration, or admitted production reads. |
| r36 | Five isolated Unity filters, 104/104: registration 6, filesystem 36, envelope/Inspector 41, Studio Models 9, Editor integration 12. It binds the documented Unity source snapshot and confirms production `W24FS001` zero-I/O closure. | Desktop or Broker implementation, authenticated named-pipe behavior, MCP/server admission, Editor/Desktop pixels, visual QA, user authority, or production real-read GO. |
| S5 focused receipts | Existing S5 contract/Trace and production-gate behavior, including conservative `VISUAL_PENDING`, exact persisted bindings, transaction controls, and pending evaluator/issuer boundaries. | A Desktop command ticket, Broker authority, Visual QA, L3/L4, Publication, or a user verdict. |

Evidence rule: a future parity report may use r31/r32/r35/r36 only as the **legacy side** of a comparison. The Desktop, Client, Broker, transport, and Worker sides require their own source identities, tests, receipts, and explicit limitations.

## 3. Target ownership boundary

The following is a target responsibility split, not an implemented topology or an admitted execution chain.

| Component | Future responsibility | Forbidden responsibility |
|---|---|---|
| Avalonia Desktop | MVVM presentation for Dashboard/Library/Create/Preview/Patch/Review/Jobs/Settings; local transient editing state; user confirmation surfaces; accessible and testable rendering. | Unity API calls, `AssetDatabase`/Scene/Prefab access, direct Unity-project file reads or writes, S5/QA/L3/L4 authority, or treating displayed status as a command ticket. |
| `VFXComposer.Protocol` | Pure-C# versioned DTOs, enums, command/result/error shapes, stable identity/hash fields, capability negotiation, and explicit non-authority semantics. | Unity references, filesystem roots, mutable global providers, transport implementation, hidden fallback fields, verdict inference, or executable delegates. |
| `VFXComposer.Client` | Protocol serialization, correlation/cancellation, authenticated connection use, timeout handling, replay-safe response matching, and fail-closed version/capability checks. | Opening project files, resolving caller paths, Unity mutation, inventing Broker/Worker success, or granting authority from transport authentication. |
| Independent Broker | Optional only for disconnected Desktop/protocol development; mandatory for the Windows production-connected profile. Owns two authenticated named-pipe roles (Desktop and Worker), trusted project root identity/lease bootstrap, exact Worker-session handle delivery, admission, routing, job lifecycle, backpressure, audit correlation, and Worker availability state. | Project-content enumeration/parsing/read/write, direct Unity-project mutation, self-registration from project JSON, converting envelope comparison into execution authority, visual/user verdicts, accepting unauthenticated/ambient callers, or a Desktop→Worker fallback. |
| Unity Package Worker | The only future component allowed to call Unity APIs or touch project state, and only for an admitted, versioned command after the relevant S5/S6 gates and user confirmations pass; produces typed job events/results. | Trusting Desktop paths/status, bypassing S5, reusing a stale plan, turning an operational result into Visual QA/L3/L4, or exposing an unfrozen production endpoint. |

The frozen Windows production direction is `Desktop ViewModel → Client → authenticated Broker Desktop session → Broker admission/router → separately authenticated Worker session → existing gate-owned Unity operation`. Broker authenticates the exact Worker SID/PID/approved image/process epoch/generation before duplicating a least-privilege root handle to that PID. Broker may establish native root identity but may not enumerate or read project content; Worker alone uses the handle for content reads/writes and Unity APIs. Disconnected/test-only profiles may omit Broker and remain non-project-capable. Any replacement trusted host requires a new ADR and independent audit; there is no direct-Worker fallback.

## 4. Desktop parity gates

Each matrix row cites one or more of these gates. Passing a later gate does not waive an earlier one.

| Gate | Required evidence |
|---|---|
| `DP-0 Freeze` | Current four-file Unity baseline identity recorded; no new five-tab/Player/MCP/polish feature work; compatibility tests remain runnable. A separately approved compile/corruption/security fix requires new hashes and receipts while retaining the old baseline as predecessor. |
| `DP-1 Protocol` | Pure .NET 8 build with no Unity dependency; exact schema/version tests; missing/extra/wrong-type/unknown-version rejection; immutable request/result identities; explicit authority `none` where applicable; no absolute project path or caller-selected output path in public DTOs. |
| `DP-2 Desktop MVVM` | Deterministic ViewModel unit tests for state, filters, selection reset, disabled actions, errors, cancellation, and no silent fallback; Desktop/Client static dependency checks prove no Unity-project filesystem or Unity API access. |
| `DP-3 Client/Broker` | Separate Desktop↔Broker and Worker↔Broker authenticated Windows named-pipe receipts; peer SID/PID/approved image/process epoch/Broker generation, identity/lease/capability/version and exact handle-target binding; replay, disconnect, timeout, cancellation, unauthorized caller, wrong PID/image, stale registration/session, and Broker-absent fail-closed tests. Authentication proves channel/peer identity only, not S5 or visual authority. |
| `DP-4 Worker` | Isolated scratch-project Worker integration with exact source/protocol identity, admission replay immediately before action, allow-listed Unity API effects, before/after tree accounting, rollback/atomicity where mutation exists, natural process exit, and residue zero. No canonical project may be used as a parity fixture. |
| `DP-5 Desktop visual` | Avalonia rendered-pixel snapshots at frozen scale/theme states, keyboard/focus/accessibility tests, and human visual review for Desktop presentation. This evidence is distinct from Unity effect/capture Visual QA. |
| `DP-6 Authority` | Negative tests proving UI fields, Broker authentication, job completion, machine evidence, QA evidence, and user verdict are non-equivalent; no L3/L4/Publication/user-signoff action exists without the separately frozen private issuer and exact persisted binding. |
| `DP-7 Retirement` | Every `MUST-MIGRATE` behavior has passed its gates; no `KEEP-DISABLED` legacy dependency remains unless the user explicitly reclassifies it; Desktop release and rollback path are available; dependency scan finds no required old-window caller; user explicitly approves hide/retirement; S5/S6 core gates and historical evidence remain retained. |

Retention shorthand used below:

- `KEEP`: preserve the old behavior and tests as the compatibility baseline; only an approved compile/corruption/security fix may change bytes, and then it must create a new frozen baseline/receipt while preserving the old one as predecessor.
- `HIDE-LATER`: it may be hidden only after the row's parity gates pass, a reversible release switch exists, and user approval is recorded.
- `NO-DELETE`: deletion is forbidden until global `DP-7`; even after UI retirement, S5/S6 gate code and historical receipts are not UI debris and remain retained.

### 4.1 Frozen parity disposition registry

`MUST-MIGRATE` means Phase 5 parity cannot pass until the behavior has its own new-source gate. `KEEP-DISABLED` means no new control is exposed and the legacy diagnostic route must remain accessible; it blocks hiding the old window unless the user later reclassifies it. `DEFERRED` is not part of the current parity promise and does not authorize implementation.

| Disposition | Legacy/target behavior | Owning phase and required boundary |
|---|---|---|
| `MUST-MIGRATE` | Desktop shell/navigation, Refresh, Library projection/filter/details/status, selection reset | Phase 1 shell + Phase 2 read-only Worker query; DP-1–DP-6 |
| `MUST-MIGRATE` | Create fields, base-from-Recipe, prompt/draft clipboard, Validate, Dry Run, Formal Build | Phase 1 transient MVVM + Phase 3 exact commands/handlers/schemas; raw JSON never becomes a build ticket |
| `MUST-MIGRATE` | Preview metadata, authoritative open/close job, playback enter/exit | Phase 3 Preview commands and Worker guards + Phase 4 media identity/presentation; operational only, no evidence authority |
| `MUST-MIGRATE` | Patch queue/revision, Validate Patch, canonical copy, formal Apply | Phase 1 MVVM + Phase 3 `ValidatePatch`/`ApplyPatch` command, transaction and rollback gates |
| `MUST-MIGRATE` | Review status display, automatic checks, review draft/reset, non-sign-off review-evidence write | Phase 1 separated status types + Phase 4 query/command/identity/write-once gates; never a verdict or L3/L4 issuer |
| `MUST-MIGRATE` | Dashboard connection/status, Jobs/logs/cancel/backpressure, Settings registration/security | Phase 1 disconnected shell, Phase 2 Broker sessions, Phase 3 Jobs; transport health never means authority |
| `KEEP-DISABLED` | Ping/Reveal Recipe or Runtime Entry in Unity | No Desktop path reveal or Unity selection command in the current plan. The old diagnostic window remains available; hiding it requires migration or explicit user reclassification. |
| `KEEP-DISABLED` | Any sign-off, migration-decision, Publication or authority-issuing UI control | Such a control does not exist in the legacy baseline and must not be invented for parity. A future private issuer needs a separate ADR and user authorization. |
| `DEFERRED` | Embedded MCP/AI provider entry, Player UI, non-Windows transport and post-functional visual polish | Explicit current non-goals; they do not count as Phase 1–5 completion work and receive no fallback implementation. |

## 5. Global shell and cross-tab behavior

| Legacy behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| `Tools/VFX Composer/Studio`, five-tab dispatcher, shared left rail, shared status box | Dashboard shell plus navigation to all eight Desktop areas | `U-WIN` `Open`, `OnGUI`, `DrawTabs`, `ResolveActiveTabCallback`; `T-INT` five callback rows; r31 | Protocol owns navigation-safe state names only; Desktop owns shell; Client/Broker/Worker own no visual navigation | `DP-0`, `DP-2`, `DP-5` | `KEEP / HIDE-LATER / NO-DELETE`. r31 callback resolution is not a rendered-Desktop parity result. |
| Refresh on enable/button | Dashboard + Library refresh command; Jobs while remote work is in flight | `U-WIN` `OnEnable`, `Refresh`; `U-MODEL` `Scan`; `T-INT` real scan/Refresh; r31 | Protocol request/result; Client correlation; Broker admission; Worker read projection | `DP-1`–`DP-4`, `DP-6` | Desktop must not scan `Assets` itself. Hide Desktop Refresh until trusted registration and Worker read projection exist. |
| Shared filters/list selection | Library | `U-WIN` `DrawLeft`, `SelectItem`; `U-MODEL` filter/index; `T-MODEL`; r32 | Protocol library projection; Desktop VM filtering/selection | `DP-1`, `DP-2`, `DP-5`, `DP-6` | Selection resets dependent review state. Displayed maturity/status never becomes authority. |
| Status/reasons/help text | Dashboard, Library details, Jobs diagnostics | `U-WIN` status field/help boxes; `U-MODEL.StatusReasons`; r31/r32 behavior only | Protocol typed diagnostic codes; Client/Broker/Worker preserve codes; Desktop localizes/presents | `DP-1`–`DP-3`, `DP-5`, `DP-6` | Do not transport exception text, project bytes, or absolute paths as a substitute for stable diagnostics. |

## 6. Legacy Library tab → Desktop IA

| Legacy Library behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Scan Recipes and ownership Manifests; de-duplicate and sort by effect ID | Library data source | `U-MODEL` `Scan`/`Parse`; strict JSON and exact Runtime Entry checks; `T-INT` scan/duplicate rejection; r31 | Protocol typed library snapshot; Client; Broker trusted registration; Worker project read | `DP-1`–`DP-4`, `DP-6` | Desktop/Client never use `AssetDatabase`, `File`, or project roots. Production S6 registration currently returns `W24FS001`, so Desktop source remains unavailable. |
| Search and filters for archetype/dimension/element/style/capability/carrier/lifecycle/maturity/status | Library list/filter panel | `U-WIN.DrawLeft`; `U-MODEL.VfxStudioLibraryFilter`; `T-MODEL` independent-filter cases; r32 | Protocol filterable fields; Desktop VM performs deterministic filtering | `DP-1`, `DP-2`, `DP-5`, `DP-6` | Preserve exact conservative defaults and ordinal identity semantics; never infer status from maturity/evidence. |
| Show Recipe, Runtime Entry, Manifest ownership, Contract/Trace/evidence/verdict identities, costs, reasons, requirements, carriers, forbidden substitutions, and trace claims | Library details; Review for evidence separation; Dashboard for summary | `U-WIN.DrawLibrary`/`DrawW24Details`; `U-MODEL.ReadFormal`; S5/S6 reports | Protocol read projection; Worker verifies bytes; Desktop renders; Client/Broker merely carry | `DP-1`–`DP-6` | A verified file is not a decision. Trace authority/cross-evidence claims remain visibly `UNVERIFIED` unless the existing gate-owned verifier produced an admitted result. |
| Conservative `VISUAL_PENDING`, `UNASSESSED`, commercially ineligible; forged L3/L4 ignored | Dashboard + Library + Review | `U-MODEL.IndexForTests`/`ReadFormal`; `T-MODEL` forged L3/L4; `T-INT` forged manifest status; r31/r32; S5 report | Protocol explicit status domains; Worker/S5 is source; Desktop display only | `DP-1`, `DP-2`, `DP-4`, `DP-6` | No UI, Client, or Broker may promote status. Unknown/missing values fail to pending, never to pass. |
| Ping Recipe/Runtime Entry | Library contextual action | `U-WIN.Ping`; callback body has no focused parity test | Worker-only Unity selection/reveal command, if separately approved; Protocol/Client/Broker route it | `DP-1`–`DP-4` | Desktop cannot map asset paths to local filesystem or invoke Unity selection. Keep action hidden until a narrow Worker command is frozen. |
| “Use as Create Base” | Create prefill | `U-WIN.DrawLibrary`; in-memory state only | Protocol stable recipe projection; Desktop VM creates transient draft | `DP-1`, `DP-2`, `DP-6` | No project read fallback and no formal identity inheritance by implication. |
| “Preview” handoff | Preview | `U-WIN.DrawLibrary`; r31 proves callback/guarded scratch route, not pixels | Protocol selected-effect identity; Client/Broker job; Worker Preview command | `DP-1`–`DP-6` | Disabled until exact Preview Worker parity passes. Operational Preview never creates evidence or authority. |

## 7. Legacy Create tab → Desktop IA

| Legacy Create behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Description/ID/name/archetype/dimension/style fields | Create editor | `U-WIN.DrawCreate`; no focused field-interaction or pixel receipt | Protocol draft DTO; Desktop VM | `DP-1`, `DP-2`, `DP-5` | Local transient form state is not a Recipe, contract, build plan, or authority record. |
| Copy AI prompt | Create utility | `U-WIN`, `VfxStudioDraftBuilder.Prompt`; AI chat explicitly out of scope | Desktop owns clipboard; Protocol may own versioned prompt inputs, not provider execution | `DP-1`, `DP-2`, `DP-5` | No embedded MCP/AI route. Clipboard text grants no project or build permission. |
| Clone selected Recipe into an in-memory draft | Create “base from Library” | `U-WIN`, `VfxStudioDraftBuilder.FromRecipe`; strict JSON; r31 exercises duplicate-source rejection only | Worker reads pinned selected Recipe; Protocol carries a safe draft projection; Desktop VM edits transient copy | `DP-1`–`DP-4`, `DP-6` | Desktop must not open the Recipe path. No selected base means no fallback scan/read. |
| Raw draft text editing and copy | Create | `U-WIN.DrawCreate`/`CopyDraft`; validation is required before copy; no project write | Desktop VM/clipboard; Protocol versioned draft text limits and validation result | `DP-1`, `DP-2`, `DP-5` | Copying is not saving. Do not add a Desktop “Save into Assets” shortcut. |
| Validate / Dry Run | Create result + Jobs | `U-WIN.ValidateDraft` calls domain parser and S12 DryRun; S5 gate presenter; no dedicated end-to-end Create test | Pure extractable validation may move to Protocol only if Unity-free; compiler/S5 evaluation remains Worker; Broker owns job | `DP-1`–`DP-4`, `DP-6` | Never replace Worker/S5 validation with Client-side optimism. Version mismatch, unavailable Worker, or stale base blocks. |
| Formal Build (S5) | Create submission + Jobs | `U-WIN.FormalBuild` is deliberately blocked for raw JSON; S5 report defines exact approved-plan path | Protocol exact command; Client; Broker admission/job; Worker consumes gate-owned approved plan | `DP-1`–`DP-4`, `DP-6` | Keep disabled until an exact S5 plan/receipt protocol and Worker replay are frozen. Raw draft JSON can never be a build ticket. |

## 8. Legacy Preview tab → Desktop IA

| Legacy Preview behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Display selected effect, authoritative Scene, Scene hash, Runtime Entry, and binding explanation | Preview metadata panel | `U-WIN.DrawPreview`; `U-MODEL.VfxStudioAuthoritativePreview`; S6 report | Protocol immutable Preview projection; Worker verifies current bytes; Desktop renders | `DP-1`–`DP-5`, `DP-6` | Desktop may display only Worker-verified identities and stale/pending states. It must not resolve project paths itself. |
| Open authoritative Scene | Preview action + Jobs | `VfxStudioAuthoritativePreview.TryOpen`; `T-INT` tamper/missing-prefab/cancel/positive scratch cases; r31/r36 | Protocol command; Client/Broker; Worker alone calls Scene APIs and repeats physical binding guard immediately before replacement | `DP-1`–`DP-4`, `DP-6` | User confirmation is not transferable authority. No alternate Scene/camera/sampler fallback. Hide until Worker parity and isolated scratch receipt exist. |
| Enter/exit authoritative Play Mode | Preview action + Jobs | `U-WIN.ToggleAuthoritativePlay`; not independently covered by r31 | Worker-only Unity action after the same guard; Broker job lifecycle; Desktop confirmation/status | `DP-1`–`DP-4`, `DP-6` | No Desktop process control or direct Unity launch. Worker-unavailable and cancellation paths fail closed. |
| Ping Runtime Entry | Preview contextual action | `U-WIN.Ping`; no focused behavior receipt | Narrow Worker selection/reveal command if approved | `DP-1`–`DP-4` | No direct path reveal or asset load in Desktop/Client. |
| “Operational only” Preview | Preview boundary banner; Review retains evidence state | `U-WIN.DrawPreview`; S6 report | Protocol explicit non-evidence result; all components preserve it | `DP-1`, `DP-2`, `DP-6` | Opening/playing a Scene is not machine evidence, Visual QA, a user verdict, L3, or L4. Desktop pixels are not Unity effect-quality evidence. |

## 9. Legacy Patch tab → Desktop IA

| Legacy Patch behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Queue style, palette, archetype/content/behavior, replace, remove, and clear operations | Patch editor | `U-WIN.DrawPatch`; `VfxStudioPatchQueue`; r31 proves only callback resolution | Protocol exact operation union/path/value limits; Desktop VM queue | `DP-1`, `DP-2`, `DP-5`, `DP-6` | No loose JSON operation or arbitrary Unity property path. Queue state is not an applied Patch. |
| Bind expected Recipe revision | Patch editor | `U-WIN.expectedRevision`; `VfxPatchService.Validate` call | Protocol revision/base identities; Worker replays current base; Client/Broker carry | `DP-1`–`DP-4` | Stale or missing revision blocks; no Client-side auto-rebase or silent retry against different bytes. |
| Validate Patch | Patch result + Jobs | `U-WIN.ValidatePatch`; callback body lacks focused integration evidence | Pure validation may move to Protocol only if Unity-free; authoritative current-base validation remains Worker | `DP-1`–`DP-4`, `DP-6` | Desktop cannot read the selected Recipe. Worker absence returns unavailable, not valid. |
| Copy Patch JSON | Patch utility | `U-WIN.CopyPatch`; no project write | Desktop clipboard; Protocol canonical serialization | `DP-1`, `DP-2` | Copy is not apply, approval, or S5 admission. |
| Apply through formal pipeline | Patch submission + Jobs | `U-WIN.ApplyPatch` is deliberately blocked; S5 report requires a new exact build/capture/evidence binding | Protocol command and approval references; Broker job; Worker/S5 transaction | `DP-1`–`DP-4`, `DP-6` | Keep hidden/disabled until the formal post-patch S5 plan and rollback/atomicity gate exist. Desktop never writes Recipe/Patch/Prefab/Manifest. |

## 10. Legacy Review tab → Desktop IA

| Legacy Review behavior | Desktop IA destination | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Display machine route, Visual QA route, and user-verdict record separately | Review detail; Dashboard summary | `U-WIN.DrawReview`; `U-MODEL` separate fields/defaults; `T-MODEL`; S5/S6 reports | Protocol separate typed domains and provenance; Worker verifies bytes; Desktop renders | `DP-1`–`DP-6` | These three fields are non-equivalent. Missing remains `NOT_RECORDED`; a verified record file is not an L4 decision. |
| Run automatic checks | Review diagnostics + Jobs | `U-WIN.RunReviewChecks`; `VfxStudioAutomaticReviewChecks` keeps Manifest/budget/idempotence/playback pending; `T-MODEL`; r32 | Protocol check/result types; Worker reuses exact S5 verifier when available; Broker job | `DP-1`–`DP-4`, `DP-6` | Never trust Manifest/Trace telemetry booleans. No optimistic completion or local Desktop recomputation of Unity evidence. |
| Disabled automatic checkboxes and editable shape/layers/motion/dissipation/depth notes | Review session VM | `U-WIN.DrawReview`; `VfxStudioReviewState`; `T-MODEL` reset; no Desktop pixel evidence | Desktop VM; Protocol non-authoritative draft-note shape if persistence is later approved | `DP-1`, `DP-2`, `DP-5`, `DP-6` | Manual boxes are observations only; they cannot create QA pass, a user verdict, L3, or L4. Selection/refresh resets them. |
| Reviewer field and “Write review evidence (not sign-off)” | Review submission + Jobs | `U-WIN.WriteReview` writes `docs/vfx-reviews/<id>/REVIEW.md`; r31 explicitly does not invoke it | Future Protocol command; Broker admission; Worker-only write after separately frozen safe path/write-once policy | `DP-1`–`DP-4`, `DP-6` | Desktop direct write is forbidden. Keep the Desktop control hidden until the Worker write protocol and audit receipt exist. The output remains non-authoritative notes. |
| No signing or migration-decision control; pending banner | Review boundary | `U-WIN.DrawReview`/`ToMarkdown`; S5 issuer pending | Protocol must omit sign-off authority; future private issuer remains outside ordinary UI/transport DTOs | `DP-1`, `DP-2`, `DP-6` | Do not add a “temporary” Desktop sign button. A future user-authority flow needs a separate reviewed design and exact persisted binding. |

## 11. New Desktop IA without a one-to-one legacy tab

| Desktop area / behavior | Legacy relationship | Current source/test/evidence | Future owner | Desktop parity gate | Retention and boundary |
|---|---|---|---|---|---|
| Dashboard | Consolidates selected-effect identity, conservative status, gate blockers, recent jobs, and connection state; old UI has only list/status/help fragments | `U-WIN` status and S5 presenter; `U-MODEL` conservative fields; r31/r32/S5 | Protocol summary projections; Desktop VM; Client connection state; Broker/Worker health as typed pending/unavailable | `DP-1`–`DP-6` | Dashboard is a projection, never an authority or execution ticket. No green “ready” state from transport health alone. |
| Jobs | Replaces synchronous status-string UX for Preview/validate/build/patch/review operations | No existing job model, Broker, authenticated pipe, or Worker endpoint; r31/r36 do not fill this gap | Protocol job state/events; Client correlation/cancel; Broker lifecycle; Worker execution/result | `DP-1`–`DP-4`, `DP-6` | Until implemented, every job action is unavailable. Do not simulate success from old Unity callbacks. |
| Settings: project registration | Replaces implicit Unity-project context | S6 registration source is dormant; r36 proves `REGISTRATION_ISSUER_PENDING` and production `W24FS001` before I/O | Protocol opaque registration identity only; Broker owns trusted lease; Client displays; Worker accepts only admitted lease | `DP-1`–`DP-4`, `DP-6` | No caller-entered root, absolute path, project JSON self-registration, mutable provider, or “trust this folder” bypass. |
| Settings: connection/security | No legacy equivalent | No Broker/Client/pipeline implementation evidence | Client and production-required Broker; Windows authenticated named pipe first | `DP-1`, `DP-3`, `DP-6` | Broker absence, auth failure, version mismatch, or stale lease remains fail closed. HTTP/stdio/MCP/direct-Worker fallback is not implied. |
| Embedded MCP entry | Explicitly stopped, not migrated from the five tabs | S6 envelope v2 is structural comparison only; r36 proves no transport/admission | None in Phase0; any future external integration requires a separate user-authorized design through Client/Broker/Worker boundaries | New authorization plus `DP-1`–`DP-6` | Do not add it to Unity or Desktop during this migration. Envelope equality is not an execution ticket. |
| Player UI | Explicitly stopped | No current Player-UI parity evidence | Unassigned | Separate future authorization | Do not build, count, or delete anything on its behalf in this migration. |
| Visual polish and AI chat | Explicitly stopped / low priority | r31 is `-nographics`; no Desktop visual evidence | Desktop only after functional gates; provider integration unassigned | `DP-5` plus separate authorization | Functional/safety parity first. No provider/plugin connection is authorized by this document. |

## 12. Authority, write, and visual boundaries

These constraints apply to every row and cannot be relaxed by presentation parity:

1. The Desktop and Client perform no direct Unity-project read or write. Broker may only establish native root identity and deliver least-privilege handles; it never enumerates, parses, or reads project content. All project-content access belongs to a future admitted Worker query/command behind trusted registration.
2. An authenticated named pipe proves an authenticated channel/caller under its future threat model; it does not prove an approved S5 plan, machine verdict, Visual QA, user verdict, L3, L4, or Publication.
3. Broker admission and job completion are not authority. The Worker must revalidate exact current bindings immediately before any Unity operation; mutation additionally needs atomic publication/rollback and exact before/after accounting.
4. Machine evidence, Visual QA evidence, and user verdict records remain separate. No UI field, color, checkbox, receipt presence, or file-hash match may upgrade one into another.
5. `VISUAL_PENDING`, `UNASSESSED`, `NOT_RECORDED`, and commercially ineligible remain the conservative defaults. Unknown, stale, missing, mismatched, or unsupported input fails to those pending/blocked states, never to pass.
6. Preview open/Play Mode is operational behavior only. Unity effect/capture Visual QA does not prove Desktop rendering, and Desktop screenshot approval does not prove Unity effect quality.
7. r31 proves callback/integration behavior without pixels; r32 proves model behavior; r35 is predecessor scaffold evidence; r36 proves current Unity regressions plus production zero-I/O closure. None may be relabeled as Desktop/Broker/transport evidence.
8. The S6 local filesystem adapter remains `W24S6_LOCAL_FILESYSTEM_READ_SCAFFOLD_ONLY / NO_TRANSPORT / AUTHORITY_NONE`; production registration and real reads remain `NO-GO`. It is not a ready-made Desktop repository API.
9. The S5 descriptor replay/evaluator path remains pending and zero-write in production. A Jobs screen must not present its test-only structural replay as a machine verdict or advancement route.
10. Existing S5/S6 gate, inspector, strict-JSON, registration, transaction, and evidence code is not “old UI” and is not eligible for deletion merely because the five-tab window is later hidden.

## 13. Preserve, hide, and retirement decision

Current decision: preserve the currently frozen five-tab Unity UI and its tests as the compatibility baseline; do not extend it. A separately approved compile/corruption/security fix follows the rebaseline rule in §1 and does not erase the predecessor bytes or receipts.

A single legacy behavior may be hidden only when:

- all gates cited by its matrix row pass on frozen Desktop/Protocol/Client/Broker/Worker identities;
- its negative safety behavior is demonstrated, not merely its happy path;
- a reversible release switch and documented legacy diagnostic access remain available;
- the user explicitly approves the hide step.

Deletion remains prohibited until global `DP-7`. A retirement proposal must additionally show:

- complete parity across Dashboard, Library, Create, Preview, Patch, Review, Jobs, and Settings;
- separate Desktop rendered-visual evidence and Worker scratch-project integration evidence;
- authenticated transport receipts and fail-closed Broker/Worker absence behavior;
- zero remaining callers of the old window surface;
- retention of the historical r31/r32/r35/r36 receipts and S5/S6 reports as immutable migration evidence;
- a rollback path that does not require reconstructing deleted source;
- explicit user approval identifying exactly which UI-only files may retire.

## 14. Phase0 blockers and next admissible slice

### P0 — stop-the-line violations

- Any new Unity five-tab, Player UI, visual-polish, or embedded MCP implementation.
- Any Desktop/Client direct Unity-project access or write.
- Any claim that r31/r32/r35/r36 proves Desktop, Broker, transport, or authenticated-pipe behavior.
- Any deletion or hiding of the frozen Unity baseline before row-level parity and user approval.
- Any Broker/Worker route that converts transport authentication, envelope comparison, or UI state into S5/visual/user authority.

### Future phase gates — not open Phase 0 findings

- Phase 1 freezes pure-C# Protocol versioning, exact DTO field sets/schemas, stable errors, capability negotiation, disconnected Client/Desktop behavior, offline locked dependencies, and non-authority result semantics.
- Phase 2 freezes the mandatory Windows production Broker profile, both authenticated named-pipe peer roles, lease issuance/revocation, exact Worker PID/epoch/generation handle delivery, read-only Worker bridge, replay/disconnect and Broker-absent behavior.
- Phase 3 freezes Worker command admission, exact S5 replay point, project read/write allow-lists, Client/Broker/Desktop Jobs routing, cancellation, backpressure, atomicity/rollback and evidence/authority separation.
- Phase 4 freezes media/evidence identity, Desktop rendered/accessibility evidence and authority non-equivalence.
- Each `DP-*` gate becomes an executable test plan with distinct Desktop, Client/Broker and Worker receipts in its owning phase. These future gates do not prevent Phase 1 source creation after Phase 0 independently reaches P0=0/P1=0; they do prevent later capabilities from being pulled forward.

### P2 — deliberately later

- Desktop visual polish beyond the functional/accessibility baseline.
- Player UI, embedded AI chat/MCP, non-Windows transport, and provider/plugin integrations.
- Legacy window retirement after—not during—functional migration.

After the complete Phase 0 document set independently reaches P0=0/P1=0, the already authorized next slice is a pure .NET 8 Protocol plus disconnected Client/Desktop skeleton with no Unity reference, listener, transport or project I/O. Before that GO, only source-free/fail-closed design work is admissible. It is never another Unity UI slice.
