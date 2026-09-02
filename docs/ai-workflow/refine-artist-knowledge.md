# Refine Artist Knowledge — 2D Fire Projectile Catalog

> **Source of truth.** This document is the only human-edited source of the refinement
> knowledge fragment. The machine-consumed export lives at
> `src/VFXComposer.AI.Providers/Recipes/Assets/refine-artist-knowledge.fragment.json`
> (embedded resource). After editing this document you must re-export the fragment:
> update its content to match, bump its `version`, refresh `exportedOn`, and refresh
> `sourceSha256` with the SHA-256 of this file's exact bytes. The consistency test in
> `VFXComposer.AI.Tests` fails until the export is refreshed.
>
> **Language.** Everything that reaches a prompt is English (REQ-004 §10.2). The
> Chinese alias column below is local matching data for the override guard only
> (REQ-004 §17.2 O-3 ruling); it never enters a prompt.
>
> **Scope.** Template catalog 1.0.0 — six templates, eleven editable parameters, all
> 2D fire projectile. Every action stays inside the inclusive `[min, max]` bounds of
> the committed catalog snapshot; an ask that goes past a bound stops at the bound.

## 1. Feedback translation table

One row per editable parameter. *Aliases (en)* and *Aliases (zh)* are the
deterministic naming lexicon the override guard matches against the user's feedback
(ordinal, case-insensitive; English aliases as word tokens, Chinese aliases as
substrings). *Direction* and *Magnitude* classify the translation; the *Guidance*
sentence is what the prompt renders (bounds are appended from the catalog snapshot
at render time, so they never go stale here).

| Parameter | Type, bounds | Direction | Magnitude | Aliases (en) | Aliases (zh) | Guidance |
|---|---|---|---|---|---|---|
| PFT_2D_FireCore.scale | float [0.6, 2.4] | bidirectional | moderate-step | core, fire core, fireball, flame body, scale | 火核, 火球, 主体, 核心, 火焰主体 | "Bigger, hotter, more massive fire" raises scale by a moderate step (about 0.2 to 0.4); "smaller, tighter, more compact" lowers it the same way; an extreme ask such as "as big as possible" goes to the bound and stops there. |
| PFT_2D_Embers.rate | float [4, 36] | bidirectional | moderate-step | embers, ember, sparks, spark, ember density, spark density, denser, sparser | 余烬, 火星, 余烬密度, 火星数量 | "More, denser embers or sparks" raises rate by a moderate step (about 4 to 8); "fewer, cleaner, sparser" lowers it; "as many as possible" means the upper bound, never past it. |
| PFT_2D_Embers.lifetime | float [0.25, 1.1] | bidirectional | small-step | ember lifetime, ember duration, linger, lingering | 余烬时长, 余烬持续, 火星存留 | "Embers linger, fade slower" raises lifetime by a small step (about 0.1 to 0.2); "fade quicker, snappier embers" lowers it; stop at the bound. |
| PFT_2D_FireTrail.time | float [0.08, 0.4] | bidirectional | small-step | trail, tail, streak, trail length, trail time, length | 拖尾, 尾迹, 尾巴, 拖尾长度, 轨迹长度 | "A longer trail or tail" raises time by a small step (about 0.05 to 0.1); "shorter, snappier trail" lowers it; "barely any trail" means the lower bound, never below it. |
| PFT_2D_FireTrail.width | float [0.12, 0.55] | bidirectional | small-step | trail, tail, streak, width, thickness, thick, thin, thicker, thinner, wider, narrower | 拖尾, 尾迹, 尾巴, 宽度, 粗细, 变细, 变粗 | "A thicker, bolder trail" raises width by a small step (about 0.05 to 0.1); "thinner, finer, more delicate" lowers it; stop at the bound. |
| PFT_2D_LaunchFlash.lifetime | float [0.06, 0.22] | bidirectional | small-step | launch flash, muzzle flash, flash, flash duration, flash lifetime | 起手闪光, 发射闪光, 枪口闪光, 闪光, 闪光时长 | "The launch flash reads longer" raises lifetime by a small step (about 0.02 to 0.05); "snappier, blink-and-miss" lowers it; keep it at or below the launch stage duration and stop at the bound. |
| PFT_2D_LaunchFlash.size | float [0.45, 1.8] | bidirectional | moderate-step | launch flash, muzzle flash, flash, flash size | 起手闪光, 发射闪光, 枪口闪光, 闪光, 闪光大小 | "A bigger, punchier launch flash" raises size by a moderate step (about 0.2 to 0.4); "subtler, smaller flash" lowers it; stop at the bound. |
| PFT_2D_FireImpact.count | integer [8, 40] | bidirectional | moderate-step | impact burst, burst, debris, fragments, impact particles, count | 爆裂, 碎片, 冲击粒子, 碎片数量, 爆裂数量 | "A denser, richer burst with more debris" raises count by a moderate integer step (about 4 to 8); "cleaner, fewer fragments" lowers it; count stays an integer and stops at the bound. |
| PFT_2D_FireImpact.speed | float [1.5, 6] | bidirectional | moderate-step | impact burst, burst, debris, fragments, speed, velocity, burst speed, debris speed | 爆裂, 碎片, 速度, 爆裂速度, 碎片速度 | "Debris flies faster, a more violent burst" raises speed by a moderate step (about 0.5 to 1); "softer, slower burst" lowers it; stop at the bound. |
| PFT_2D_Shockwave.endSize | float [1.2, 4] | bidirectional | moderate-step | shockwave, shock wave, ring, shockwave size, ring size, ring radius, radius | 冲击波, 冲击环, 冲击波大小, 冲击波半径, 半径 | "A wider shockwave ring" raises endSize by a moderate step (about 0.3 to 0.6); "a tighter ring" lowers it; stop at the bound. |
| PFT_2D_Shockwave.lifetime | float [0.12, 0.5] | bidirectional | small-step | shockwave, shock wave, ring, shockwave duration, ring duration, shockwave lifetime | 冲击波, 冲击环, 冲击波时长, 冲击波持续 | "The ring expands more slowly, reads longer" raises lifetime by a small step (about 0.05 to 0.1); "a quick sharp pulse" lowers it; stop at the bound. |

Aliasing convention: a word that names a module (for example "trail", "flash",
"shockwave") appears in the alias list of every parameter of that module, because
naming the module names all of its parameters; a word that names one aspect (for
example "thinner", "radius") appears only in that parameter's list. Deliberately
generic single words that would cross modules (for example bare "size" or
"lifetime") are not aliases: the guard's fail-safe direction is to restore the
user's hand-tuned value when naming is uncertain.

## 2. Catalog aesthetic conventions

- Three-beat timing: launch is a short accent (roughly 0.06 to 0.22 seconds), travel
  carries the body of the effect (roughly 0.6 to 1.5 seconds), impact resolves in
  roughly 0.2 to 0.5 seconds; keep the beats in that magnitude relation instead of
  letting one beat dominate.
- Keep PFT_2D_LaunchFlash.lifetime at or below the launch stage duration, so the
  flash never bleeds into the travel beat.
- PFT_2D_Embers.rate and PFT_2D_FireCore.scale couple perceptually: a larger core
  reads hollow without proportionally more embers, so when scale moves toward its
  upper half, move rate in the same direction moderately rather than leaving it at
  the default.
- Under the strict budget (at most two modules in the whole recipe), spend the first
  slot on the travel-stage fire core; give the second slot to the fire trail when the
  description stresses motion, or to the impact burst when it stresses the hit; drop
  the launch flash and the shockwave first, because launch and impact stay readable
  as empty beats.
- When feedback asks for "more impact" without naming a module and the module budget
  is already spent, retune the existing impact-stage module instead of restructuring
  the recipe.

## 3. Refinement discipline

- Change only the aspects the current feedback names; every other field keeps its
  previous value verbatim.
- Output the complete Recipe v1 JSON as exactly one JSON object, with no markdown
  fence and no explanation.
- Never change id, metadata.templateCatalogVersion, or recipeVersion.
- Keep the strict red lines: the three stage roots launch, travel, impact in that
  order; at most two modules in the whole recipe; never attachTo; every parameter
  inside its inclusive [min, max]. When feedback asks for a value past a bound, set
  the bound and stop.
