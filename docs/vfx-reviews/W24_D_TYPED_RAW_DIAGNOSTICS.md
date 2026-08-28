# W24 D baseline typed raw diagnostic foundation

Status: source and EditMode coverage added; **not Unity-executed**.

## Raw artifacts

- `W24RendererMaskDiagnosticCapture` accepts only an explicit non-empty, enabled, active and
  duplicate-free `Renderer` collection. It draws only those renderers through a hidden shader
  into `R8_UNorm`, requires synchronous R8 readback and depth-stencil support, then exposes a
  binary `0/255` `HxW` NPY payload. It has no Beauty/PNG fallback.
- `W24LinearLdrDiagnosticCapture` renders the serialized tagged `MainCamera` into a
  single-sample, HDR-disabled `ARGB32` target with `RenderTextureReadWrite.Linear`. It reads back
  a linear `float32` normalized `HxWx3` or `HxWx4` NPY array. It deliberately has no PNG encoder;
  presentation PNGs cannot become formal raw pixel inputs.
- `W24NpyWriter` now emits finite C-contiguous float32 `HxWx3` and `HxWx4` arrays in NPY v1.0.

Both captures reuse `W24ObjectIdDepthDiagnosticCapture.CameraState`, execute explicit rendering
inside `try/finally`, and restore the serialized camera plus `RenderTexture.active` on success or
failure.

## Receiver-ID integration (no S3 mutation in this change)

For a future D-light baseline, add two serialized `W24DiagnosticObjectRegistration` components
to scene-only `Receiver_A_LinearProbe` and `Receiver_B_LinearProbe` with distinct nonzero IDs,
then call the existing `W24ObjectIdDepthDiagnosticCapture.Capture(mainCamera, registrations,
view, width, height)`. Persist its `ObjectIdNpy` alongside the linear-LDR NPY and the explicit
renderer mask. The metrics plan must consume the receiver-ID pixels to define each luminance ROI;
it must not infer receiver regions from renderer names or Beauty pixels. This document does not
change S3 authoring, recorder, evidence sealing or S5 admission.

## Remaining integration work

The D raw captures are deliberately isolated. A later S5-owned integration must bind each raw
artifact to C0 token provenance and a frozen metric plan before any requirement may pass.
