# S12 VFX brief — 3D stylized arc slash

> Gate F design input only. This is not a Recipe, template Manifest, generated asset, or implementation authorization.

## Intent and scope

Create a readable third-person melee **presentation VFX**: a light-cartoon orange-red arc sweeps from lower-left to upper-right across the actor's forward-facing action plane. It should read as one confident horizontal/diagonal sword swing at character scale, with a bright yellow cutting edge, a short delayed afterimage, separated sparks, and a clean fade. The target duration is **0.45 s**.

The effect is deliberately visual only. It does not own damage, collision, hit confirmation, weapon sockets, character animation, targeting, camera shake, sound, or gameplay timing. A host game may place and orient the effect after deciding those matters.

The default is appropriate for the existing 3D perspective preview, not the older orthographic 2D presentation. No change to the supplied default is necessary. The only conditional product decision is whether a future game needs a weapon-socket offset; S12 should not add that dependency.

## Approved visual target reference

[`slash-visual-target-v1.png`](reference/slash-visual-target-v1.png) is the approved **visual target reference**, SHA-256 `D5ED31F1F7C8A1C37DBE828980188CC2EEB58515953915A8727C2FE25B4362A5`. It is not a Unity production asset, texture, flipbook, screenshot substitute, or a source image to place wholesale on a card.

Read it as a layered visual specification: the lower-left origin leads into one strong upward/right primary arc; its yellow-white inner blade is held inside a broad orange main layer; darker red outer arcs form delayed residuals; and sparse diamond-shaped yellow-orange sparks detach from the path. Its four visible reduction steps motivate the primary/afterimage/sparks/dissipation story below. Unity must rebuild those readings with reviewed mesh/ribbon and ParticleSystem templates. In particular, the implementation must remain readable with **Bloom disabled** and must not depend on the reference image's dramatic ground reflection, floor glow, or any screen-space reflection.

## Camera, scale, and silhouette

- Camera: third-person perspective, nominal 55–65 degree vertical FOV, reviewed at normal gameplay distance and a close readability distance.
- Space: world-space action plane approximately facing the camera, with a small yaw bias so the ribbon has depth rather than becoming a flat HUD mark.
- Character scale: authored around a 1.8 m humanoid. Default outer arc radius is 0.95–1.25 m; total diagonal footprint is 1.7–2.2 m. It must not eclipse a full torso for more than the primary 0.16 s.
- Direction: visual motion begins at lower-left and finishes upper-right in the selected preview camera view. The runtime-facing orientation is supplied by the caller; Recipe data must not contain a camera transform.
- Silhouette: one thick crescent/ribbon is the primary read. The inner yellow edge is a thin, discontinuity-free blade cue, never a second competing crescent. Afterimages are shorter, thinner, darker, and delayed; sparks detach from the arc instead of forming a smoke cloud.

## Color and rendering language

| Layer | Default appearance | Guardrail |
| --- | --- | --- |
| Primary arc | orange-red body, warm additive/translucent material | carries the largest area and remains readable without Bloom |
| Blade edge | saturated yellow to yellow-white inner rim | 15–28% of primary ribbon width; should not white-out the whole arc |
| Afterimage | darker red-orange outer residuals, lower alpha | maximum two short echoes; never brighter than the primary arc |
| Sparks | yellow-orange diamond-like cores moving apart | few, separated particles with rapid alpha/size falloff |
| Dissipation | dim orange motes and residual alpha fade | no opaque smoke, screen distortion, or lingering loop |

URP Bloom and ground/screen-space reflection are not visual dependencies. Use reviewed URP materials and renderer ordering local to the templates; do not expose shader, blend, render queue, or arbitrary colors in Recipe input.

## Time story

The planned v2 timeline uses overlapping timed phases rather than pretending the existing projectile lifecycle is a slash.

| Phase / stable ID | Time (s) | Visual responsibility | Success condition |
| --- | ---: | --- | --- |
| `anticipation` | 0.00–0.04 | faint lower-left lead-in and small compressed edge glint | visible only as preparation; no stationary full arc |
| `primary_arc` | 0.04–0.20 | full orange-red arc grows/sweeps lower-left to upper-right; yellow blade edge is sharpest at 0.10–0.16 | unmistakable direction and one dominant crescent |
| `afterimage` | 0.12–0.30 | one or two shorter delayed arcs, fading behind the primary path | supports speed without making a solid fan |
| `sparks` | 0.14–0.36 | sparse, separated sparks leave the upper/right side of the completed sweep | adds impact energy without implying a gameplay hit event |
| `dissipation` | 0.20–0.45 | primary body is gone; residual alpha and a few motes shrink/fade | no live visible particles or trail after 0.45 s |

The overlap is intentional: afterimage, sparks, and fade begin before the primary phase has wholly ended. It is the main reason a sequential Launch/Travel/Impact controller is not a valid substitute.

## Proposed visual modules and authoring responsibility

| Module | Proposed responsibility | Authoring notes |
| --- | --- | --- |
| `arc_sweep` | primary curved mesh/ribbon with orange-red body and yellow inner edge | one reviewed 3D template; color relationship and mesh topology remain template-owned |
| `arc_afterimage` | delayed, shortened secondary ribbons | one template may contain up to two echoes with fixed offset relationship |
| `slash_sparks` | sparse detached yellow-orange particles | world/local simulation choice is template-owned and documented; no collision module |
| `slash_dissipation` | short residual fade/motes | must end inside the effect lifetime and never loop |

The first implementation should use pre-authored mesh/ribbon and ParticleSystem templates, not VFX Graph, procedural arbitrary curves, runtime mesh generation, or an editable weapon trail.

## Parameter envelope for the first formal template set

Values below are authoring targets for the future Manifest tests, not accepted Recipe fields yet. All values need min/default/max visible/stable verification before implementation is accepted.

| Template parameter | Min | Default | Max | Meaning |
| --- | ---: | ---: | ---: | --- |
| primary arc scale | 0.80 | 1.00 | 1.30 | whole character-scale footprint |
| primary ribbon width | 0.16 m | 0.24 m | 0.34 m | outside body width; blade rim stays proportional |
| primary sweep duration | 0.12 s | 0.16 s | 0.22 s | phase-local only; total remains <= 0.55 s |
| afterimage count | 1 | 2 | 2 | integer; fixed delays, no unbounded echo fan |
| afterimage alpha | 0.18 | 0.32 | 0.45 | cannot exceed primary opacity |
| spark count | 8 | 14 | 24 | separated burst particles |
| spark speed | 1.2 m/s | 2.2 m/s | 3.6 m/s | motion away from completed arc |
| spark lifetime | 0.10 s | 0.18 s | 0.28 s | must fit the 0.45 s effect envelope |
| dissipation lifetime | 0.12 s | 0.20 s | 0.28 s | zero visible residue at total end |

## Static budget target

Budget is a preflight estimate, not device certification. Target a single slash at default values: at most 4 ParticleSystems, 5 materials, 0 TrailRenderers, <= 48 estimated peak particles, <= 7 transparent renderers, no dynamic lights, distortion, collision, sub-emitter chains, or texture larger than 1024 px. The future v2 calculator must still evaluate both `mobile_medium` and `pc_editor`; any resulting warning is reported, never hidden.

## Explicit Unsupported for S12 v1 of slash

- Damage, collision, hit stop, hit reaction, target selection, network replication, weapon animation, sockets, or character movement.
- Camera-relative Recipe parameters, per-frame camera tracking, screen-space slash UI, screen distortion, Bloom requirement, or dynamic light.
- Arbitrary curve control points, arbitrary mesh/shader/material paths, arbitrary render queues, texture generation, VFX Graph graphs, or runtime AI.
- Multi-hit combo chains, looping aura behavior, beam behavior, per-target spawning, and cross-effect synchronization.
- Reusing empty projectile `launch`/`travel`/`impact` stages to impersonate the above timeline.

## Visual acceptance checklist

| Check | Pass condition |
| --- | --- |
| Direction | At normal third-person distance a reviewer identifies lower-left → upper-right without labels. |
| Primary silhouette | One dominant curved body, with the yellow edge visibly inside/along it rather than a detached stripe. |
| Layering | Afterimages are delayed/subordinate; sparks are detached and sparse; no layer hides the primary arc. |
| Timing | Primary read completes by 0.20 s; all visual residue is absent by 0.45 s. |
| Perspective | Front, side, oblique-top, close, and game-distance views retain a legible arc and avoid implausible flat-card popping. |
| Backgrounds | Arc remains legible on dark, neutral, and bright backgrounds without Bloom. |
| Scale | Default footprint reads at a 1.8 m character scale and does not permanently obscure the torso. |
| Lifecycle | Play/reset/replay clears ribbon residue and particles; no teleport streak or stale afterimage. |
| Scope | No gameplay component, character/weapon dependency, or forbidden projectiles stages are introduced. |
