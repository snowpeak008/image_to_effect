# S15 authority-frame detached-warm-component geometry audit v2

> Status: **machine geometry proxy only; no spark attribution, visibility verdict, visual pass, or commercial-use pass is issued.** The signed user boundary remains: conditional pass, “内容还可以，但无法做商用”.

## Supersession

V1 is preserved byte-for-byte but is `SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO`. Its component-to-spark ratio interpretation is invalid because beauty RGB cannot identify particle instances. V1 must not be used as active evidence.
Preserved v1 JSON SHA-256: `a73619e373d7c76984a4328c023be8fa66c45d05ba15e4dddc42aff349306d41`; report SHA-256: `876aecaf99088b8456d5c49f2b23941a0c56bd9d18e82ad0bb4f80d42297cb48`.

## Sealed input binding

- Authority run: `run-20260823T041806959Z`; frames hash-verified: `28/28`.
- Metadata SHA-256: `7187f00123cbacb39166f59bb8ffa3a1369bc6049731b8f28a23dff144ec94ca`.
- Derived JSON SHA-256: `78ff00ad8e781820fa6666818ffeb580496af68798a5593e31ae8091205cd13b`.
- V2 measurement tool SHA-256: `625dcf77316850da2d17f8c0da0dc2c29fe7f949b5a94f4cbe53d8769792440d`.
- Authority metadata/PNGs and superseded v1 outputs are opened read-only or not opened; v2 writes only to its new derived directory.

## Reproducible geometry proxy

Foreground is every pixel differing from the declared `[19,21,24]` background. The largest 8-connected component is the main-effect proxy. A detached warm candidate is a non-main component at most 128 pixels and 32×32 pixels with peak red at least 64 codes above background. This says nothing about whether that component is a spark, dissipation, mesh fragment, anti-aliased island, or another source.

Recorder spark and dissipation counts are copied only as context. Their sum is `unattributedRecorderLiveCount`; no RGB component is matched to any recorder instance.

## Key-frame measurements

| Frame | Time | Spark live context | Dissipation live context | Unattributed recorder live | Detached warm candidates | Min candidate border clearance | Nearest blade-proxy pixel |
|---:|---:|---:|---:|---:|---:|---:|---:|
| `0001` | `0.016667` | 0 | 0 | 0 | 2 | 208 | 5.000 px |
| `0002` | `0.033333` | 0 | 0 | 0 | 2 | 208 | 5.000 px |
| `0006` | `0.100000` | 0 | 0 | 0 | 0 | — | — |
| `0010` | `0.166667` | 8 | 0 | 8 | 4 | 153 | 46.174 px |
| `0014` | `0.233333` | 8 | 2 | 10 | 6 | 149 | — |
| `0020` | `0.333333` | 1 | 3 | 4 | 1 | 255 | — |
| `0025` | `0.416667` | 0 | 2 | 2 | 0 | — | — |
| `0027` | `0.450000` | 0 | 0 | 0 | 0 | — | — |

## Interpretation boundary

- Frame `0020` records spark context `1`, dissipation context `3`, and therefore `4` unattributed live instances. RGB contains `1` detached warm candidate; **no correspondence or ratio is inferred**.
- Detected warm candidate pixels do not touch the canvas border; the minimum measured clearance is `148 px`. This cannot reveal off-canvas projections.
- True spark canvas containment, blade overlap, visibility, and occlusion all remain `PENDING_INSTANCE_ID_OR_DEPTH_DIAGNOSTIC`.
- Off-canvas, merged, occluded, below-threshold, dissipation/fragment, and foreign-source explanations cannot be distinguished from these sealed beauty RGB frames.
- Thresholds remain `PENDING_HUMAN_OR_DIAGNOSTIC_CALIBRATION`. Family resemblance, legal provenance, licence scope, and commercial suitability are not evaluated; the user's restriction is unchanged.
