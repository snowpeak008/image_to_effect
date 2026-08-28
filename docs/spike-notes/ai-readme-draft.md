# Fireball Recipe draft (S2)

Return JSON only. This is an S2 draft aligned with `DEVELOPMENT_PLAN.md` §5 and §15: it keeps only the three templates actually covered by S1, but it uses the formal Recipe enumerations and semantic Patch paths. Do not invent or omit fields.

## Recipe structure

| Path | Type / allowed values | Meaning |
|---|---|---|
| `recipeVersion` | integer, exactly `1` | Recipe format version. |
| `id` | string, exactly `"fireball_2d"` | Stable machine identifier. |
| `name` | string | Human-readable Recipe name. |
| `dimension` | string, exactly `"2d"` | Formal visual-dimension enum. |
| `archetype` | string, exactly `"projectile"` | Formal effect-family enum. |
| `style` | string, exactly `"stylized"` | Formal style value for this spike. |
| `targetProfile` | string, exactly `"mobile_medium"` | Formal target profile for this spike. |
| `randomSeed` | integer `[0, 4294967295]` | Fixed particle seed. |
| `stages` | ordered array, exactly `travel` then `impact` | The two S1-covered fireball phases. |
| `metadata` | object with exactly `createdBy` and `templateCatalogVersion` | Provenance fields; values are strings. |

Each stage has exactly `id`, `trigger`, `duration`, `enabled`, and `modules`.

| Stage | `trigger` | `duration` | Required modules |
|---|---|---:|---|
| `travel` | `on_launch` | number `[0.1, 10]` | `core`, then `embers` |
| `impact` | `on_hit` | number `[0.1, 5]` | `burst` |

Each module has exactly `id`, `kind`, `templateId`, `parameters`, `enabled`; only `embers` also has `attachTo`, whose value must be `"core"`. `enabled` is boolean and must be `true` for all modules in this spike. Do not add `attachTo` to `core` or `burst`.

## Available templates and parameters

| Module id | `kind` / `templateId` | Parameters (type and inclusive range) | Purpose |
|---|---|---|
| `core` | `"energy_body"` / `"T_Core"` | `scale`: number `[0.5, 3]` | Main orange fireball sprite. |
| `embers` | `"secondary_particles"` / `"T_Embers"` | `rate`: number `[0, 100]`; `lifetime`: number `[0.1, 3]` | Continuous sparks during travel. |
| `burst` | `"impact_burst"` / `"T_Burst"` | `count`: integer `[1, 100]`; `speed`: number `[0.1, 10]` | Radial impact burst. |

Every module `parameters` object contains exactly the parameter names in its table row. Values are numbers unless `count` and `randomSeed`, which are integers.

## Reference recipe

See `sample-recipe.json`. Its standard values are core `scale: 1.2`, embers `rate: 18` and `lifetime: 0.55`, and burst `count: 24` and `speed: 3.5`.

## Patch response format

For a local change, return a JSON array of operations, not a full Recipe and not an object wrapper. Each operation has exactly `op`, `path`, and `value`. `op` is `"replace"`; `path` begins with `/stages/` and uses the stable stage ID and stable module ID rather than array indexes. The target path must already exist, and `value` must have the parameter's declared type and range. Include only requested changes.
