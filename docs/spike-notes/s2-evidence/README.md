# S2 evidence index (revision 2)

All four rounds were regenerated after the acceptance correction by separate isolated `gpt-5.6-terra` / `high` subagents. Each was told not to inspect the workspace or use tools and received only the contract plus its own task input; no expected output was supplied.

The S2 executor acted as Validator. A full Recipe is legal only if it has the exact draft fields and formal enums aligned with `DEVELOPMENT_PLAN.md` §5, the required ID-based stage/module topology, and values with the declared types and ranges. Validator paths use stable semantic IDs (`/stages/{stageId}/modules/{moduleId}/...`), never array positions. A repair may change only Validator-reported paths. A Patch is a bare JSON operation array using those same semantic paths; it must not include a Recipe or an operation wrapper.

Every round document preserves its raw prompt, first output, Validator feedback, correction output (if any), correction count, per-field verification, and final determination. `无` means no post-output feedback or correction was needed.
