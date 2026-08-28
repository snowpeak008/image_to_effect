# Error and forbidden-substitution catalog

Every matching item must appear in `forbiddenSubstitutions` and in the frozen known-cheat corpus.

| ID | Forbidden substitution | Authority failure | Cross-evidence alarm |
|---|---|---|---|
| `whole-image-rotation-fake-fragments` | One card or parent rotation presented as independent fragments | telemetry finds shared transform / no independent instances | fragment-ID trajectories show one rigid motion |
| `static-fake-trail` | Static line/card presented as motion trail | telemetry lacks emitter history or motion-derived vertices | trail mask does not cover projected emitter path |
| `additive-fake-light` | Additive brightness presented as real scene illumination | no Light/Light2D or no light state telemetry | receiver-only linear luminance A/B has no delta |
| `hard-state-picture-swap` | Unrelated image switch presented as causal stages | semantic timeline lacks preserved object/value | transition masks show undeclared discontinuity |
| `full-image-sustained-loop` | A whole image loop presented as sustained fire | no independent core/outer/smoke/ember carriers | periodic seam / layer-ID stability alarm |
| `undeclared-residual` | Permanent residual outside cleanup contract | cleanup telemetry remains alive past deadline | residual mask exceeds exempt layer area |

This catalog is a growing regression set, not a claim that it detects unknown cheats.
