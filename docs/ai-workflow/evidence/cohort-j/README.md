# Cohort J preregistration — Patch-only, awaiting dispatch approval

J is the proposed S9 recovery batch. It contains exactly the three Patch-only questions `J1`–`J3`; its acceptance threshold is exactly 3/3. Cohort I remains frozen historical evidence: its Recipe result is 4/5, while its Patch result is only P2 successful (1/3). J does not reinterpret or repair I.

The original pre-dispatch J freeze was replaced before any dispatch after review found that its generated examples leaked the J answers. That replacement deleted only J payload/envelope/manifest/temp preparation files, then regenerated them with non-overlapping examples; it did not create an attempt, report, final, transport witness, Recipe/history, or Generated runtime artifact.

Before dispatch, `Freeze Cohort J Patch-only Payloads` exports the current canonical Recipe, Catalog table, and generated Patch examples; writes each full Patch-only payload exactly once both here and under `C:\Users\admin\AppData\Local\Temp\vfxcomposer-s9-cohort-j`; writes a short hash-bound envelope; and writes `initial-payloads.generated.json`. The payload intentionally excludes the full Recipe authoring guide and Recipe Schema.

Each isolated `gpt-5.6-terra` / `high` / `fork_turns:none` author may make exactly one `exec_command` call to read the named temp payload, verify its SHA-256, and return only a bare Patch JSON array. It may not use any other tool, workspace, directory, or network resource. If a repair is needed, it must continue in the same thread with a hash-bound payload containing the complete previous machine report.

No J model output, attempt, report, final, transport witness, Recipe/history, or Generated runtime evidence exists before dispatch approval. Local payload copies and hashes prove preparation only; the host cannot capture wire payloads or child tool traces, so transport records must disclose that limitation.
