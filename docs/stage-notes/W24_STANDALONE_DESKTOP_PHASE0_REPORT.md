# W24 standalone desktop — Phase 0 architecture-freeze report

Date: 2026-08-26  
Status: `PHASE_0_GO / PHASE_1_DISCONNECTED_SHELL_AND_PROTOCOL_ONLY`  
Authority: none. This report grants no Broker registration, project read/write, Unity Worker command, machine/visual verdict, user sign-off, L3, L4, Publication, or production transport authority.

## 1. Outcome

The user STOP-THE-LINE decision is recorded and the Phase 0 gate is complete:

- Unity Editor main-window feature work is frozen. The existing five-tab UI, Models, tests and r31/r32/r35/r36 receipts remain compatibility/diagnostic baselines.
- The accepted product direction is a .NET 8 Avalonia MVVM Desktop, pure-C# Protocol, Client, Unity Package Worker, and a Broker that is optional for disconnected/test profiles but mandatory for the Windows production-connected profile.
- Desktop and Client perform no direct Unity-project read or write. Broker may establish native root identity and deliver least-privilege handles, but Worker alone may inspect project content or use Unity APIs.
- Windows production topology has two authenticated named-pipe peer roles, Desktop↔Broker and Worker↔Broker. There is no Desktop→Worker or drive-letter fallback.
- Machine, Visual QA, user verdict, L3 and L4 remain separate typed domains. UI state, transport authentication and job completion do not issue authority.
- No new solution, build root, `apps/`, `src/`, or `services/` code was created during Phase 0.

Phase 1 is now admitted only within the frozen boundary: pure Protocol, disconnected Client, and Desktop shell with zero listener and zero project access. Phase 2–5 capabilities remain gated.

## 2. Frozen document set

| Document | Lines | SHA-256 |
|---|---:|---|
| `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md` | 230 | `a196422ab9c47fe6903acf28527ca0bc0889c5225c827c2cd8fa9fe808be14b8` |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md` | 483 | `a9fb48a7cf01bf6d5e74bbf78bddd696f3e65ae27b81a1ae4816b8aeed8b3bd1` |
| `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md` | 242 | `db1172620c9b27dd60a5381b68dc9d0e7ba894812ebae6288a4e7d512c937c5b` |
| `docs/allwork/24_VFX_DESIGN_TO_IMPLEMENTATION_SYNC.md` | 897 | `ebae24bd912c0795fa253ce1684071fcdc72fa5ab2bd4b92bbaafac9dba4c496` |
| `docs/allwork/00_INDEX_AND_ACCEPTANCE.md` | 159 | `9546f1c022b2b3286cd90aab6d944fd0363afea942fb9a060d768c39f7a6594a` |
| `docs/DECISIONS.md` | 64 | `fd3d6dd3ef3f21b7176a9f6031b510776fcb1de0017ef4ed6640bb8d547dbc23` |
| `docs/stage-notes/W24_S6_UI_REPORT.md` | 290 | `fb54aef63c41afa7e18e084ccbe3f8a2b77ca07bbad5038c46ec3d6319cc3f7a` |

The S6 report stayed byte-identical while the architecture documents were authored. Its r36 `104/104` receipts remain evidence only for the current Unity compatibility/read-scaffold snapshot.

## 3. Independent audit trail

The independent reviewer performed three read-only rounds and did not edit files, run Unity, or access the network:

1. Initial review: `P0=0 / P1=6 / P2=4`, Phase 1 code roots `NO-GO`. It identified missing per-phase estimates, an unresolved production trust topology, phase-gate wording conflict, incomplete Phase 2–4 file/test/schema plans, project-access ownership ambiguity, and an offline dependency-plan gap.
2. Remediation review: `P0=0 / P1=2 / P2=0`, still `NO-GO`. It required a normative migration disposition for every mapped behavior and explicit Phase 1 machine/visual/user/L3/L4 status ownership.
3. Final frozen-byte review: `P0=0 / P1=0 / P2=0`, scoped `GO` to enter Phase 1.

The final review specifically confirmed:

- six Phase 0–5 elapsed-day estimates and the cumulative 3–5-week planning boundary;
- mandatory production Broker topology, two peer sessions, Worker-only project-content access and no direct fallback;
- a Phase 2 read-only Worker bridge, complete Phase 3 Jobs routing, and explicit Phase 4 schema/test/receipt gates;
- exact wire-schema parity, status-domain separation, Desktop/Client no-project-access tests, and locked/offline dependency gates;
- `MUST-MIGRATE / KEEP-DISABLED / DEFERRED` disposition, with Ping/Reveal blocking legacy-window hiding until migrated or explicitly reclassified;
- narrow critical-fix rebaselining without deleting predecessor bytes or receipts;
- absence of all planned Phase 1 code/build roots at the audit boundary;
- no rebinding of r31/r32/r35/r36 to Desktop, Client, Broker, IPC, installer or new Worker APIs.

## 4. Preserved Unity baseline

| File | SHA-256 |
|---|---|
| `project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioWindow.cs` | `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587` |
| `project/Packages/com.vfxcomposer.unity/Editor/UI/VfxStudioModels.cs` | `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68` |
| `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6EditorIntegrationTests.cs` | `d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6` |
| `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6StudioModelsTests.cs` | `28188ffcbd31471876c137be3023aa668f93327a506dd4862c06d355e372d33d` |

These bytes are not Desktop evidence. A separately approved compile/corruption/security fix must create a new frozen baseline and receipt while retaining this one as predecessor.

## 5. Phase 1 admission boundary

Permitted next work:

- pinned .NET 8 solution/build metadata and approved offline NuGet provenance;
- pure-C# Protocol DTOs, strict JSON, schemas, typed/self hashes and golden vectors;
- disconnected Client and Avalonia MVVM shell/navigation;
- explicit non-authority machine/visual/user/L3/L4 presentation models;
- tests proving no Unity dependency, no listener and no direct project read/write.

Still prohibited:

- Broker listener/issuer activation, project registration or production real-read;
- Unity Worker endpoint, Unity action, command queue or project mutation;
- public HTTP, arbitrary TCP, production stdio MCP, caller paths, `EditorPrefs` or environment-variable trust;
- Desktop writes to `Assets`, `Packages`, `ProjectSettings`, evidence or artifact roots;
- visual pass, user verdict, L3/L4 or Publication authority;
- any new Unity Editor main-window, five-tab, Player UI, visual-polish or embedded-MCP work.

## 6. Remaining independent blocker outside Phase 1

The full110 formal projection remains blocked on the exact immutable QA `model-version-id`. This report does not provide, infer or authorize one.
