# W24 focused Unity machine-gate evidence

This write-once directory archives the final successful focused EditMode runs used to verify the current W24 S1, S4, S5, transaction, typed-evidence, batch-entry, and S6 source state.

The authoritative index is `receipt.json`. Each XML and log is copied byte-for-byte from the isolated shadow run and is bound by SHA-256 in that receipt. Every listed final run exited naturally with code 0. The one skipped batch-entry test is intentionally interactive-Editor-only; its reason is preserved in the XML.

This package is machine-only evidence. It grants no Visual QA, L3, L4, commercial-use, or user-acceptance verdict.
