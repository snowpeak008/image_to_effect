# S12C2 report — validated AI Recipe runtime evidence

Status: Main-Agent independent acceptance passed. The immutable frozen S12 v3 preregistration contract remains unchanged (`runtimeEvidence: 0`) and did not dispatch an agent. After that freeze, the Main Agent dispatched one isolated `gpt-5.6-terra` / `high` Recipe agent at `/root/s12_ai_recipe` using the frozen payload; no Patch AI was dispatched.

`docs/ai-workflow/s12-slash-v3/runtime/recipe.attempt0.json` was processed through the real v2 parser, validator, budget calculator, DryRun, and Build. The machine-read frozen acceptance rules require all three values to be above their Manifest defaults. The recorded AI Recipe values are width **0.30**, spark count **18**, and afterimage alpha **0.40**.

The write-once [runtime report](s12c2-evidence/runtime-report.json) records `succeeded: true`, the source attempt’s raw-byte SHA-256, canonical recipe SHA, build hash, generated prefab GUID, actual values, empty real ValidationReport error arrays, local snapshot GUIDs, and required agent metadata: `gpt-5.6-terra` / `high` / `fork none` / `/root/s12_ai_recipe`.

Before restoring the formal generated output, the AI-built prefab and all four rendered material dependencies were deep-copied and rewired under `Assets/VFX/Preview/S12_AI_ValidatedSlash/`. The local snapshot is checked for local-only material paths/GUIDs, no missing scripts, width binding scale, the 18-count Spark burst maximum, and serialized alpha 0.40. Its retained PlayMode scene is `S12_AI_ValidatedSlashPreview.unity`.

The generated AI capture is isolated on Unity layer 31; the evidence camera culls only that layer, so it cannot capture the Gold Sample host. [Metadata](s12c2-evidence/metadata.json) hashes all four true generated frames and records natural simulated particle counts/distinct positions. The verifier requires `primary > afterimage > dissipation > 0` warm-pixel coverage and `complete == 0`; it never calls `ParticleSystem.Emit` or writes particle positions.

The compiler then rebuilt `Assets/VFX/Generated/slash_3d_stylized` from the unchanged canonical Recipe. Its final manifest is back to canonical recipe SHA `1bf6e95698ae1ef8f76d79d774a094fe70858d194b632e0a190055caa80526b8`.

Verification:

- `cmd /c tools\compile-check.bat`: exit 0.
- Targeted EditMode `VFXComposer.Tests.EditMode.S12C2AiRecipeEvidenceTests`: 2/2 passed.
- Targeted PlayMode `VFXComposer.Tests.PlayMode.S12C2AiRecipeRuntimeTests`: 1/1 passed.

Main-Agent independent verification also passed: compile exit 0, [`s12-main-c2-edit.xml`](../../test-results/s12-main-c2-edit.xml) 2/2, and [`s12-main-c2-play.xml`](../../test-results/s12-main-c2-play.xml) 1/1, with visual evidence and residue checks accepted.

Two pre-acceptance capture attempts were rejected and left no final evidence: the first exposed an additive untitled-scene authoring issue; the second used `-nographics` and hit Unity URP’s native particle-render `Camera.Render` crash. The completed capture was made with a hidden graphics window. A subsequent visual audit found that the initial non-isolated batch included the Gold Sample host; that batch was explicitly discarded before the final layer-isolated write-once recording.
