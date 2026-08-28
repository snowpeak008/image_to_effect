# ADR-005: User-mode Broker/Worker architecture

Status: `ACCEPTED FOR PHASE-2 PLANNING` on `2026-08-28`. This ADR is a docs-only architecture rebase. It grants no runtime, production-read, mutation, evidence, or authority capability.

Normative U0 architecture token: `USER_MODE_LOCAL_CREATIVE_TOOL_V1`.

Supersedes: the normative production-read route in `ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md`. ADR-004 and its dormant artifacts remain historical provenance only; they are not prerequisites for this route.

## 1. Decision

VFX Composer is a single-user, local, trusted authoring tool. Phase 2 will use ordinary user-mode processes owned by the current interactive Windows user:

- the actively launched Desktop or Unity host starts the ordinary-user Broker and Worker child processes;
- no Windows Service, SCM registration, `LocalSystem`, privileged installer, privileged enrollment, `SeSecurityPrivilege`, `SeRestorePrivilege`, strict-SACL live gate, loaded-image proof, or privileged production issuer is required;
- Broker/Worker communication uses local named pipes restricted to the current-user SID, a cryptographically random unguessable pipe name, a one-use nonce, a monotonically changing generation, and exact parent/child process-handle plus process-epoch correlation;
- the user explicitly selects a project, after which the system carries a restricted project locator. The locator identifies only that selected project root and never authorizes arbitrary caller paths;
- hashes and signatures establish release/update integrity only. They do not turn a local process, project, transport event, machine result, visual result, or user verdict into authority.

Unity remains the owner of Unity API and project-content operations. Desktop remains free of direct Unity project I/O. Broker owns session admission, process correlation, the selected-project binding, and routing; Worker performs the bounded Unity-side read.

## 2. Threat model

### Trusted

The product trusts:

1. the current logged-in user;
2. the Desktop, Broker, Worker, or Unity host that user deliberately starts;
3. the explicit project selection made by that user;
4. locally installed release bytes to the extent established by ordinary distribution integrity checks.

### Defended

The product must fail closed against:

- a different Windows user attempting to connect to the pipe;
- stale, replayed, cross-generation, or cross-session messages;
- a wrong project or a locator escaping the explicitly selected root;
- protocol/version/schema drift and unknown or malformed messages;
- PID reuse, dead-child reuse, or a process whose handle/epoch does not match the admitted child;
- an unexpected executable path relative to the launched release layout;
- descriptor, handle, nonce, pipe-name, or path leakage through logs and diagnostics;
- Broker/Worker crash, disconnect, partial startup, or orphaned-child conditions.

### Not defended

This product does not attempt to defend against malicious code already running as the same user, a local administrator, kernel compromise, offline disk tampering, debugger/injection by the same user, or a deliberately malicious selected project. Those are outside the product threat model. Claims that require resistance to those actors are forbidden.

## 3. Process and session invariants

1. Parentage is explicit. The launching Desktop or Unity host retains the child process handle; Broker retains the Worker child handle where Broker performs the launch. PID text alone is never identity.
2. Admission binds current-user SID, parent/child relationship, process handle, creation-time epoch, random pipe name, one-use nonce, protocol version, role, and generation.
3. A nonce is consumed exactly once. A reconnect, restart, crash, or generation advance requires a new pipe name and nonce.
4. Every message is rejected after generation/session revocation. Unknown fields, unknown message kinds, capability drift, or locator drift reject before project I/O.
5. Pipes are local-only and current-user-SID-only. There is no public HTTP, arbitrary TCP, shared machine-wide endpoint, or environment-variable trust root.
6. Child processes are terminated or observed to exit on parent shutdown. Existing lifecycle-containment code may be reused only after it is shown to work for an ordinary-user child topology; no sandbox or hostile-same-user security claim follows.
7. Diagnostics use stable codes and must not disclose the random pipe name, nonce, raw handles, or unrestricted project path.

## 4. Project selection and locator boundary

The project enters the system only through an explicit user selection in Desktop or the active Unity host. Selection must resolve to one bounded project root and verify the expected Unity project markers. The resulting restricted locator:

- is scoped to that selected root and a single session/generation;
- cannot name a sibling, parent, UNC/device path, alternate data stream, or arbitrary later caller path;
- is checked again by the receiving Worker before read operations;
- carries the frozen C1/C2 identities needed for project and Worker-locator correlation;
- is revoked on project change, disconnect, parent death, Worker restart, or generation change.

The user’s choice is the source of local project intent. It is not a machine-wide enrollment, privileged registration, or authority grant.

## 5. Reuse and retirement ledger

Accepted C1 project-selection and C2 Worker-locator contracts are reuse inputs. Ordinary-user-compatible fragments from the historical P1 pipe work and S1 lifecycle/ownership design may also be reused after package-local review. The stopped M1 writer's uncommitted 12-file candidate is a U1 review input only: U0 does not merge, accept, or rebind it. The stopped M2 branch at `fa8843be` is historical only and does not enter the delivery line. Reuse means copying or adapting narrowly verified behavior; it does not import another writer's provenance or the old package's service, SACL, privileged-root, issuer, enrollment, or `Running` claims.

The following route is historical and is not a dependency of any U-node: D1/D1R durable privileged profile, ServiceHost, SCM/install policy, I1 installer, R1 host-owned enrollment, A1 service attestation, B1 service convergence, and their installed-service E2E/final-audit chain. Their dormant evidence remains available for provenance and regression comparison only.

## 6. Seven-node delivery DAG

The complete current Phase-2 route contains exactly seven nodes. Its exact dependency edges are:

`U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`.

| Node | Scope | Exit condition |
|---|---|---|
| U0 | This docs-only user-mode architecture rebase. | Seven owned docs agree; docs gate passes; one commit; no handoff micro-package and no source change. |
| U1 | Combined C3 + W1 actual Unity Worker connector: bind accepted C1/C2 contracts to the one canonical Unity adapter and implement the ordinary-user Worker connector. | Exact codec/vector/schema parity plus connector lifecycle negatives; no second wire format, Desktop integration, or project-read claim. |
| U2 | User-mode Broker/Worker child-process, pipe, nonce/session, and cleanup runtime. | Random-name/current-user-SID pipe, one-use nonce, generation, handle/epoch parent-child checks, cleanup/crash/replay negatives. |
| U3 | User project selection and read containment. | Explicit selection, restricted locator, Worker-only bounded read, wrong-project/path and revoke/reselect/restart tests. |
| U4 | Desktop integration. | Desktop launches/connects through the U2 route, consumes U3 selection/read without direct project I/O, and exposes fail-closed recovery state. |
| U5 | Local ordinary-user end-to-end. | Desktop/Unity-host launch through Broker/Worker read, wrong-user/session/project/protocol/path/crash negatives, clean teardown. |
| U6 | Independent final frozen-byte and evidence audit. | P0/P1/P2=0 for the declared user-mode scope; no source edits and no authority promotion. |

U1 and U2 may run in parallel only when their exact file ownership is disjoint; both consume U0, and U3 cannot start until both are frozen and audited. This concurrency does not add a node, does not split C3 from W1, and does not let U2 define Unity wire semantics.

## 7. Frozen estimate

The estimate is frozen when U0 passes its docs gate and must not be retroactively changed to make later progress look better:

- **Completion algorithm:** score the seven-node user-mode route with frozen planning weights `U0=8`, `U1=22`, `U2=22`, `U3=18`, `U4=12`, `U5=12`, `U6=6` (total `100`). At U0 closeout, count U0 plus independently accepted C1/C2 reuse and only the reviewable ordinary-user portions of P1/S1 as earned-value credit; do not count the stopped M1 candidate or M2 branch. The formal U0 baseline is `45/100`, reported as the uncertainty band **`42%-48%`**.
- **Remaining-effort comparison:** normalize the unimplemented privileged ADR-004 route at this rebase to `100` remaining units and the U1-U6 user-mode route to `50` remaining units. The planning point is therefore `50%` shorter, reported honestly as approximately **`45%-55%` shorter** because Unity integration and local E2E cost remain uncertain.
- Passing tests, lines of code, receipt count, or closing a docs node does not independently change either estimate. Any future re-estimate must publish actual elapsed/package data, use the same unit definitions, and explain variance instead of rewriting this baseline.

## 8. Consequences and gates

- U0 is an architecture decision only. Current production connection and project read remain NO-GO until U5 passes and U6 accepts the exact evidence.
- User-mode does not mean unauthenticated: cross-user isolation, freshness, exact process correlation, project restriction, protocol strictness, cleanup, and redaction remain required.
- C1/C2 and selected P1/S1 fragments reduce work but do not skip U1-U6 acceptance.
- The stopped M1/M2 work grants no acceptance credit. The old privileged nodes are closed history: no further implementation or audit may be scheduled for them, and they are not blockers for any U-node.
- Machine evidence, visual evidence, user verdict, L3, L4, and command/mutation authority remain separate later gates.
- Reintroducing a Windows Service, SCM mutation, privileged token, strict-SACL live requirement, loaded-image proof, or privileged enrollment requires a new ADR and explicit user authorization.
