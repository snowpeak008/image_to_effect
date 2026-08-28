# S9 isolated AI evidence

Every round used a newly spawned `gpt-5.6-terra` agent at `high` reasoning with `fork_turns:none`, no workspace/tool access, and only the formal AI workflow documents/parameter table contract injected into its prompt. `R1` asked for a quick cinder projectile, `R2` a large slow orb, `R3` an impact-heavy projectile, `R4` an emberless arc, and `R5` a steady comet. The three Patch prompts asked independently for embers rate reduction, ember disable, and a second lighter ember module. The `*.initial-output.json` and `*.final-output.json` files are unedited agent responses; report files are machine-written by `S9AiWorkflowEvidenceTests` while invoking the real Validator/Build/Patch APIs.

R2 received its raw `E601 /build` report twice and corrected on repair 2. R3/R4 received raw `E308` reports for each unknown `templateId` twice but did not preserve the required `PFT_2D_` tokens; their final outputs remain as failure evidence. No human edited an AI JSON or Patch.
