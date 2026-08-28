# Contract authoring

## Evidence authority routing

| Requirement type | Required authority | Authoritative evidence | QA role |
|---|---|---|---|
| `visual-measurable` | `diagnostic` | Diagnostic Pass measurement | Beauty-frame cross-check only |
| `visual-semantic` | `visualQa` | Independent Beauty-frame review | User/Human Verdict cross-check |
| `behavioral`, `structural`, `budget` | `telemetry` | Semantic telemetry (and build report for budget) | Report only visible contradiction |
| `user-signoff` | `user` | User verdict | Visual QA advisory only |

`calibrationLabels` is not a production-contract authority. It is reserved for S0a frozen ground truth in the Visual QA calibration protocol.

## Write verdicts that can be reviewed

Write a visual requirement as a visible condition, not an aspiration. For example:

```text
At steady frames 90–150, the core layer remains inside the central 40% of the effect mask;
the smoke layer may overlap no more than the outer perimeter. Inspect core/smoke masks in ROI x=….
```

The values and ranges must come from the intended contract, not be copied as defaults. State when the intended observation cannot be captured; this is a contract defect, not permission to guess.

For an internal-plus-visible claim, split it. Example: “Light affects the ground” becomes a telemetry requirement for the Light component and a diagnostic requirement for receiver linear luminance. A qualitative perceived hierarchy, if required, is a separate `visual-semantic` requirement.

## Candidate and state rules

A candidate is immutable: contract hash, Prefab/Manifest hash, Capture-tool hash, evidence hashes, and QA report together form `C0`, `C1`, or `C2`. Evidence directories are write-once. Every candidate reviews every required item and emits a per-requirement regression difference. QA gets only the current contract and current candidate evidence in a fresh isolated session; historical comparison occurs after review.

All stored hashes use `sha256:` followed by lowercase hexadecimal SHA-256. Canonicalize JSON as UTF-8, sorted object keys, and no insignificant whitespace. Compute `contractHash` from the canonical contract with its own `contractHash` member omitted; compute every other manifest/report hash from its canonical content with only that member omitted. Store a new candidate directory or new report revision for any changed byte; never replace an existing evidence file, manifest, label set, or sealed report.

## Capture identity freeze and cycle avoidance

The three required Capture Profile content identities are fixed before C0 and are inputs to the final pre-C0 contract revision:

- `sceneHash` hashes the canonical formal serialized capture scene only. The scene must not embed candidate evidence, report metadata, or `contractHash`; candidate metadata references the scene hash instead.
- `prefabManifestHash` is `BuildManifest.buildHash`: the canonical content identity of the built Prefab/manifest inputs. It excludes `designContractHash`, candidate ID, QA report, and evidence fields. It is not a hash of a manifest file whose bytes include the contract hash.
- `captureToolHash` hashes the frozen capture tool source and configuration, excluding output directories, emitted manifests, and evidence hashes.

The implementer may build these three inputs before C0. Design Director then writes the final frozen `contractRevision` and `contractHash`; only after that may C0 capture. A later source/configuration change cannot be “backfilled” into an existing contract or evidence directory: it creates a new revision and a new C0. This one-way order prevents a `designContract ↔ manifest/evidence` hash cycle.

## W24 S1 structural completeness

The common S1 schema requires all ten §7.9 segments for `sustained`, `one_shot`, and `event_driven` lifecycles. In particular, it does not accept a vague `captureProfile`: Unity/URP, graphics API/driver, serialized camera/scene/Prefab-manifest/renderer/volume identities, scene/Prefab-manifest/capture-tool hashes, camera pose/FOV, render settings, and seed plan are all required. Before the first formal build only, `extensions.captureBindingStatus = PENDING_FIRST_FORMAL_BUILD` permits the explicit `pending:formal-build` sentinel for `sceneHash` and `prefabManifestHash`; this state is pre-C0 and is prohibited from capture. The capture tool identity must always be a real canonical hash. `FROZEN_PRE_C0` requires real scene and manifest hashes. The schema also rejects a layer that omits geometry, material model, colour role, blend mode, motion model, timing, attachment, continuity, or budget cost. It requires a canonical unsigned-32-bit seed and exactly two distinct robustness seeds; the canonical seed must also differ from them. JSON Schema cannot compare an array value with a sibling scalar, so the final difference and capture-binding checks are explicit validator invariants.

L3 requires the normal qualified-QA route; L4 additionally requires a user signature bound to `contractRevision + buildHash + captureProfile`. A visual-output change expires that signature and returns the entry to L3.
