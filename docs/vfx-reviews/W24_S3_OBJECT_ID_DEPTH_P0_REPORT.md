# W24 S3 Object-ID + Linear Depth P0

Status: isolated implementation prepared; not yet integrated into the common recorder or S3 producer.

The P0 diagnostics use the formal serialized `MainCamera` matrices and explicit
`CommandBuffer.DrawRenderer` calls. They never derive IDs/depth from Beauty and they do not
run through URP post processing. `R32_UInt` is emitted as NumPy `<u4`; `R32_SFloat` is emitted
as NumPy `<f4`. Unsupported rendering/readback formats, missing IDs, non-finite depth, and
unregistered renderers fail closed.

The implementation deliberately creates an ephemeral diagnostic material per object ID. It
therefore changes neither shared materials nor renderer material-property blocks. All GPU
objects and temporary materials are released in `finally`; the formal camera state is also
restored in `finally`.

The next authorized integration must add typed diagnostic records to
`W24ContinuousCaptureRecorder`, register the S3 binding marker/fragments, and bind the sealed
NPY hashes into the render-metrics input and C0 trace.

## Hardening completed

- Every new Unity asset/source has a stable `.meta`, including the shader folder.
- Required registrations reject missing, disabled, inactive, duplicate, out-of-range, and
  unsupported renderers. P0 accepts only `MeshRenderer` and `SkinnedMeshRenderer`.
- Capture preflight rejects a non-active/non-MainCamera authority, invalid clip/FOV values,
  empty view IDs, non-finite/non-normalized poses, singular explicit matrices, and dimensions
  outside `1..8192` before allocating GPU resources.
- `D24_UNorm_S8_UInt` render support is checked together with R32_UINT/R32_FLOAT render and
  synchronous readback support.
- NPY floats reject NaN/Infinity. The test fixture parses the NPY v1 header and raw payload,
  rather than checking text fragments only.
- Camera restoration covers transform, target, clear/background/mask/HDR/MSAA/enabled state and
  both custom `worldToCameraMatrix` and `projectionMatrix`; an exception-path test covers it.
- Focused Roslyn static compilation of the three runtime sources against Unity 2022.3 managed
  assemblies passed. No Unity process was started.

## Deferred graphics smoke risks

Only a graphics-enabled Unity batch run can prove the host adapter supports synchronous
`ReadPixels` from `R32_UInt` and `R32_SFloat`, MRT writes with an integer/float target pair, and
the new shader's `uint SV_Target` output on the actual Direct3D driver. These are deliberately
startup fail-closed checks; a failure blocks formal evidence rather than switching to RGBA or
Beauty-derived diagnostics.
