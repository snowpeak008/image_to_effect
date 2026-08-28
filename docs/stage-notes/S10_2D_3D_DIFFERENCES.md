# S10 2D / 3D differences — fireball v1

## Shared without change

| Contract | 2D and 3D result |
|---|---|
| Recipe version and top-level semantic model | Both remain Recipe v1. `dimension`, `id`, and `name` are the only required top-level identity differences. |
| Lifecycle | `launch` / `travel` / `impact` IDs, triggers, durations and enabled flags are byte-for-byte equivalent. |
| Module semantics | The same module IDs and kinds remain: `launchFlash`, `core`, `trail`, `embers`, `coreFlash`, `burst`, `shockwave`. |
| Attachment graph | `travel/trail → core` and `travel/embers → core` are unchanged and remain same-stage attachments. |
| Patch identity and protection | Stable Stage/Module-ID paths, revision handling, and v1 `travel/core` `energy_body` removal protection apply to both dimensions. |
| Runtime contract | Generated Prefabs expose the same Launch/Travel/Impact controller and public playback API. |
| Build guarantees | Canonical hash, dependency hash, deep copy, material copy, atomic recovery and same-path GUID preservation are shared. |

## Intentionally not shared

| Area | 2D | 3D |
|---|---|---|
| Templates | Sprite / 2D particle / 2D Trail inputs under `Templates/2D`. | Protected Prefabs under `Templates/3D`: Sphere Mesh core with billboard flame, billboard embers, spatial TrailRenderer, camera-facing launch/impact, and mesh-particle Ring shockwave. |
| Material / ordering | 2D sorting layer/order from S5. | 3D Materials own opaque core queue plus explicit transparent queues: flame/trail 3090–3100, embers 3150, impact 3200, ring 3050. No 2D sorting values are reused as 3D depth policy. |
| Binding | `core.scale`, `embers.rate`, etc. | Separate allow-listed `3d.*` symbols and S10 handler registrations. Manifest data never supplies a Unity property path. |
| Preview | Orthographic single-life-cycle scene. | Perspective 3D Gold Sample, rendered with hidden-graphics-device batch `Camera.Render` in front, side, oblique-top, close and normal-game-distance views. |
| Bounds / limits | 2D sprite/particle expectations. | Mesh and particle template-local bounds; manifests record `bounds:template-local` and `camera:perspective-reviewed`. This documents a template limitation without adding a Recipe field. |

## Explicitly unsupported in v1

- A module cannot use a template whose Manifest dimension differs from its Recipe: validator/compiler return structured `E310` at that module's `templateId` path and the Dry Run is `blocked`.
- An undeclared or unregistered 3D binding is never ignored: compiler returns structured `E500` at the stable module parameter path and the Dry Run is `blocked`.
- No arbitrary 3D mesh path, Shader, render queue, camera distance, light, bounds override, physics collision or gameplay behaviour may be supplied in Recipe/Patch. Those facts remain in the reviewed template/Manifest boundary.
- Cross-stage `attachTo`, arbitrary property reflection and 2D Sorting-to-3D depth conversion remain unsupported; existing validation rejects invalid attachment structure rather than guessing.

The only Recipe differences in [`fireball-3d.default.json`](../../project/Assets/VFX/Recipes/fireball-3d.default.json) are the necessary `id`, `name`, `dimension` and per-module `templateId` substitutions. Parameter values deliberately remain the same v1 semantic values.
