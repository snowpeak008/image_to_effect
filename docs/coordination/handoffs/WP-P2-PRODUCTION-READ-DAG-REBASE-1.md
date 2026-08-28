# WP-P2-PRODUCTION-READ-DAG-REBASE-1 — STOPPED

## Identity and objective

Package: `WP-P2-PRODUCTION-READ-DAG-REBASE-1`  
Scope: `PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY`  
Model / reasoning setting: Codex (GPT-5-based) agent; inherited task reasoning
setting, with no more-specific model identity asserted by this handoff.

Objective completed as a documentation-only architecture rebase: preserve D1_0 as
NO-GO, split fresh D1R dormant remediation from I1 privileged live-store integration,
insert pure Protocol C2 host-owned locator/locator-ACK before W1, and recompute the
future production-read DAG. No implementation is authorized or published.

## Time and starting identities

Start record: `2026-08-27T20:15:56Z` (earliest retained target-document write;
pre-write verification completed earlier, but no finer durable start timestamp was
created).  
End / final validation: `2026-08-27T20:23:29.4241628Z`.

Frozen control identities verified before the first permitted write:

```text
35ca3855bce8d49490dab9cef37496278797c2e58de94caaefa3355e27b9293a  docs/coordination/W24_PROGRAM_CONTROL.md
debcd8743e82e71b26a3aba52d3b546e6cb24beab9497476ab1b75ec0652b615  docs/coordination/W24_WORK_PACKAGE_REGISTRY.md
6cc0ac33f9315324d560c799e0291688dfe62a1901c331241bb0f1cea5fd5745  docs/coordination/W24_EVIDENCE_INDEX.md
```

Starting architecture identities:

```text
7849105a1d8038592b744816662fdf60b18d5760e462d3b71fabde51882afd75  docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md
737d968f05a6f8b9467ba16d78f62d4e4b442ff65d623bad4ab1f98447a16ef0  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md
7aca5fc37a1b48f3b207230fe3504b8eee161e0794a6a3db73789dd5e29da0ef  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md
```

## Exact authored manifest and receipts

The exact four-document edit set is the three SHA-bearing architecture documents
below plus this handoff. This handoff deliberately has **no self-hash**; placing its
hash in its own bytes would invalidate it. The SHA-bearing authored manifest is
`.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/03-authored-architecture-manifest.sha256`.

```text
155711c41a8905e46760e5a27942414e40e321ffd912d2dfb4ecdd817cbe4e5a  docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md
ea519fdb592f31ba4f529f507067ec48ae88de4acf8f47a5f63928d69f2ef91b  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md
9e72d693c704b8579e1897f2e213f715311d4c50244c60a6dbfb4b5af24d1d43  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md
INTENTIONALLY_NO_SELF_HASH  docs/coordination/handoffs/WP-P2-PRODUCTION-READ-DAG-REBASE-1.md
```

Durable generated receipts, all within the only allowed receipt root:

- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/01-starting-boundary.txt`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/02-preflight-and-accounting.txt`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/03-authored-architecture-manifest.sha256`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/04-final-validation.txt`

## Result, counts and reproducible checklist

The current future DAG is acyclic: 13 nodes, 8 edge groups and 17 directed edges.
Its exact edge groups are `D1R→I1→A1`, `D1R→P1`, `D1R→S1`, `I1→R1`, `C2→W1`,
`C1+C2+W1+A1+P1+S1+R1→B1`, `B1+C1→D2`, and `D2→E1→A2`. D1_0 is retained
STOPPED/NO-GO provenance rather than a GO node. Historical G0 `12/6/15/68/1`
remains provenance only.

Current accounting is 75 unique leaves, 12 governed sequential overlays and one
receipt-root exception. The 75 is historical 68 plus C2's seven unique new leaves;
the 12 overlays are nine C1→C2 Protocol/codec/test/verifier rows plus three retained
D1_0→D1R remediation rows. `UNAPPROVED_COLLISIONS=0` does not make a false global
zero-overlap claim: every listed overlay requires one active writer, its exact prior
hash, no concurrent overlap and a fresh independent audit.

C2's exact 16-path envelope is seven absent unique DTO/schema/test/vector leaves and
the nine listed C1 overlays. The existing `GoldenVectors/**/*.json` project inclusion
makes that envelope sufficient without a csproj or lock change. C2's locator has no
caller path and binds typed host-owned identities, session/process epoch and
broker/registration/enrollment generations; its `worker.project.locator.ack` is a
separate kind from handle-grant ACK. W1 remains its original nine Unity paths, after
C2 plus ADR-003, with no pipe, project read or session issuer.

D1_0's three current bytes remain NO-GO. D1R may only make the stated dormant/static,
fail-closed repairs. I1 alone provisions the pinned root with enabled
`SeSecurityPrivilege`, `ACCESS_SYSTEM_SECURITY` and target-directory rename rights,
then makes the first real strict commit/reopen/readback. B1 must consume I1's live
receipt, in addition to C1+C2+W1+A1+P1+S1+R1, before its sole final `Running` gate.

The remediation writer recorded a final-validation exit `0` for the stated semantic
checks. That record is not accepted here as a receipt PASS: the final independent
audit found that the failed verifier lacks literal command/times/exit/raw and that
receipt-root aggregates are unrepeatable. The independent auditor recomputed the
semantic gates; the resulting nonblocking P2 is recorded in the final closeout below.

## Constraints proved and not proved

Proved by the documentation/preflight checks:

- Only the four contract-allowed documentation paths were authored; generated files
  are confined to this package's receipt root.
- D1R/I1 separation, C2-before-W1, exact W1 nine-file scope, B1 predecessor set and
  governed overlays are explicit and acyclic.
- `W24FS001`/`23`, every-serving-pipe ACL/readback, Job lifecycle-only containment,
  ordinary-profile exclusions, loaded-image limitations and production/authority
  NO-GO remain preserved in the normative ADR and phase records.

Not proved (and not claimed): any C2/D1R/W1/I1/B1 implementation, C2 vector behavior,
D1 remediation correctness, privileged store success, serving pipe, installation,
Worker, enrollment, production read, ServiceHost `Running`, authority or independent
audit result. This writer also does not make a worktree-wide claim about unrelated
writes by other agents.

## Self-audit, blockers and next dependency

Historical writer self-audit reported no finding; it is not the required independent
audit and is superseded by the final independent verdict below. No warnings beyond the
intentionally conservative start-time record and the deliberate no-self-hash rule.

At this historical pre-final-audit checkpoint, the remaining blocker was an
independent read-only audit before any separate fresh package could decide whether to
implement C2, D1R, I1, W1 or later nodes. D1_0 remains NO-GO; no source or
implementation authorization is conveyed by this handoff.

## STOPPED

`WP-P2-PRODUCTION-READ-DAG-REBASE-1` is **STOPPED**. No continuation into C2, D1R,
W1 or any other package was performed or is authorized here.

## Remediation closeout — supersedes conflicting earlier handoff wording

This fresh docs-only remediation closes the initial independent-audit findings
`P0=0 / P1=2 / P2=1` in the allowed four-document scope. It began with the
real recorded boundary command at `2026-08-27T20:36:16.1530105Z` and its final
architecture manifest was recorded at `2026-08-27T20:43:26.7467672Z`, exit `0`.
The remediation was complete at that checkpoint; its fresh independent audit is
recorded as final below. It was not an implementation or package GO.

The three corrected findings are:

1. ADR-004's normative 13-node owner table now gives both P1 and S1 dependency
   `D1R`, never old `D1_0` or `D1`; its edge list remains `D1R→P1` and
   `D1R→S1`.
2. Only for W1's exact nine planned paths, ADR-003 §4's ownership sentence
   `Unity 侧只允许新增：` and its immediately following two-path listing are
   narrowly superseded as a composition-shim location exception. The frozen
   `Worker/Protocol/W24S6WorkerProtocolCodec` adapter and ADR-003 tests remain
   read-only dependencies; W1 may only compose their primitives with C2 schemas
   and canonical golden bytes, and may not fork DTO/hash/canonicalization/
   registry/kind/schema/codec semantics.
3. The four static receipts below were regenerated from this remediation's real
   command records. No timestamp, exit code or successful static-verifier result
   is invented; the newline-sensitive non-success verifier attempt is recorded
   in receipt `04` and is not promoted to a PASS.

New receipt identities (all and only under the authorized existing package root):

- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/01-starting-boundary.txt`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/02-preflight-and-accounting.txt`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/03-authored-architecture-manifest.sha256`
- `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/04-final-validation.txt`

Final authored-document manifest (this handoff has no self-hash):

```text
e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141  docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md
3d254384569658493cb7f0b9f5c414046861db08082d68ab6dd2dbc4c7255199  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md
ee7a5f935e76faf3e14d87a842eba8ff68138f8711061e60cfdca6161bd3c360  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md
INTENTIONALLY_NO_SELF_HASH  docs/coordination/handoffs/WP-P2-PRODUCTION-READ-DAG-REBASE-1.md
```

Recorded accounting remains `13/8/17` and `75/12/1`; C2 `7/7` and W1 `9/9`
planned leaves remain absent. The actual Release Broker smoke emitted exact
`W24FS001`, one output line, exit `23`, from
`2026-08-27T20:39:03.7640203Z` to `2026-08-27T20:39:03.9092912Z`.
Production and Phase 2 remain NO-GO; no source/schema/Unity/control file was
authored, and C2/D1R/W1 were not started.

The writer self-audit of the documented corrections was not a fresh independent
audit. At that checkpoint, the sole next dependency was a fresh read-only independent
audit, required to treat receipt `04`'s non-success verifier attempt honestly and not
infer implementation authorization.

## STOPPED — remediation complete, pre-final-audit checkpoint

No continuation into C2, D1R, W1, or any other package occurred or is authorized.

## Final independent verdict — status closeout

Final independent verdict: `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY`;
`P0=0 / P1=0 / P2=1`. This scoped GO closes only the documentation-only DAG rebase
and grants no implementation, runtime, production, project-I/O, lease, command or
authority capability.

Current authored hashes independently recorded for the three architecture documents:

```text
e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141  docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md
3d254384569658493cb7f0b9f5c414046861db08082d68ab6dd2dbc4c7255199  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md
ee7a5f935e76faf3e14d87a842eba8ff68138f8711061e60cfdca6161bd3c360  docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md
```

The external handoff pre-status SHA-256 is
`373ea70a286aeb61f594126227616114752a2d9c96238a05aeb1f9f67dd0ee9f`.
It is an independent-audit input from before this status update; this handoff has
intentionally **no self-hash**.

Recorded DAG/accounting remain `13/8/17` and `75/12/1`; C2 `7/7` and W1 `9/9`
planned leaves remain absent. The sole P2 is explicit and nonblocking: the failed
verifier lacks literal command/times/exit/raw, and receipt-root aggregates are
unrepeatable. The independent auditor recomputed the semantic gates. No receipt PASS
is claimed, no receipt is repaired or regenerated, and this final verdict does not
assert `P2=0`.

Scope validation is limited to `docs/coordination/W24_PROGRAM_CONTROL.md`,
`docs/coordination/W24_WORK_PACKAGE_REGISTRY.md`,
`docs/coordination/W24_EVIDENCE_INDEX.md`, and this handoff. Active package is
`NONE / NEXT NOT PUBLISHED`; C2/D1R/W1 are not published. D1 remains
`STOPPED / NO-GO`, C1 remains closed GO, and `W24FS001`/23, Phase 2 and authority
remain NO-GO. Quota remains `100%` with no quota-based scheduling.

## STOPPED — final

`WP-P2-PRODUCTION-READ-DAG-REBASE-1` is **STOPPED**. No continuation into C2, D1R,
W1 or any other package occurred or is authorized.
