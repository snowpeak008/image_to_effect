# W24 S3 — remaining vertical baselines

Status: `FORMAL_MACHINE_EVIDENCE_BOUND_VISUAL_REVIEW_PENDING`  
Date: 2026-08-25

The repaired source chain is frozen at `W24S3BaselineAuthoring.CompilerVersion =
w24-s3-baseline/2.7` (source SHA-256
`27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35`) and capture tool
`w24-s3-capture/3.5` / `sha256:f605aacf4d27128347a2a8434a29cebc95aa789af73df55cc26c6fd7a0e42726`.
The earlier 3.4 identity is superseded: its materials were separated, but its Contract and test
incorrectly treated the neutral Lit asset's importer-managed `_EMISSION` keyword as a functional
invariant. Version 3.5 gives URP compatible GI hints, but accepts neutral receivers only from their
black serialized emission, independent material identity and receiver-only real-Light A/B result;
it then revalidates functional material state and frozen outputs after the transaction's final
directory import. A fresh isolated rebuild and graphics-backed capture have now completed; only
the independent visual/user authority remains pending.

## Scope delivered

Three isolated authoring targets were added. The Editor command `VFX Composer/W24/S3/Build three remaining baselines` writes/imports each Recipe first, then creates exactly one owned Runtime Entry under `Assets/VFX/Generated/<effectId>/`, plus shared dependencies under `Assets/VFX/Shared` and scene-only previews under `Assets/VFX/Preview/W24S3`.

`Runtime/W24/W24S3RuntimeEntry` is the sole Player-safe `IVfxRuntimeEntry` in every generated S3 Prefab. It owns Initialize / Play / Stop / ResetForPool / SendEvent and forwards lifecycle actions to the S2 moving-trail, binding/fragment, semantic timeline and lighting modules. Cleanup now calls `W24SemanticTimeline.ResetForPool`, emits a distinct interrupt transition before reset, defers `Completed` until bounded-tail cleanup, and sends impact/fragment through that bounded tail. Fragment visuals detach from the model root before independent motion. `W24S3PreviewDriver` is scene-only and drives only that public Entry; the Runtime Prefab contains neither preview driver.

| Baseline | Runtime Entry | Formal Preview | Required real carrier | Cleanup rule |
|---|---|---|---|---|
| B — Moving projectile / trail | `VFX_w24_moving_projectile_trail.prefab` | `VFXPREVIEW_MovingProjectileTrail.unity` | `W24MovingEmitterTrailProtocol` + `TrailRenderer` | Entry events move the emitter; immediate stop and pool reset clear vertices. |
| C — Model socket / fragments | `VFX_w24_weapon_socket_fragments.prefab` | `VFXPREVIEW_ModelSocketFragments.unity` | `W24ModelBindingAdapter` to `weapon_socket` + `W24FragmentMotionSystem` on three independent Transforms | `ConfigureModelRoot(Transform)` then `Play`; a null/missing root yields explicit fault and reset restores the visual home. |
| D — Real lights / receivers | `VFX_w24_real_light_receivers.prefab` | `VFXPREVIEW_RealLightReceivers.unity` | Two unshadowed `UnityEngine.Light` components, `W24RealLightingModule`, an independent persistently emissive source-body material, and neutral receivers A/B on a separate material | Entry Stop/Reset disable lights and clear all roots. |

Each `docs/vfx-contracts/w24_*.contract.json` now passes the common lowerCamel W24 S1 Schema/semantic validator. The contracts include all ten required segments, allowed and forbidden substitutions, real camera/tool metadata, canonical plus two robustness seeds, object-form layer costs, and `VISUAL_PENDING`. Implementation Trace registrations are independent files under `docs/vfx-traces/`, not illegal contract top-level fields. The bootstrap records remain immutable source inputs; the isolated build created separate real-identity C0 receipts and the graphics run created separate `FORMAL_EVIDENCE_BOUND` traces. No item is labelled L3 or L4.

The current bootstrap bindings are B r26 / `sha256:93fce950b2bbea9de590bdbc9594f43bce5894ebf4ccbacafbe370af8d279b58`, C r25 / `sha256:88c752a126d0c0922861af647158b884dfb9095b173e573d3279239ca0f3dc4d`, and D r24 / `sha256:0a7b6c6715834f17c5b4864346c015cc3b96bbd32b5be734d3b2b6b38e959859`; each bootstrap Trace binds the exact corresponding requirement set in both directions.

Production Manifests are written only to `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json` by the internal S5 `CommitFirstFormalBuild` transaction. Authoring code cannot obtain or manufacture its `formalProduction` binding. The Manifest retains the exact pending Contract/Trace file hashes as an evidence-free `PRE_C0_FIRST_FORMAL_BUILD` receipt; there are no ad-hoc top-level contract/trace blocks. Its complete ownership records contain GUID/type/SHA-256, dependencies and cost. Each effect owns only its isolated generated folder. B and C own one material each; D owns a neutral receiver material and an independent emissive core material. Preview scenes are neither owned outputs nor Player dependencies.

After the transaction, a gate-issued immutable bootstrap receipt is the sole input to the write-once `docs/vfx-candidates/<effectId>/C0/` freezer. That separate candidate freezes the real scene hash, raw build identity and Runtime Entry GUID, remains `C0_CAPTURE_PENDING`, and contains no authority/cross-evidence records. The original bootstrap Contract/Trace are never rewritten. Real capture must later create a distinct `FORMAL_EVIDENCE_BOUND` Trace before the ordinary exact-plan gate can replace the Manifest binding.

## Persisted-material rejection and reusable rule

The rejected 3.3 design enabled `_EMISSION` on a black shared URP Lit asset and supplied the
actual color through a runtime `MaterialPropertyBlock`. That looked valid on the transient
`Material` during first authoring, but the importer later wrote no valid emission keyword. The
3.4 repair separated materials but still treated the neutral asset's importer-managed keyword as
a functional invariant. A later folder import demonstrated that the keyword can change while its
serialized `_EmissionColor` remains black. The general rule is now: source-body emission requires
non-black serialized emission plus its compiled keyword; neutral receiver semantics require a
different URP Lit asset with black serialized emission and a positive receiver-only real-Light A/B
measurement. Runtime overrides may tune a valid variant but must not be the sole reason the variant
exists. Authoring must save, force-reimport, reload, run the transaction's final exact-folder
import, and then read-only validate functional material state and frozen output hashes before
commit. A diagnostic receiver must never share an emissive source material. Any violation
rejects the entire affected Contract/build/capture identity and requires a fresh C0 rather than an
in-place evidence repair.

## Machine coverage added

- `W24S3BaselineContractTests`: common-S1 strict parsing/hash validation; independent trace references; external Manifest paths; strict isolated ownership; Preview exclusion; one runtime entry; real carrier / receiver structural assertions; and post-reimport separation of D's truthful emissive core material from its neutral receiver material.
- `W24S3BaselineRuntimeTests`: natural-frame PlayMode checks use only `W24S3RuntimeEntry` public lifecycle/events for source motion and trail reset, configured-root socket parenting plus missing-root fault, independently integrated fragment event, and actual Light enable/reset.
- `W24S3FormalEvidenceTests`: marked `Explicit`; it validates each write-once C0 candidate against its real Scene, Manifest build hash, Runtime Entry GUID, receipt hashes and registered capture-tool bundle before capture. It does not use manual `Emit`, `Simulate`, `Sample`, or time jumps.

## Isolated formal execution result

The source chain was copied byte-for-byte into
`.codex_tmp/w24-fresh-20260825-0628` and executed without touching the user's open canonical
Editor. The 31-source bundle recheck was `31/31`, and all three common contract validators passed.

- Batch first-formal authoring exited successfully and created exactly three Generated roots,
  three compiler `w24-s3-baseline/2.7` Manifests and three immutable C0 candidates.
- Post-import material readback found the core material with non-black HDR emission and
  `_EMISSION`, while the separate neutral receiver material retained exactly black serialized
  emission. No receiver depends on the source-body material.
- `W24S3BaselineContractTests`: `19/19` passed.
- `W24S3BaselineRuntimeTests`: `6/6` passed through natural PlayMode frames.
- `W24S3FormalEvidenceTests` precondition gate: `1/1` passed.
- The three graphics-backed capture methods were run separately and each passed `1/1`. Their
  sealed typed-raw counts are B `9`, C `21`, and D `12`; each has one sealed metrics input and one
  measured metrics report. Recorder PlayerLoop serials are fully consumed (`454/454`, `454/454`,
  and `547/547`).
- The resulting evidence traces are all `FORMAL_EVIDENCE_BOUND`: B has four of six machine
  requirements passed, C five of seven, and D four of six. The remaining two requirements in
  every baseline are deliberately the independent Visual QA and user authorities.

The evidence is write-once under the isolated shadow's
`artifacts/vfx-evidence/<effectId>/C0` and
`docs/vfx-candidates/<effectId>/C0/evidence`; it has not been copied over the canonical project.
S3-specific Player-build evidence has not been claimed. Most importantly, all visual requirements
remain `VISUAL_PENDING`: no visual pass, L3, L4, user signature, or commercial-quality claim is
created by these machine results.
