# Patch authoring (v1)

Return a bare JSON array of operations only: no `{ "operations": ... }` wrapper, recipe object, comments, or Markdown fence. The caller supplies `expectedRevision` separately; it is not a Patch field. A successful apply increments the Recipe revision by exactly one and records history.

Allowed operations are `replace`, `add`, `remove`, `enable`, and `disable`. Each operation has exactly `op` and `path`; `replace` and `add` additionally require `value`. `remove`, `enable`, and `disable` must not have `value` or extra keys.

## Allowed stable paths

- `replace`: `/stages/{stageId}/modules/{moduleId}/parameters/{parameter}`
- `add` and `remove`: `/stages/{stageId}/modules/{moduleId}`
- `enable` and `disable` a module: `/stages/{stageId}/modules/{moduleId}`
- `enable` and `disable` a stage: `/stages/{stageId}`

Every path ID must start with a letter and then contain only letters, digits, `_`, or `-`. Do not use array indexes, RFC 6902 escaping, `~`, `..`, asset paths, or Unity property paths. `replace` only changes an existing declared parameter. Its new value must pass the live Manifest type and inclusive range.

`add` adds a complete module object and its `value.id` must exactly equal `{moduleId}` in the path. That module must satisfy the normal Recipe/Manifest/Catalog contract. `remove` may not remove any `energy_body` module in `travel`, even if its ID differs from the canonical `core`; this is a semantic safety rule. The canonical `travel/embers` secondary-particle module is removable. Enable/disable changes the target object's `enabled` flag.

Read only this document, the canonical revision-1 Recipe, and `canonical-patches.generated.md`; also read the generated parameter table only for an `add` or a parameter replacement. Do not read or recreate the complete Recipe Schema for a Patch-only task.

The generated examples are checked through the real Patch service against an isolated copy of the canonical Recipe. They are examples, not an instruction to reuse their values or IDs; take all live template facts from the generated table.

Examples (facts shown are only examples; consult the generated table before authoring a different template):

```json
[
  {
    "op": "replace",
    "path": "/stages/travel/modules/embers/parameters/rate",
    "value": 9
  }
]
```

```json
[
  {
    "op": "disable",
    "path": "/stages/travel/modules/embers"
  }
]
```
