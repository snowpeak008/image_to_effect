# WP-PROTOCOL-P3-RECOVERY — STOPPED

## Scope, model, and recovery record

- Objective: reconcile the collision-derived mixed Protocol/schema bytes into one bounded, pure-C# Phase 3 command/job wire-contract foundation.
- Model configuration: `gpt-5.6-terra`, `reasoning=max`, as required by dispatch.
- Reasoning record: requirements-first reconciliation of the existing Protocol design, ADR-002, the Phase 3 plan, strict-codec conventions, and the incompatible flat/nested partial attempts. The selected result is deliberately structural and vocabulary-bound; it introduces no Broker/Worker transition, transport, project I/O, or authority runtime semantics.
- Recovery timeline: first recovery-write proxy `2026-08-27T00:08:28.7529355Z`; last original Protocol-test write `2026-08-27T00:21:01.1310128Z`; audit-correction freeze `2026-08-27T00:52:19.6048901Z`.
- User-reported quota at restart: 24%; no <=5% stop condition was reported.
- Only the package allow-list and generated-output exception were used. No registry/status document, original collision handoff, solution/project file, Client, service, Unity, Broker, or Worker source was edited.

## Mixed baseline and aggregate replay

The required aggregate algorithm is repository-relative forward-slash paths; paths sorted with `StringComparer.Ordinal`; each line encoded as `<lowercase-sha256><two spaces><forward-path><LF>`; UTF-8 without BOM; files below `bin` or `obj` excluded.

| Root | Count | Accepted pre-edit aggregate |
| --- | ---: | --- |
| `src/VFXComposer.Protocol` | 67 | `eae304f088778dd990c5cfe51f98071bed46513f6059d25913e76706e0719b90` |
| `src/VFXComposer.Protocol.Tests` | 18 | `f7ce93800ab967945d67d80b0c119da61c6ff4741e4f3f56d9c54be5dcd1cabb` |
| `docs/schemas/desktop` | 29 | `8ee944b45d83e5ee131914ac71de878891a0e9835cf08d0c9c81d63d1a5319db` |

Two initial local diagnostics were rejected because they used package-root-relative paths rather than repository-relative paths. The first also used default PowerShell sorting (`25064b2151b5953e08a973a4d2cc7385ba6e2a76c0ac00373637113af4fe513a`, `59584d5db9951aa71b34e789ec3a912ab0f5e67926e6a92b303b903ad42f70b8`, `25651f45c8c8ddf5ee87752a25b84c0cbe4344994fd907e30b1b22ff5d645304`); the second retained package-root paths despite ordinal sorting (`26793a1753fcc0a511fab95c9c59698780ffd95bec40c09ffdd976fb9da210e0`, `b47478a2f3228b39020b1cf6fa1b81dd89e5e844401daa20b04fd6d916b57816`, `25651f45c8c8ddf5ee87752a25b84c0cbe4344994fd907e30b1b22ff5d645304`). The controller independently replayed the corrected algorithm with both PowerShell and Python before edits.

Independent final audit caught a stale post-edit aggregate receipt. Its correct pre-minimal-fix basis was Protocol `67/d52f232a38035352452c961f4d473309cdaf9538e13e75235074db17a3bc8751`, Tests `21/5f8ebccc29983462ff75cc1278acc0fdaecd2c431834dcf5aa6ea76d6288a5cd`, and schemas `33/e176d7d265e119ece658f3c4b9a56af5d395f59d530e38242688e53f9278ccba`. The final values below were independently replayed after the minimal correction and match the durable aggregate receipt.

## Selected contract and audit correction

The contract chooses the nested-envelope direction; it does not preserve the incompatible flat attempt.

- There are **9 root command DTOs** — ValidateRecipe, BuildCandidate, OpenPreviewJob, ClosePreviewJob, SetPreviewPlayback, ValidatePatch, ApplyPatch, RunFocusedTests, and CancelJob — plus the one reusable `CommandEnvelope` DTO.
- `CommandEnvelope` binds version, request/command/idempotency identities, project, lease/generation, closed command kind/capability, a confirmation-policy reference, and a typed self-hash. The confirmation reference is only `confirmation.policy.reference.v1` data, never a verdict or authority grant.
- Recipes and patches use content/contract/validation references rather than raw JSON tickets. Job correlation binds job/origin identities, origin command kind, and the expected typed command self-hash; all correlation identities are structural and distinct.
- The four immutable job DTOs are JobProgress, JobLogEvent, JobArtifact, and JobCompletion. Their states/outcomes/artifact kinds are closed vocabularies; artifact has no location/path; completion is not machine, visual, user, L3, or L4 authority.
- `MessageKinds`, `WireSchemaRegistry`, and `StrictWireCodec` form the single strict ingress/registry path. All 14 new schemas are exact Draft 2020-12 closed shapes.
- Audit correction: every new positive C# `long` wire value now has `maximum: 9223372036854775807`: command-envelope `leaseGeneration` in all 10 command shapes, and `leaseGeneration` plus `eventSequence` in all four job shapes (18 schema occurrences). Strict ingress calls `TryGetInt64` before self-hash validation. Exact `9223372036854775808` is rejected by the codec for all 18 P3 long fields and by schema validation for every applicable schema.

Explicit non-semantics: command acceptance, lease enforcement, transition legality, job execution/cancellation behavior, authorization/confirmation decisions, runtime dispatch, project mutation, persistence, transport security, external interoperability, and all authority-domain behavior remain out of scope.

## Exact changed-file manifest

The source manifest has 41 files and SHA-256 `9d5c564958ff4d2eca785b8a9cc5b98b4ccf1aaed4920d5e029088bb7a39af48`; durable replay confirms 41/41. This handoff is intentionally excluded to avoid self-reference; the controller must hash it externally after STOP.

```text
a1f266da856aaedcbcee9a7da2db67fa0fbdc7772df4056ee6f80790b5fc0191  docs/schemas/desktop/commands/vfxcomposer-apply-patch-command-v1.schema.json
0bbeaee63569752a169ca185b8d3474fa117730ecd187093b14ba08e6543b55d  docs/schemas/desktop/commands/vfxcomposer-build-candidate-command-v1.schema.json
79dd0c5f85003dcbd275cdc2a3a28a7fc8d5d7716bb60fc93d34e38544fa3016  docs/schemas/desktop/commands/vfxcomposer-cancel-job-command-v1.schema.json
450449977478e7afc367ab3a6b16ee6f2ce9ad787d72205f5b7fa405190af94a  docs/schemas/desktop/commands/vfxcomposer-close-preview-job-command-v1.schema.json
21a07ef611b949cb50dfbc6b031491025ae63f41480646f873d1dc6fd2f7fa56  docs/schemas/desktop/commands/vfxcomposer-command-envelope-v1.schema.json
cd6d3ab300b71f797778bd450f412503bbcfc5069cc495eb5f7f2aae68b43098  docs/schemas/desktop/commands/vfxcomposer-open-preview-job-command-v1.schema.json
1380167bd0a7eb4e3fa9c403561cb3ba771842071701df4f5334ebdd9b4ecc7b  docs/schemas/desktop/commands/vfxcomposer-run-focused-tests-command-v1.schema.json
dd40e07bec9491948dcafa0d9950339f6f9e1dee5ce932baaf86e52af30554c6  docs/schemas/desktop/commands/vfxcomposer-set-preview-playback-command-v1.schema.json
f31d38da077d7eb60045a79eae50170a332cffa4f6d81ba442b57fd2f27c410c  docs/schemas/desktop/commands/vfxcomposer-validate-patch-command-v1.schema.json
c9a111638b7459e3df6b9b7f6960c299ca430729f2e5c762f1a5beac5f525b33  docs/schemas/desktop/commands/vfxcomposer-validate-recipe-command-v1.schema.json
d47c3c79ceabadd09bd879d07f102f1e9b147a3f017448b307b7d96b5459277f  docs/schemas/desktop/jobs/vfxcomposer-job-artifact-v1.schema.json
3395bda6f6841355bac63b8efaccc48516a3110d6e207e60f664cd54228ca081  docs/schemas/desktop/jobs/vfxcomposer-job-completion-v1.schema.json
84ba401aff9646ce56ef9e18ac5026ced80dde9f2b211bf9d91257a1c1ca1c23  docs/schemas/desktop/jobs/vfxcomposer-job-log-event-v1.schema.json
e36c4f897d665637b40b4a411059750fc2421fb3a09abeae2c444d75df86c41e  docs/schemas/desktop/jobs/vfxcomposer-job-progress-v1.schema.json
43c47d6034b26b6be334324c250f2fe3525d33ba7ea1a5f580c5741b8629267c  src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs
5729a7af52d317c9a0c4c76b2149c66e0888c529d9bd33e695cc940b06aff583  src/VFXComposer.Protocol.Tests/GoldenVectors/phase3-command-job-v1.json
c926bba196ec09a5e31d131be1d74da37e92173c5895cb05cdd5d93f5cac552b  src/VFXComposer.Protocol.Tests/Phase3CommandJobContractTests.cs
8200d3cdae7577da30d28f217a8b115fe2aefbedc2328f9dcc9bb991631b8a16  src/VFXComposer.Protocol.Tests/Phase3WireFixtures.cs
58fe434394a64ee27dd213f3b5e4c48da806191dfcd4b9ae7134bcb7680d146d  src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj
d187bc0bfbbc77913a1f2f9dccbf73753eaaf231068492d762ce369937b00129  src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs
448009ee0dc8621034edbbbbdd5b13b100b1c238c04ed42e4a33d914a5c99295  src/VFXComposer.Protocol/Commands/ApplyPatchCommand.cs
1a0e33198f5a7b0c499e65b98ef334f0d9c7ad89d0c99c1cc9e696814c6ca555  src/VFXComposer.Protocol/Commands/BuildCandidateCommand.cs
24a7b4ffdf046cae66c392fbe2827196c451bb61a3257a8156921d52a0dbbd74  src/VFXComposer.Protocol/Commands/CancelJobCommand.cs
bdfe910eb43650a17c30ef2d555b98e968d26e4fd9273e59bc5cb42ff51ba1c2  src/VFXComposer.Protocol/Commands/ClosePreviewJobCommand.cs
857bc9b7b353caef7b1408e219f921c9665fcb4496424eaac8e989897d428593  src/VFXComposer.Protocol/Commands/CommandContracts.cs
434658ee205660007bc4be989433dc50bb9490ff27f458bcf4503863fd725ef4  src/VFXComposer.Protocol/Commands/CommandEnvelope.cs
4bfb1bbc5204a10cf0beddd72fa9776fa03ca56f735c46cf1175573e7ffd1f82  src/VFXComposer.Protocol/Commands/OpenPreviewJobCommand.cs
01732a99fcd2aa4b05776ad37617080120b6f8131ff6e31659c38ebcc8ecc493  src/VFXComposer.Protocol/Commands/RunFocusedTestsCommand.cs
d3cda6c5e4cce448a1b9ee2ba01efdefb0b607f9c0bd5f08531c020c843b4fd2  src/VFXComposer.Protocol/Commands/SetPreviewPlaybackCommand.cs
ec9241f73c2d25fe0ed10521693f9a44069a8767853395fd8790290213a6ad02  src/VFXComposer.Protocol/Commands/ValidatePatchCommand.cs
c0c74796f688131f4f543c08955be95bb7f543e761f88fcfa0e119841c3b1d2f  src/VFXComposer.Protocol/Commands/ValidateRecipeCommand.cs
e177a8ff4453bcf818bdfd8d3be26e05bafe53db223b3df364ffb66499f8847a  src/VFXComposer.Protocol/Jobs/JobArtifact.cs
cccea6e3bf909463eebb188f122a6865980cc8ef504b6e2c6fb93cfb71381ede  src/VFXComposer.Protocol/Jobs/JobCompletion.cs
3a6741f91db5f56970b2cdd6884fdb3f2bc89ece8fd719f0fc966dc1279b2327  src/VFXComposer.Protocol/Jobs/JobContracts.cs
69f77b11c8e906e7ff9875173a533ed2fb181c76bd35045e77a9e43b36919cff  src/VFXComposer.Protocol/Jobs/JobCorrelation.cs
5790ddd830e96a5cfa9936095dbfd4fefe86cda67a69d4b2e526d8ea3df96cad  src/VFXComposer.Protocol/Jobs/JobEventEnvelope.cs
580f35b79f573f9c4a8b461a02f68572249222ac63073504fafb81663cb5d6c2  src/VFXComposer.Protocol/Jobs/JobLogEvent.cs
2c731365bf5d1179e18a02945d70795a0846149ece909023dbba1746175e4841  src/VFXComposer.Protocol/Jobs/JobProgress.cs
21ed502d573cb615674288f786dbf9730fbd1812caf707bcfa35b987d8442f8c  src/VFXComposer.Protocol/Jobs/JobWireVocabulary.cs
0936d301d40bcf777e73e8a197ffcd624ca167bf32f2d4c77176daf16abed0a7  src/VFXComposer.Protocol/Json/StrictWireCodec.cs
6f7128fd24a6f3af3f715e0ca43040e4760938c502188df275134399ecf54a03  src/VFXComposer.Protocol/WireSchemaRegistry.cs
```

## Validation, aggregates, and receipts

```text
dotnet build src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-restore -p:RestoreLockedMode=true
  PASS: 0 warnings, 0 errors

dotnet test src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=protocol-p3-recovery.trx" --results-directory .codex_tmp/WP-PROTOCOL-P3-RECOVERY/test-results
  PASS: 88 passed, 0 failed, 0 skipped

dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true
  PASS: 0 warnings, 0 errors

python .codex_tmp/WP-PROTOCOL-P3-RECOVERY/validate_phase3_schemas.py
  PASS: 14 Draft 2020-12 meta-valid schemas; 14 independent positive vectors; 10 command schemas reject 5 negatives each and 4 job schemas reject 6 each, including Int64 overflow
```

The three accepted build/test command logs retain captured stdout/stderr and each now ends with the exact durable line `exitCode=0`; the refreshed test run overwrote the same durable TRX path.

`RestoreLockedMode=true` together with `--no-restore` proves only that the builds accepted the property against already-restored assets. It does **not** revalidate the lock graph. The installed SDK rejects literal `dotnet build --locked-mode` with `MSB1001`; that diagnostic is retained separately.

The independent golden-vector generator uses Python standard-library canonical JSON and typed SHA-256 length-prefix calculation, rather than serializing from the .NET implementation. Protocol regressions cover duplicate/unknown/missing/wrong-kind/version/type/hash/correlation/path/authority negatives, plus the new overflow negatives.

| Root | Count | Final Ordinal aggregate |
| --- | ---: | --- |
| `src/VFXComposer.Protocol` | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| `src/VFXComposer.Protocol.Tests` | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| `docs/schemas/desktop` | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |

The final manifest replay confirms the 41 source entries and all three aggregates. Receipt SHA-256 values follow; the receipt list intentionally does not hash itself or this handoff.

```text
9d5c564958ff4d2eca785b8a9cc5b98b4ccf1aaed4920d5e029088bb7a39af48  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/changed-source-manifest.sha256
83ff437ab9d189016b5d8b0825533c9f1ce5bdebbd11f117354cdd254d44e8ed  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/final-ordinal-aggregates.txt
22a9ab8a54c02303e845a4d063d500a12a784c7a8de4a436a608db9ed27f91fc  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/forbidden-surface-scan.log
7ca27578576f8f2b037148c4c4f56faf20b5c16cda4e834c70f10837cfc8bfba  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/generate_phase3_schemas.py
85dd7bd9b4a2e6028c588e02516f51cd56def3ada525cbf9d86f5f6bb718e26e  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/generate_phase3_vectors.py
17c00cfba8821a61137a59c8eece78737339d7d2fbed0175b9ae2e0d42c5a64b  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/legacy-ui-hash-check.log
76e87c23755db93ed4bd1e43cd1a668110b6a9c41f5d7ca464d2fcb15c1f7a6f  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/literal-locked-mode-build-probe.log
b7fe16610c745f538cefa97fe9389d0073d2e4b053730dfa4d037e4250e381da  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/manifest-replay.log
0e06c1b66a68522b0e66c8d6349f24508be2c00f4014933abb44e25d2eea83ab  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/protocol-tests-build.log
5f322b9d9e2a8ca766178f5102eae7bc9a195466e86b8ba1c1809911ff3e41f8  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/protocol-tests.log
bc07975380cb30c1fa38e0e6159a491c7bfbe3cc61e5d0ee6786f90b6237075d  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/schema-validation.exit.txt
f20e91fb064fea4658b75992ee486535dd01efaf2035686e0eded2efd89da101  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/schema-validation.json
22bc3cd7899df8a9887ddb3d27e2ef7b26c51c2268feb525a70fb63fbd19b39f  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/solution-build.log
2439d1d259f264350f54b5ff17785dff03d79590180cb0596d0bd78d406ad710  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/test-results/protocol-p3-recovery.trx
2cf321e6c60ee29a9e621d53101d4fd2d7985d41833fb8b5c254542ca7cf2e9f  .codex_tmp/WP-PROTOCOL-P3-RECOVERY/validate_phase3_schemas.py
5729a7af52d317c9a0c4c76b2149c66e0888c529d9bd33e695cc940b06aff583  src/VFXComposer.Protocol.Tests/GoldenVectors/phase3-command-job-v1.json
14329a4840f614cd646c9cd9f5957470de67bedf810fe189321ff1282b5dec2d  src/VFXComposer.Protocol.Tests/bin/Release/net8.0/VFXComposer.Protocol.Tests.dll
989784bf1e50f6bfe0751e3e4001ce2ed5b43ef0c234009bb262c7e721902554  src/VFXComposer.Protocol/bin/Release/net8.0/VFXComposer.Protocol.dll
```

The Phase-3 scan is intentionally limited: it found no new Phase-3 runtime process/I-O/transport/listener surface and no public capability-or-authority API surface. It does not claim that all Protocol source lacks generic `Process` or delegate tokens; pre-existing registry strings and private implementation detail are outside that claim. The closed `commandCapability` scalar is deliberate wire vocabulary, not a runtime capability API. The independent legacy Unity UI read-only check matched all four designated hashes.

## Proved, non-proved, and terminal audit

Proved: immutable DTO/schema parity; strict ingress; exact P3 long-range parity; duplicate/unknown/missing/wrong-kind/version/type/hash/correlation/path/authority and overflow negatives; 14-schema meta/positive/negative validation; independent vectors; release build/test; source-manifest and aggregate replay; bounded forbidden-surface scan; and legacy UI non-drift.

Not proved: executable Broker/Worker behavior, actual lease enforcement, confirmation/authorisation decisioning, runtime transitions, end-to-end dispatch, Unity UI behavior, persistence/project mutation, transport security, or external interoperability.

- P0: 0 open findings. The independent audit found no source P0/P1 issue; this package remains wire-contract-only.
- P1: 0 open findings. The positive-`Int64` schema/codec parity finding is corrected and covered; lock-graph validation is explicitly not claimed.
- P2: 0 open findings. Aggregate evidence, handoff wording, and scan scope were corrected; handoff self-hash remains intentionally external.

**STOPPED.** Frozen pending independent controller audit. No follow-on package was started.
