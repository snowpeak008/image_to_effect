# Cohort B fixed prompts (written before dispatch)

All five isolated agents receive the unchanged formal documents `README.md`, `recipe-authoring.md`, `template-parameters.generated.md`, `recipe-v1.schema.json`, and `validation-reports.md` verbatim, plus exactly one prompt below. They must return raw JSON only and have no tools/workspace.

1. `b_aurora`: Create a 2D PC-preview stylized projectile with a bright quick launch, medium travel core, a slim trail, light embers, and a compact impact.
2. `b_boulder`: Create a 2D mobile-medium stylized projectile that feels heavy: a large core, slow one-second travel, broad trail, moderate embers, and a strong but legal impact.
3. `b_spark`: Create a 2D PC-preview stylized projectile with a tiny core and no trail, sparse embers, then a crisp brief burst and shockwave.
4. `b_ribbon`: Create a 2D mobile-medium stylized projectile whose main character is a long narrow trail behind a medium core, with no embers and a restrained impact.
5. `b_nova`: Create a 2D PC-preview stylized projectile with a modest launch, normal travel core/trail/embers, and an emphatic large impact burst and shockwave.

Patch B fixed prompts: P1 replace travel embers rate with a lower legal value; P2 disable travel embers without removing it; P3 add a second legal lightweight ember module attached to travel core. Each receives the unchanged Patch document and the same isolated baseline Recipe at revision 1.
