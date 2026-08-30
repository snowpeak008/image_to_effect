# batches/ — batch manifest samples & schema

Fixed inputs for the F6 end-to-end batch flow (CLI `vfxc batch` / MCP `vfx_submit_batch`).

| File | Purpose |
|---|---|
| `batch-manifest.schema.json` | JSON Schema (2020-12) for `vfxcomposer.batch-manifest/1`. **Tooling aid only.** |
| `sample-batch.manifest.json` | A valid three-item prompt manifest used as the E2E batch flow's fixed input. |
| `sample-batch-recipe.manifest.json` | A recipe-kind manifest referencing the bundled strict recipe below. |
| `recipes/spark_projectile_2d.json` | A recipe meeting the F2 strict build budget, built for real on Unity 2022.3.62f3c1 (see `docs/stage-notes/F6_E2E_EVIDENCE.md`). |

## Authority

The **authoritative** validator is the hand-written `VFXComposer.Batch.Core.BatchManifestParser`
(REQ-002 §5.4), not this schema. The parser also enforces two rules JSON Schema cannot express:

- the **capability gate** (a prompt manifest needs a generation channel; a recipe manifest needs a
  build-capable host with a directory to resolve recipe references against), and
- the **recipe-reference probe** (a `kind: recipe` item's `recipePath` must resolve to a readable
  recipe JSON relative to the manifest's own directory).

`BatchesSampleManifestTests` (in `VFXComposer.Cli.Tests`) parses `sample-batch.manifest.json`
through that parser on every build, so the sample cannot silently drift out of validity.

## Recipe-kind sample

`sample-batch-recipe.manifest.json` references `recipes/spark_projectile_2d.json`, a recipe that
satisfies the F2 **strict structural budget** (one render-module stage, no `attachTo` chain, all
three stage roots present). It is not hand-asserted valid — it was built for real by a Unity
2022.3.62f3c1 batchmode restricted build, producing the full three-file write surface with zero
out-of-bounds writes (`docs/stage-notes/F6_E2E_EVIDENCE.md`). Its canonical SHA256 computed by the
.NET `RecipeCanonicalJson` twin matched the Unity side byte for byte.
