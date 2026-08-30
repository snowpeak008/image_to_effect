# batches/ — batch manifest samples & schema

Fixed inputs for the F6 end-to-end batch flow (CLI `vfxc batch` / MCP `vfx_submit_batch`).

| File | Purpose |
|---|---|
| `batch-manifest.schema.json` | JSON Schema (2020-12) for `vfxcomposer.batch-manifest/1`. **Tooling aid only.** |
| `sample-batch.manifest.json` | A valid three-item prompt manifest used as the E2E batch flow's fixed input. |

## Authority

The **authoritative** validator is the hand-written `VFXComposer.Batch.Core.BatchManifestParser`
(REQ-002 §5.4), not this schema. The parser also enforces two rules JSON Schema cannot express:

- the **capability gate** (a prompt manifest needs a generation channel; a recipe manifest needs a
  build-capable host with a directory to resolve recipe references against), and
- the **recipe-reference probe** (a `kind: recipe` item's `recipePath` must resolve to a readable
  recipe JSON relative to the manifest's own directory).

`BatchesSampleManifestTests` (in `VFXComposer.Cli.Tests`) parses `sample-batch.manifest.json`
through that parser on every build, so the sample cannot silently drift out of validity.

## Recipe-kind sample (deferred)

The sample here is prompt-based, which exercises the generation-then-build path end to end. A
`kind: recipe` sample whose recipe satisfies the F2 **strict structural budget** (≤2 render-module
stages, no `attachTo` chain, all three stage roots present) is authored alongside the F6 E2E harness
that actually builds it, so the recipe is validated by a real restricted build rather than by hand.
