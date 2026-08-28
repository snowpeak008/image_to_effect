# S12 Gate F decision — 3D stylized arc slash

> Decision: **GO for an isolated render-order Spike and a separate Recipe v2 implementation path; NO-GO for disguising Slash as projectile v1.** Main-Agent decision date: 2026-08-22.

## Accepted production brief

The visual brief and target image in `S12_SLASH_BRIEF.md` are accepted: a character-scale, third-person, lower-left to upper-right stylized 3D slash lasting about 0.45 seconds, readable without Bloom, ground reflection, gameplay logic, a character, or a weapon dependency.

## Frozen decisions

1. Slash uses Recipe v2 with a finite `timeline.duration` and five sibling phases carrying explicit `startTime` and `duration`. The v2 runtime may schedule overlaps, but S12 does not introduce conditions, nesting, arbitrary events, or a generic graph.
2. Every required phase is non-empty and owns a real visual responsibility. S12 will prototype independent templates/modules for `anticipation`, `arc_sweep`, `arc_afterimage`, `slash_sparks`, and `slash_dissipation`. The proposal to hide afterimages as fixed children of `arc_sweep` is rejected because it would make the separately timed `afterimage` phase an empty semantic shell.
3. The player-safe host API is `PlaySlash(Vector3 position, Quaternion orientation)`, `StopEffect(bool immediate)`, and `ResetForPool()`. It has no hit, damage, socket, animation, weapon, camera, or collision input.
4. v1 and v2 Patch contracts are explicitly dispatched and isolated. They may share transactional primitives, bare-array operations, revision/history rules, and stable IDs, but v1 Patch must reject v2 Recipes and v2 Patch must reject v1 Recipes without writing.
5. S12 is post-MVP capability-validation work. The embedded package and v1 compiler remain at the accepted 0.1.0 baseline during the Spike. A later S12 acceptance may make a separate version decision; the Spike does not rewrite release documentation or formal fireball outputs.

## Protected v1 baseline

The following values were recorded before the Spike and must remain exact:

| Baseline | Value |
| --- | --- |
| `fireball-2d.default.json` SHA-256 | `53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88` |
| `fireball-3d.default.json` SHA-256 | `1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2` |
| 2D generated Prefab GUID | `edfdb8327c7bd234c94f0f4338c35816` |
| 3D generated Prefab GUID | `27d60143a7650dd4fb850abed3ca178b` |
| 2D output filename+SHA manifest hash (18 files) | `B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240` |
| 3D output filename+SHA manifest hash (20 files) | `4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD` |

The v1 Recipe bytes, generated output bytes, Prefab GUIDs, Preview references, parser/compiler behavior, AI v1 bundle, and normal S4–S11 regression tests are protected. Any difference is a stop condition, not an expected migration.

## Spike exit gate

The disposable Unity Spike must prove one curved primary ribbon, a separate subordinate afterimage ribbon, an inner yellow edge, and sparse detached sparks can be reconstructed with reviewed Unity 2022.3/URP assets. It must capture front, side, oblique-top, close, and game-distance views; remain readable without Bloom on dark, neutral, and bright backgrounds; show no flat-card popping or broken transparency ordering; record camera/material/mesh facts and hashes; and stay outside formal `Templates`, `Recipes`, and `Generated`.

Only a passed Spike authorizes formal Recipe v2, Runtime, Compiler, Patch, AI, or template implementation.
