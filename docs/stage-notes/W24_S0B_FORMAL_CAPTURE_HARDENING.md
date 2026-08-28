# W24 S0b formal capture hardening

Status: implementation hardening complete; no Unity execution was performed in this change.

The S0b sustained-flame formal test now freezes a canonical operator command before capture, binds its SHA-256 through `BeginFormal`, and captures only from recorder-issued `LateUpdate` tokens. It acknowledges every natural frame and retains the exact matrix `3 seeds × 11 frame indices`.

The command fixes two `stop` branches and one `interrupt` branch. Semantic telemetry records every observed frame and machine requirements derive from measured lifecycle state, exclusive exit carrier, cleanup, actual point-light limits, receiver A/B luminance, and structural budget readback. Visual QA and user requirements remain `pending:` and cannot be promoted by capture.

The S0b-specific public S5 finalizer preflight rejects a partial matrix, missing/altered operator-command binding, incomplete branches, failed machine evidence, a failure-status marker, or any substituted Visual QA/user pass. Beauty evidence is explicitly LDR ARGB32; the receiver diagnostic is a separate linear ARGB32 readback.

The capture-tool bundle, frozen contract hash, and implementation-trace contract hash were regenerated after these changes. This document is not evidence of visual acceptance, L3, or L4.
