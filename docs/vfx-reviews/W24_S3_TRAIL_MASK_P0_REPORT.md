# W24 S3 trail-only mask P0

Status: isolated implementation prepared; not integrated with the common recorder or S3 capture producer.

`W24TrailMaskDiagnosticCapture` accepts an explicit enabled `TrailRenderer` plus externally
recorded per-frame emitter history. It draws only that renderer using a temporary hidden mask
material and the formal MainCamera matrices into an `R8_UNorm` target with a private depth buffer.
The readback must contain exactly `0` or `255`, and is emitted as NumPy `|u1`; there is no Beauty,
post-processing, TrailRenderer vertex inspection, RGBA, or fallback path.

The next integration must store the per-frame emitter history in semantic telemetry, write the
mask NPY/hash as a typed diagnostic artifact, and pass the projected external history into the
existing Python `trail` metric input.

Safety and test scope:

- The capture scopes the formal MainCamera through the existing `CameraState` guard, including
  its explicit view/projection matrices. It does not change any TrailRenderer field, material,
  material-property block, layer, active state, or shared asset; the diagnostic material and
  render targets are temporary and are released in `finally`.
- Edit-mode coverage parses the NPY v1 header and payload bytes exactly, rejects non-binary
  data, exercises all formal format-failure branches, and validates the pure frozen-matrix
  external-history projection. The reusable camera-state exception-restoration test remains in
  `W24ObjectIdDepthDiagnosticTests`.
- A real Unity 2022.3 URP batch graphics smoke is still required before this can become a formal
  artifact: verify the platform supports `R8_UNorm` Render + ReadPixels and D24 rendering,
  that `CommandBuffer.DrawRenderer` renders the chosen TrailRenderer with the replacement pass,
  and that the R8 readback is exactly `{0,255}` on the target GPU. Any failure remains
  fail-closed; no conversion from Beauty or RGBA is permitted.
