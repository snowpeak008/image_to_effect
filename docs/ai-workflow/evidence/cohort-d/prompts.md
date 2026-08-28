# Exact pre-dispatch inputs

Common source input is the committed `docs/ai-workflow/README.md`, `recipe-authoring.md`, `template-parameters.generated.md`, `recipe-v1.schema.json`, `validation-reports.md`, and for Patches `patch-authoring.md`, all at workspace state immediately before D dispatch. Return format instruction for Recipe: “Return exactly one raw JSON Recipe object and nothing else.” Patch: “Return exactly one raw JSON operation array and nothing else.” Agents receive no acceptance spec, tools, workspace, prior outputs, or expected answer.

## D1

Create a 2D PC-editor stylized projectile named `d1_quick_comet`: a quick, compact fire comet with a small travel core, a slim short trail, sparse embers, and a restrained compact impact.

## D2

Create a 2D mobile-medium stylized projectile named `d2_heavy_bolt`: a heavy slow bolt with a large travel core, broad trail, many embers, and a strong impact burst.

## D3

Create a 2D PC-editor stylized projectile named `d3_tiny_spark`: a quick tiny spark with a small travel core, no trail, and a restrained compact burst and shockwave impact.

## D4

Create a 2D mobile-medium stylized projectile named `d4_narrow_ribbon`: a normal-speed ribbon with a medium travel core, a narrow trail, no embers, and a restrained compact impact.

## D5

Create a 2D PC-editor stylized projectile named `d5_nova`: a normal fire projectile with a medium-or-larger travel core, broad trail, normal-or-more embers, and an emphatic large impact burst and shockwave.

## P1

Using the supplied isolated baseline Recipe at expected revision 1, reduce the travel embers rate by half. Return exactly one raw JSON operation array and nothing else.

## P2

Using the supplied isolated baseline Recipe at expected revision 1, disable the travel embers module without removing it. Return exactly one raw JSON operation array and nothing else.

## P3

Using the supplied isolated baseline Recipe at expected revision 1, add a second lightweight ember module to the travel core. Return exactly one raw JSON operation array and nothing else.
