# A1–A8 机器验收矩阵 — 内部 MVP 0.1.0

这些不是文字替代执行。每一条有正常（非 `Explicit`）的 EditMode/PlayMode 测试；运行 `cmd /c tools\run-tests.bat EditMode` 和 `cmd /c tools\run-tests.bat PlayMode` 会实际执行它们。历史一次性 S9 证据仍保持 Explicit，未被计为本矩阵通过。

| Scenario | Executed release test / evidence | Machine assertions |
| --- | --- | --- |
| A1 默认 2D | `S11ReleaseAcceptanceTests.A1_Default2DFireball_BuildsFormalPrefabWithAllStages` | 真正 Build、Launch/Travel/Impact、Runtime controller。 |
| A2 重复构建 | `S11ReleaseAcceptanceTests.A2_Repeated2DAnd3DBuilds_AreCanonicalIdempotentAndKeepPrefabGuids` | 2D/3D 第二次为 `Unchanged`，GUID 不变，输出目录递归 filename+SHA-256 集合完全相同。 |
| A3 非法模板 | `...A3_A4_InvalidTemplateAndOutOfRangeParameter_AreBlockedWithoutGeneratedWrites` | `E308` 精确稳定路径、Generated SHA-256 集合不变。 |
| A4 参数越界 | `...A3_A4_InvalidTemplateAndOutOfRangeParameter_AreBlockedWithoutGeneratedWrites` | `E314` 精确稳定路径、Generated SHA-256 集合不变。 |
| A5 模板保护 | `...A5_TemplateProtection_BuildAndPatchFailuresLeaveTemplateBytesUntouched` | 非法 Build、真实失败 Patch 和真实成功 Patch 后，Templates 递归 SHA-256 集合均不变。 |
| A6 火星减半 | `...A6_EmbersHalfPatch_UpdatesRevisionHistoryAndOnlyTargetModuleImpact` | 真实 Patch、revision `1→2`、解析 history、rate `18→9`；只有 `embers` 模块影响项为 `Update`，其余为 `Unchanged`。 |
| A7 Runtime/Preview | `...A7_RuntimeAndFixedPreviews_ArePresentWithoutEditorAssemblyInRuntime` + `...A7_PlayerBuildPreflight_SerializesFourFormalAndS12EvidencePreviewScenesToAnExternalTemporaryBuild` + `S11FormalGeneratedRuntimeTests` (PlayMode) | 四个固定 Preview（2D/3D fireball、formal Slash、AI-validated Slash evidence）在 system temp 实际 Player Build；PlayMode 加载其序列化场景中的保留正式 Prefab 并播放 Launch/Travel/Impact/Stop。 |
| A8 3D/Unsupported | `...A8_ThreeDUsesSharedSemanticsAndReportsUnsupportedDimensionMismatch` + `S10ThreeDRuntimeTests` (PlayMode) | 比较相同 stage/module semantic IDs、trigger、duration、enabled 和 attach graph；维度错配 `E310`，3D 生命周期运行。 |

金样证据继续位于 `docs/s5-evidence/`、`docs/s7-evidence/` 与 `docs/s10-evidence/`；S11 以结构/参数/依赖和哈希为金样层级，遵从 S1 对 Unity 序列化非字节稳定的结论，不把 YAML 字节差误报为不确定性。
