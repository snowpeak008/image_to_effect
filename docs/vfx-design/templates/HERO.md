# Hero contract template

Use for ultimate skills, model-bound effects, multi-camera effects, or effects with several perceptual phases.

In addition to Standard, declare:

- visual hierarchy and expected focus for each phase;
- per-layer visibility and occlusion policy;
- multiple 3D capture views and receiver-lighting requirements where applicable;
- explicit degradation / LOD policy and a per-phase budget;
- all effect-to-model/socket/bone bindings and missing-binding behavior;
- an effect-wide causal map so a release, hit, and residual cannot be unrelated picture swaps.
