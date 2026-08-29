# AI workflow for VFX Composer v1

This directory is the complete contract for an AI agent that writes a VFX Recipe or an incremental Patch. For a new Recipe, read these files in this order:

1. [Recipe authoring](recipe-authoring.md)
2. [Canonical generated Recipe](canonical-recipe.generated.json)
3. [Generated template parameter table](template-parameters.generated.md)
4. [Validator report guide](validation-reports.md)
5. [Patch authoring](patch-authoring.md), only for an existing Recipe change

For a **Patch-only** request, use the smaller reading order instead:

1. [Patch authoring](patch-authoring.md)
2. [Canonical generated Recipe](canonical-recipe.generated.json), as the revision-1 baseline
3. [Canonical generated Patch examples](canonical-patches.generated.md)
4. [Generated template parameter table](template-parameters.generated.md), only when adding a module or changing a parameter

Patch authors do not need `recipe-authoring.md` or `recipe-v1.schema.json`. The Patch API is deliberately narrower than Recipe authoring; do not reconstruct or resubmit the whole Recipe.

`recipe-v1.schema.json` is an AI-readable description of the currently supported Recipe shape. It does not replace the Unity Validator: parameter names, types, bounds, template-kind matching, uniqueness, attachment targets, catalog availability, and budget are checked against the live Catalog at validation/build time.

The canonical Recipe and canonical Patch examples are machine-generated from the formal default Recipe and the live Catalog. The parameter table is generated from the same resolved `TemplateCatalog` used by Validator and Build. Export them as one authoring bundle with `Tools/VFX Composer/AI Workflow/Export Formal Authoring Bundle`; no generated file may be hand-edited.

No MCP integration exists in v1. There is no S9 CLI/BatchMode entry point: the existing Editor Validator/Build workflow remains the supported route. This is intentional; the S9 evidence shows the report-feedback loop works without adding an unverified command surface.

> **Update (2026-08-29) — the v1 "no MCP / no CLI-BatchMode entry point" decision above is historical and has been explicitly overturned by a new milestone.** Batch CLI and MCP entry points are specified in `docs/requirements/REQ-002_BATCH_CLI_MCP.md` (under acceptance) and are scheduled as tasks F4/F5 in `docs/plans/OPTIMIZATION_MASTER_PLAN.md`. Two boundaries from the original decision are retained unchanged: MCP is stdio-only, and the W24S6 stub's DryRun/ReadOnly restrictions are not relaxed.
