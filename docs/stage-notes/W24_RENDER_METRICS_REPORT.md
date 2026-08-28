# W24 §13.2b render metrics toolset

Status: source implementation and synthetic-fixture verification only. No Unity
frame was captured, and this note records neither a formal machine-gate pass
nor a visual, aesthetic, commercial, or user-acceptance result.

## Delivered scope

`tools/vfx/metrics/render_metrics.py` is a reusable Python library and CLI.
It uses only the Python standard library, NumPy, and Pillow. It deliberately
does not install or import OpenCV.

```powershell
python tools/vfx/metrics/render_metrics.py evidencemetrics-input.json --output evidencemetrics-report.json
```

The input schema is
`tools/vfx/metrics/w24-render-metrics-input.schema.json`. Each input byte has
an explicit `sha256:<lowercase hex>` registry entry. The tool opens and hashes
each item before decoding it; missing, malformed, escaping, or mismatched
evidence produces the report-level route `EVIDENCE_INVALID`, never a metric
failure. Optional `captureMetadata` consumes the existing recorder's
`w24-s0a-capture-evidence/v1` format: its own hash, diagnostic manifest,
retained Beauty/diagnostic artifacts, supplemental diagnostics, and frozen
source/build-hash fields are checked. Frozen Unity source files are normally
outside the evidence directory, so their canonical identities are checked for
presence; the evidence artifacts themselves are byte-hashed.

The input supports PNG diagnostic rasters and `.npy` arrays (the latter is
useful for lossless ID/depth/linear-HDR test or capture adapters). All emitted
fields are numeric, identifiers, pass/fail, exemption, or evidence-routing
facts. The report has no visual-quality conclusion.

Every raster used by a metric check must be registered as `kind: diagnostic`.
This metrics input schema intentionally does not admit Beauty or telemetry
items; they remain evidence for their separate cross-evidence workflows.
Receiver checks additionally require the precise
`receiver-linear-hdr` / `linear_hdr` declaration.

Each check also requires its expected pass contract (`effect-mask` / `layer-mask`
binary mask, `trail-only-mask`, integer lossless fragment/receiver/object IDs,
linear floating depth, floating normal vectors, or linear HDR as appropriate).
Non-finite arrays, RGB ID images, non-binary masks, and incompatible encodings
are `EVIDENCE_INVALID`. Registry IDs cannot replay the same path. When formal
capture metadata is supplied, every metadata artifact must bind exactly one
matching registry path and hash.

Reports include canonical input hash, tool-source hash, and a sealed report
hash. The CLI refuses to overwrite a report path, preserving write-once output.

## Measurements

| Check kind | Measurements and rule |
|---|---|
| `mask_steady` | Effect/layer mask area, centroid, luminance 5/50/95 percentiles; exactly three declared steady windows, area/luminance distribution ranges, and per-window linear area drift. |
| `autocorrelation` | Declared-period mask-area autocorrelation with a lag tolerance. With no declared period it returns `NOT_APPLICABLE_RANDOM_STEADY`, not a periodicity failure. |
| `cleanup` | Baseline-normalised MAE and 4-connected residual component areas, excluding contract `allowedResidualLayers`. |
| `trail` | Zhang–Suen trail-mask skeleton approximation, projected-history corridor coverage, mean nearest projected-history distance, and (when stationary masks are supplied) only head-to-new-space pixels. Tail fade/shrink outside that supplied head region is not prohibited. |
| `transition` | `continuous`: per-layer IoU, anchor distance, area-change ratio. `impulse`: explicitly preserved anchor only, allowing area discontinuity. `replace`: incoming-area floor with IoU not required. `clear`: remaining-area ceiling with anchors not required. |
| `receiver_luminance` | Receiver-ID region outside effect mask; fixed-exposure linear HDR on-minus-off luminance. The CLI requires diagnostic `receiver-linear-hdr` artifacts with `linear_hdr` encoding and rejects Beauty input. |
| `fragment_tracks` | Fragment-ID centroids, principal-axis angles, trajectories, and pairwise trajectory correlation. Its result is marked `cross_evidence_only`; it cannot replace telemetry as the authority for fragment independence. |
| `multiview_3d` | Object-ID/depth view measurements: depth span, centroids, anchor parallax, overlap-depth deltas, and optional normal angular spans. Default is depth-span evidence with parallax reported but not required, avoiding a false failure for deliberately centred multi-camera views; a contract can set `requireParallax: true`. A contract carrier of `billboard` returns `BILLBOARD_CONTRACT_EXEMPT`. |

`machineGatesPassed` means only that the supplied numerical threshold checks
passed; it is not an authority override. In particular, telemetry remains the
authority for trail vertex provenance and fragment independence, while Beauty
remains a cross-evidence/viewing artifact.

## Fixture tests

`tools/vfx/tests/test_render_metrics.py` uses small programmatic NumPy arrays,
not asserted Unity evidence. It exercises positive, negative, and boundary
cases for all check families, including residual-layer exemption, a periodic
versus random steady declaration, stationary head-only trail growth, all four
transition modes, Billboard exemption, hash mismatch, and Beauty rejection for
receiver luminance.

Verification run:

```text
python -m unittest discover -s tools/vfx/tests -v
Ran 23 tests ... OK
```
