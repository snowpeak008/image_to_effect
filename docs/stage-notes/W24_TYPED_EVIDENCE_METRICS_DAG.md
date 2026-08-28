# W24 Typed Evidence / Metrics DAG

## Scope

This stage adds infrastructure only.  It does not change an S0b or S3 capture fixture,
authoring asset, shader, or visual verdict.

The previous generic supplemental-diagnostic path could prove that a file was hashed, but
could not prove that a JSON summary was the Object-ID, depth, trail-mask, or metric result
claimed by an implementation trace.  Typed raw diagnostics now carry `passId`, `encoding`,
seed, logical frame, real PlayerLoop observation serial/time, required view ID, and required
derivation reference.

## Non-cyclic capture order

1. Recorder writes raw typed diagnostics below `diagnostics/` from unconsumed natural-frame
   tokens.
2. Recorder writes a write-once `w24-render-metrics-input/v1` JSON.  It names raw path/hash/
   pass/encoding entries and has no `captureMetadata` reference.
3. The controlled bridge creates one private system-temp tree, places `metrics-input.json` at
   its root, and copies every registry raw artifact to its declared `diagnostics/...` path after
   path/hash/pass validation. All input files are read-only; Python receives only this isolated
   tree and a temp report path, never the formal evidence root. The bridge re-hashes the formal
   input plus every private raw copy after the child returns, enforces a 120-second child-only
   timeout, and recursively removes the validated temp tree.
4. The recorder reads, validates and commits report bytes; the tool never writes the formal
   evidence root.
5. `Complete()` writes final metadata and seals raw passes, input and report together.

## S5 anti-swap rules

- Input registry and final typed raw diagnostics must be a complete bijection; a failed
  seed/view cannot be omitted.
- The input carries a canonical-hashed required-evidence matrix whose rows have unique
  `evidenceId`. S5 requires byte-canonical equality with
  `Contract.extensions.typedDiagnostics.requiredEvidenceMatrix`, then exact matrix/registry/raw
  resolution. The producer cannot delete an entire required pass, view, frame, or A/B row.
- The report binds canonical input JSON hash, tool hash, and self-seal hash.
- Input binds effect, candidate, contract revision/hash, capture-profile and C0 capture-tool
  bundle hashes.
- Bridge and S5 strictly parse the Contract-named bundle, reject reparse paths, replay every
  listed source hash, recompute its canonical bundle hash, and require exactly one
  `tools/vfx/metrics/render_metrics.py` whose source hash equals `expectedToolSha256`.
- Report check IDs and kinds must exactly equal the frozen input checks and every check must
  pass.
- Input and Contract freeze the absolute Python path, executable-byte SHA-256, Python version,
  NumPy version, Pillow version, and a canonical aggregate environment hash. The bridge and S5
  both replay that probe through the narrow `ProbeMetricsEnvironmentForInput` API.
- Trace evidence with `passId`/`encoding` must resolve to the identical sealed typed artifact.
- Every passed diagnostic authority or cross-evidence item additionally requires
  `metrics-report`/`json`, `metricCheckId`, and `analysisInputSha256`; S5 maps every frozen check
  exactly once to its signed Contract requirement. Generic summary JSON is supplemental only.

## Compatibility

S0a/S0b entries which contain no typed diagnostics/metrics records retain their existing
generic telemetry and supplemental diagnostic paths.  Once a requirement declares a typed
pass or a metrics check, however, S5 takes the strict branch and cannot fall back to generic
JSON.

## Static verification

- `python -m unittest discover -s tools/vfx/tests -p 'test_*.py' -v` passes (including the
  `receiver_luminance_ldr` and fragment-track anti-rigid-body cases).
- Unity was deliberately not launched for this infrastructure-only change.  The added C#
  trace validation test is intended for the next Unity EditMode run.

## Deferred P1

The executable bytes and imported NumPy/Pillow versions are frozen. A full installed-distribution
aggregate hash (all package files, native extensions and transitive dependencies) is not yet
implemented and remains a declared P1 hardening item rather than a claimed property of this stage.
