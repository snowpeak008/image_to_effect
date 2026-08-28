# Recipe authoring (v1)

Return one JSON object only: no Markdown fence, explanation, comments, trailing commas, or Patch array. Validate it in the VFX Composer Compiler window, then Build only when the report has no errors.

Start by copying `canonical-recipe.generated.json`, then change only the Recipe `id`, optional `name`, `randomSeed`, declared parameter values, and optional modules. Keep the ordinary travel module IDs `core`, `trail`, and `embers`; if a module is omitted, remove it as a complete module rather than inventing a replacement schema. Take every `templateId`, parameter name, parameter type, and parameter min/default/max from `template-parameters.generated.md`. Every manifest-declared parameter must be present exactly once in the module's `parameters` object. Do not create helper fields such as `color`, `intensity`, `position`, `notes`, `durationSeconds`, or per-parameter metadata: unknown fields are errors.

## Current top-level contract

Required fields are `recipeVersion`, `id`, `dimension`, `archetype`, `targetProfile`, `randomSeed`, `stages`, and `metadata`. `name`, `style`, and `revision` are optional.

- `recipeVersion` is the integer `1`.
- `revision` is an integer at least `1`. It is optional for backward compatibility: omitted v1 JSON is parsed as revision `1`. For a newly authored file, write `"revision": 1` explicitly. Patch application increments it; never increment it manually to bypass an expected-revision failure.
- `dimension` is `2d` or `3d`; the shipped Catalog currently exposes only 2D templates.
- `archetype` is only `projectile`; `style`, if present, is only `stylized`.
- `targetProfile` is `mobile_medium` or `pc_editor`. Budget reports are static preflight, not a device-performance certification.
- `randomSeed` is an unsigned 32-bit integer (`0` through `4294967295`).
- `metadata` contains exactly `createdBy` and `templateCatalogVersion`, both strings.

## Stages and modules

A stage contains exactly `id`, `trigger`, `duration`, `enabled`, and `modules`. Allowed triggers are `manual`, `after_previous`, `on_launch`, `on_hit`, and `on_end`. Duration is a finite non-negative JSON number. A module contains exactly `id`, `kind`, `templateId`, `parameters`, `enabled`, plus optional `attachTo`.

For a generated v1 runtime effect, provide the three stage IDs `launch`, `travel`, and `impact`: the Runtime controller has fixed roots for those IDs. Use `on_launch` for `launch`, `after_previous` for `travel`, and `on_hit` for `impact`. The Recipe Validator allows other stage IDs structurally, but Build reports `E601` when the Runtime controller cannot wire its fixed Launch/Travel/Impact roots; therefore they are not buildable v1 runtime recipes.

Recipe stage and module IDs must be nonempty and globally unique under the current Validator. Use simple IDs such as `launch`, `travel`, `impact`, `core`, and `embers`. This is important: Recipe validation does not currently impose an ID character pattern, but the Patch API accepts only IDs beginning with a letter and then letters, digits, `_`, or `-`. Use that Patch-safe subset whenever the Recipe may later receive a Patch.

`attachTo`, when supplied, names another existing module ID in the **same stage**. It is not a hierarchy path. Cross-stage attachment, self-attachment, and attachment cycles are rejected because the Runtime controller moves the Travel root independently.

The C# Validator is strict about all object fields (`additionalProperties: false` in the Schema). The single intentional exception is `parameters`: its allowed keys are selected dynamically by the module's live Manifest, so the static Schema cannot enumerate them. The generated table and Validator together make that object strict in practice.

## Do not use a generic particle-system schema

This Recipe format is not a generic particle-system interchange format. In particular, do **not** use `kind: "particle"`, `semantic`, `template`, `attachment`, or nested attachment objects. Do not invent generic particle fields such as `count`, `duration`, `loop`, `position`, `rotation`, `startColor`, `endColor`, `startSize`, `endSize`, `speed` ranges, `direction`, `spread`, `gravity`, `drag`, `blendMode`, `sortingLayer`, or `sortingOrder` unless the generated parameter table declares that exact parameter name for the selected `templateId`. Use only the exact `kind`, `templateId`, optional string `attachTo`, and parameter keys listed by the generated table. These fields are deliberately rejected rather than translated.

The canonical example replaces a hand-maintained “minimal JSON”: it is regenerated from the formal default and checked by real Validator, Dry Run, and Build tests. Do not replace it with a generic particle-system sample.
