# S15 authority-frame particle projection audit

> Status: **machine proxy only; no visual or commercial-use pass is issued.** The signed user boundary remains: conditional pass, “内容还可以，但无法做商用”.

## Sealed input binding

- Authority run: `run-20260823T041806959Z`; frames hash-verified: `28/28`.
- Metadata SHA-256: `7187f00123cbacb39166f59bb8ffa3a1369bc6049731b8f28a23dff144ec94ca`.
- Derived JSON SHA-256: `a73619e373d7c76984a4328c023be8fa66c45d05ba15e4dddc42aff349306d41`.
- Measurement tool SHA-256: `9e7bf9fb15d742606b89544d1de36b0b6f8c82da0f8ec012562325c9dcb39225`.
- The tool opens the authority metadata and PNGs read-only and writes only this derived report/JSON pair.

## Reproducible proxy

Foreground is every pixel whose RGB code differs from the declared `[19,21,24]` background. The largest 8-connected component is the main-effect proxy. A detached particle proxy is a non-main component on a recorder-live frame, at most 128 pixels and 32×32 pixels, whose peak red is at least 64 codes above background. The blade proxy is the subset of the main component with `R>=150`, `G/R>=0.80`, and `B/R>=0.25`.

These thresholds are `PENDING_HUMAN_OR_DIAGNOSTIC_CALIBRATION`. They were not calibrated or signed as an acceptance gate.

## Key-frame measurements

| Frame | Time | Recorder live | Detached visible proxy | Visibility lower bound | Unresolved/merged/occluded-or-dim | Min canvas clearance | Blade-proxy overlap | Nearest blade proxy |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `0001` | `0.016667` | 0 | 0 | — | 0 | — | 0 px | — |
| `0002` | `0.033333` | 0 | 0 | — | 0 | — | 0 px | — |
| `0006` | `0.100000` | 0 | 0 | — | 0 | — | 0 px | — |
| `0010` | `0.166667` | 8 | 4 | 50.0% | 4 | 153 | 0 px | 46.174 px |
| `0014` | `0.233333` | 8 | 6 | 75.0% | 2 | 149 | 0 px | — |
| `0020` | `0.333333` | 1 | 1 | 100.0% | 0 | 255 | 0 px | — |
| `0025` | `0.416667` | 0 | 0 | — | 0 | — | 0 px | — |
| `0027` | `0.450000` | 0 | 0 | — | 0 | — | 0 px | — |

## What the bytes do and do not establish

- Evidence integrity: `VERIFIED_28_OF_28`.
- Every detected detached particle proxy is inside the canvas; minimum measured border clearance is `148 px`.
- Detached particle/blade-proxy overlap is `0 px`. This is zero by detached-component construction and **does not prove** true particle/blade overlap is zero.
- True overlap and occlusion remain `PENDING_INSTANCE_ID_OR_DEPTH_DIAGNOSTIC` because the sealed beauty PNGs contain no instance-ID, renderer-ID, depth, or unoccluded-projection pass. Recorder-live particles that are merged into the crescent or below the proxy threshold are counted only as unresolved.
- Family resemblance, visual polish, legal provenance, licence scope, and commercial suitability are not evaluated here. The user's “有条件通过但不可商用” boundary is unchanged.
