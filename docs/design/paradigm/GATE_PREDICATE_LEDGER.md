# 门禁谓词登记表（GATE_PREDICATE_LEDGER）

状态：`ACTIVE`（T2c 单元 E 交付，2026-09-06）
上位规范：`COMPILER_BOUNDARY_V2.md` §7（156 谓词总表）、ADR-009 方法论（fail-closed + 显式豁免）、ADR-010 §10
机器强制：`AssetAllowListGateTests` / `GatePredicateLedgerTests`（EditMode）——**本表未登记的编号 = 审计 FAIL**；登记为`已实现`的行必须给出真实存在的测试名；`条件豁免`必须给出理由与到期卡号。fail-closed 三路（未立法族 / 无变体声明 / 资产不在白名单）**不可豁免**（COMPILER_BOUNDARY_V2 §7.5-5）。

## 0. 汇总

| 状态 | 数 |
|---|---|
| 已实现（EditMode 测试在库，含"样片面/清单面"等部分面实现——部分与否见备注） | 78 |
| T3待做（编译器 v2 接线 / 族落地时实现） | 75 |
| 条件豁免（显式，含到期卡号） | 3 |
| **合计** | **156** |

状态口径：状态列是**三值枚举**（`已实现` / `T3待做` / `条件豁免`），范围行（如 `GP-2 ~ GP-9`）按展开后的 id 数计入；一切限定语（"样片面""清单面""部分面"等）放在"谓词/测试名与备注"列，不进状态列。本表汇总行由 `GatePredicateLedgerTests.TheSummaryCountsEqualThePerRowStatusCounts` 机器自洽（汇总数字 ≠ 逐条计数即 FAIL）。`已实现`＝存在可运行的 EditMode 测试对该谓词的语义作构造性断言（可以是规格谓词的等价物或对样片/清单面的部分断言，细节在备注）；`T3待做`＝断言面（编译器 v2 产物 / sidecar 清单 / .vfx 模板）尚不存在；`条件豁免`＝断言面存在但按拍板显式豁免（§12）。GP-1~9 状态为 `T3待做`（.vfx 模板未产出、AllowList `vfxTemplates:[]` fail-closed），其豁免背景登记在 §12 但不占用`条件豁免`状态。

## 1. 材质族（31）

| 编号 | 测试名 | 状态 |
|---|---|---|
| SG-1 | — sidecar 清单机制未落（HLSL 路线下"子图"= include 库，见 T2B_IMPL_REPORT §3-1；等价物为 AllowList `subgraphs` 行 sha256 校验 `AssetAllowListGateTests.WL1_*`） （HLSL 路线 sidecar 等价物已由 WL-1 承载；.shadersubgraph sidecar 属 T3 若引入 Shader Graph 路线） | 条件豁免 |
| SG-2 | — （变体清单落地时） | T3待做 |
| SG-3 | — （变体清单落地时） | T3待做 |
| SG-4 | — （变体清单落地时） | T3待做 |
| SG-5 | `TechniqueFamilyBoundaryGateTests.ShaderLibrary_ZeroTextureSamplingProperties`（HLSL 等价物：库内 shader 属性零 Texture 型）+ `ParadigmSampleGateTests.Sample_NoFlipbook_NoTextureSampling` | 已实现 |
| SG-6 | — （图结构断言在 HLSL 路线无对应；linear 色彩空间由 PR-1 承载） | T3待做 |
| SG-7 | `TechniqueFamilyBoundaryGateTests.ShaderLibrary_AllShadersImport_AndCarryStyleStage`（三关键字 StyleStage 端口面逐 shader 一致） | 已实现 |
| SG-8 | — （组合清单落地时） | T3待做 |
| MG-1 | — 主图 4 张未立（HLSL 路线逐变体 shader 承载，见 T2B_IMPL_REPORT §2.1） （主图/变体分层属 T3 `SG_VfxLit` 引入时；当前逐变体 shader 的 URP 标签一致性由 ShaderLibrary_AllShadersImport 承载） | 条件豁免 |
| MG-2 | `TechniqueFamilyBoundaryGateTests.GlowShader_CarriesTheFullParameterSurface`（统一属性面探针，Glow 主图） （部分面；其余主图属 T3） | 已实现 |
| MG-3 | — | T3待做 |
| MG-4 | — | T3待做 |
| MG-5 | — | T3待做 |
| MV-1 | `ParadigmSampleGateTests.Sample_GlowStackPresent_AndBeatCoupled`（时间驱动输入非静态） （样片面；编译器谓词属 T3） | 已实现 |
| MV-2 | `TechniqueFamilyPresetTests.StylePreset_AppliesExactlyOneStyleStageKeyword` + `ParadigmSampleGateTests.Sample_CarriesExactlyTheCartoonStyleStage` | 已实现 |
| MV-3 | `TechniqueFamilyPresetTests.ElementPreset_AppliesPaletteToMaterial`（Palette 赋值非负有限） | 已实现 |
| MV-4 | `TechniqueFamilyPresetTests.ElementSamples_HotStopSatisfiesHG2`（HG-1 台阶比 ≥2.0 由 `VfxHdrGrade` 默认链 + Validate 承载） | 已实现 |
| MV-5 | `TechniqueFamilyPresetTests.ElementSamples_HotStopSatisfiesHG2`（HG-2 hot ≥1.5） | 已实现 |
| MV-6 | `AssetAllowListGateTests.WL1_EveryListedEntryResolvesToAnExistingAssetWithMatchingGuidAndSha256`（shader GUID 白名单命中） （清单面；产物面 WL-4 属 T3） | 已实现 |
| MV-7 | — （sampleCost 求和需变体清单） | T3待做 |
| MV-8 | `TechniqueFamilyBoundaryGateTests.RefractionShader_DeclaresBothRoutes` | 已实现 |
| MV-9 | — （2D 排序谓词需编译器产物） | T3待做 |
| GL-1 | `ParadigmSampleGateTests.Sample_GlowStackPresent_AndBeatCoupled`（Glow 子节点存在且计数正确） （样片面） | 已实现 |
| GL-2 | `ParadigmSampleGateTests.Sample_GlowStackPresent_AndBeatCoupled`（Glow 材质 = GlowStack additive） （样片面） | 已实现 |
| GL-3 | — （半径几何级数需编译报告） | T3待做 |
| GL-4 | — | T3待做 |
| GL-5 | `ParadigmSampleGateTests.Sample_ComponentsAreInsideTheRecognizedClosedSet`（Glow 子节点组件闭集内） （样片面） | 已实现 |
| GL-6 | — | T3待做 |
| GL-7 | `ParadigmSampleGateTests.Sample_FitsThePmTierBudget`（layerCount 计入 C_over 档位判定） （样片面） | 已实现 |
| PR-1 | `TechniqueFamilyBoundaryGateTests`（EditMode 全套跑在 Linear 色彩空间工程内；显式断言属 T3 编译器接线） （工程设置已是 Linear；显式 PlayerSettings 断言随编译器 v2 落地，卡号 T3） | 条件豁免 |
| PR-2 | `AssetAllowListGateTests.WL2_EveryAssetUnderTheGovernedRootsIsListed` + R8016（VfxOutputAuditor 沿用） | 已实现 |

## 2. GPU 粒子族（9）

| 编号 | 测试名 | 状态 |
|---|---|---|
| GP-1 | — （.vfx 模板未产出，T2B_IMPL_REPORT §6-1；fail-closed：AllowList `vfxTemplates` 为空数组，任何 .vfx 引用即 WL-1 拒绝） | T3待做 |
| GP-2 ~ GP-9 | — （同上） | T3待做 |

## 3. CPU 粒子族（10）

| 编号 | 测试名 | 状态 |
|---|---|---|
| CP-1 | `TechniqueFamilyCpuParticleTests.Registry_HasExactlyTheFourteenSpecTemplates` | 已实现 |
| CP-2 | `TechniqueFamilyCpuParticleTests.AllTemplates_Configure_EnforcesModuleDiscipline`（模块允许/禁止表逐模板断言） | 已实现 |
| CP-3 | `TechniqueFamilyCpuParticleTests.AllTemplates_TheoreticalPeak_IsPositive` | 已实现 |
| CP-4 | `TechniqueFamilyCpuParticleTests.CollisionTemplates_NeverEnableDynamicColliders` | 已实现 |
| CP-5 | `TechniqueFamilyCpuParticleTests.AllTemplates_Configure_EnforcesModuleDiscipline`（textureSheetAnimation=false 构建期强制）+ `ParadigmSampleGateTests.Sample_NoFlipbook_NoTextureSampling` | 已实现 |
| CP-6 | `TechniqueFamilyCpuParticleTests.AttractTarget_UsesLocalSpaceNegativeRadial` | 已实现 |
| CP-7 | `TechniqueFamilyCpuParticleTests.InstantRephase_UsesShortLifetimeRebirth` | 已实现 |
| CP-8 | — （产物面：SubEmitter 峰值合并计入 C_cpu 需编译器） | T3待做 |
| CP-9 | — （渲染器排序面需编译器产物） | T3待做 |
| CP-10 | — （Lights/ExternalForces 模块在产物中禁用需编译器产物；构建期强制已在 Configure） | T3待做 |

## 4. 伪粒子 UGUI（5）

| 编号 | 测试名 | 状态 |
|---|---|---|
| UI-1 ~ UI-5 | — （F 类 `mat_ui_*` 与 `SG_VfxCanvas` 属 P2 批次，T2B_IMPL_REPORT §6-2） | T3待做 |

## 5. 网格族（12）

| 编号 | 测试名 | 状态 |
|---|---|---|
| MS-1 | `TechniqueFamilyMeshAndLightTests.GeneratorRegistry_HasExactlyTheTwentySpecGenerators` + `AssetAllowListGateTests.WL3_EveryMeshGeneratorRowPinsTheGeneratorSourceHash`（无清单拒绝 = fail-closed 第 2 路） | 已实现 |
| MS-2 | `TechniqueFamilyMeshAndLightTests.AllGenerators_AreSeedDeterministic` + `AllGenerators_DifferentSeedsChangeRandomizedOutput` | 已实现 |
| MS-3 | `TechniqueFamilyMeshAndLightTests.AllGenerators_3D_ProduceValidMeshes` / `AllGenerators_2D_ProduceValidMeshes`（AssertMeshContract：通道完备/索引合法/法线有限） | 已实现 |
| MS-4 | `TechniqueFamilyMeshAndLightTests.AllGenerators_HonourTightVertexBudget` | 已实现 |
| MS-5 | `TechniqueFamilyMeshAndLightTests.AllGenerators_3D_ProduceValidMeshes`（包围盒非退化断言） | 已实现 |
| MS-6 | `TechniqueFamilyMeshAndLightTests.VoronoiPrefracture_FragmentsMarkCutFaces` | 已实现 |
| MS-7 | `TechniqueFamilyMeshAndLightTests.RadialSpikeArray_BimodalLengthsProduceLongAndShortSpikes` | 已实现 |
| MS-8 | `TechniqueFamilyMeshAndLightTests.JaggedPolyline_AnchorsBothEndpoints` | 已实现 |
| MS-9 | — （产物 sidecar `.mesh.json` 需编译器写入面） | T3待做 |
| MS-10 | — （同上） | T3待做 |
| MS-11 | — （Trail/Line 运行时组件产物面） | T3待做 |
| MS-12 | — （Cloth 产物面） | T3待做 |

## 6. 物理（9）

| 编号 | 测试名 | 状态 |
|---|---|---|
| PH-1 | `ParadigmSampleGateTests.Sample_ComponentsAreInsideTheRecognizedClosedSet`（刚体/碰撞体在闭集内且仅预破碎层） （样片面） | 已实现 |
| PH-2 | `VfxControllerV2Tests.ResetForPool_RestoresDebrisToKinematicInitialPose`（初始 TRS 烘入数组 + kinematic 复位） | 已实现 |
| PH-3 | `ParadigmSampleGateTests.Shield_HitIntegrityAndBreakInterfaces_Work`（break 后碎块受控释放） （样片面） | 已实现 |
| PH-4 ~ PH-9 | — （碰撞矩阵/excludeLayers/Cloth 系数面需编译器产物） | T3待做 |

## 7. 局部光族（9）

| 编号 | 测试名 | 状态 |
|---|---|---|
| LT-1 | `ParadigmSampleGateTests.Sample_LocalLightIsPresent_AndDriven`（L-1 等价：光组件 + 节拍器同节点） （样片面） | 已实现 |
| LT-2 | `VfxLightBeatTests.Breathe_OscillatesWithinDepthBand` / `Strobe_IsHardOnOff`（L-2 等价：非 steady 模式曲线非常量） | 已实现 |
| LT-3 | — （烘档材质发光绑定需编译器） | T3待做 |
| LT-4 | `ParadigmSampleGateTests.Sample_GlowStackPresent_AndBeatCoupled`（L-4 等价：glow 与节拍同源） （样片面） | 已实现 |
| LT-5 | `VfxLightBeatTests.DecayProgress_DrivesBeatTowardsZero` | 已实现 |
| LT-6 | `VfxLightBeatTests.Steady_IsConstantOne` + `Breathe_OscillatesWithinDepthBand`（闪烁模式语义） | 已实现 |
| LT-7 | `TechniqueFamilyPresetTests.StyleSamples_LoadAndValidate`（`VfxStylePreset.Validate`：像素禁阴影） | 已实现 |
| LT-8 | `VfxLightBeatTests.FrameRateQuantization_SnapsTime` + `IntensityQuantization_LimitsDistinctLevels`（风格量化） | 已实现 |
| LT-9 | — （产物面：光数计入 C_light 档位判定需编译器报告） | T3待做 |

## 8. 风格（24）

| 编号 | 测试名 | 状态 |
|---|---|---|
| ST-1 | `TechniqueFamilyPresetTests.StylePreset_AppliesExactlyOneStyleStageKeyword`（关键字互斥） | 已实现 |
| ST-2 | `TechniqueFamilyPresetTests.StyleSamples_LoadAndValidate` | 已实现 |
| ST-3 | `ParadigmSampleGateTests.Sample_CarriesExactlyTheCartoonStyleStage`（样片全材质恰一关键字） | 已实现 |
| ST-4 ~ ST-6 | — （风格越权检测需编译器参数合并链） | T3待做 |
| CT-1 | `TechniqueFamilyBoundaryGateTests.ShaderLibrary_AllShadersImport_AndCarryStyleStage`（cartoon 分支端口面） （端口面；数值面 T3） | 已实现 |
| CT-2 ~ CT-8 | — （卡通数值谓词需截帧/产物面；T2c-C 质量收敛已按 STYLE_IMPL 2.8-1 阶梯环落地，见提交 242223dc） | T3待做 |
| PX-1 ~ PX-5 | — （像素维度样片属 T4 铺开；shader 侧量化机制已在库，`VfxStyleStage.hlsl`） | T3待做 |
| PX-6 | `TechniqueFamilyBoundaryGateTests.Px6_ConditionalPredicate_SkipsMissingCategories` + `Px6_ConditionalPredicate_FlagsPixelMeshWithoutSnap` + `Px6b_*` 双向（拍板 #16 条件式三分支） | 已实现 |
| PX-7 ~ PX-10 | — | T3待做 |

## 9. 编译器侧（32）

| 编号 | 测试名 | 状态 |
|---|---|---|
| WL-1 | `AssetAllowListGateTests.WL1_EveryListedEntryResolvesToAnExistingAssetWithMatchingGuidAndSha256` | 已实现 |
| WL-2 | `AssetAllowListGateTests.WL2_EveryAssetUnderTheGovernedRootsIsListed` | 已实现 |
| WL-3 | `AssetAllowListGateTests.WL3_EveryMeshGeneratorRowPinsTheGeneratorSourceHash` | 已实现 |
| WL-4 | — （产物 GUID 命中白名单需编译器产物） | T3待做 |
| WL-5 | — R8013/E601 语义由 `VfxOutputAuditor` 沿用（旧根已换新），产物级测试属 T3 | T3待做 |
| WL-6 | `AssetAllowListGateTests.WL6_TheAllowListIsOutsideEveryBuildWriteSurface` | 已实现 |
| CM-1 | — （编译报告 vs 实测一致性） | T3待做 |
| CM-2 | `TechniqueFamilyBoundaryGateTests.CostModel_Evaluate_FlagsViolations_AndOverdrawDualThreshold` + `ParadigmSampleGateTests.Sample_FitsThePmTierBudget`（八项上限 + C_over 双阈值） | 已实现 |
| CM-3 | `ParadigmSampleGateTests.Sample_FitsThePmTierBudget`（分项结构预算样片面） （样片面；产物面 T3） | 已实现 |
| CM-4 | — （六档接口一致性需六档产物） | T3待做 |
| CM-5 | — （W404 降级登记需编译报告） | T3待做 |
| CM-6 | `ParadigmSampleGateTests.SeedDerivation_IsDeterministic_AndDimensionSeparated`（确定性种子面）；产物字节级 sha 一致属 T3 （种子面） | 已实现 |
| RT-1 | `ParadigmSampleGateTests.Sample_ControllerContract_PhasesAndSortedEvents`（根唯一 VfxController = IVfxRuntimeEntry） | 已实现 |
| RT-2 | `VfxControllerV2Tests.PhaseTable_IsArbitrary_NotAFixedEnum` + `Sample_ControllerContract_PhasesAndSortedEvents`（阶段 id 标准序） | 已实现 |
| RT-3 | — （必需层激活需原型目录接线） | T3待做 |
| RT-4 | `ParadigmSampleGateTests.Sample_ControllerContract_PhasesAndSortedEvents`（事件表已排序）+ `VfxControllerV2Tests.SendEvent_UnknownId_ReturnsFalse_NoThrow` | 已实现 |
| RT-5 | `ParadigmSampleGateTests.DissolveOut_ProgressInterface_AndEmptyTargetDegradation`（空引用退化确定行为） （样片面；fallbackMode 声明面 T3） | 已实现 |
| RT-6 | `VfxControllerV2Tests.ResetForPool_RestoresTransform_AndPhaseState` + `ResetForPool_RestoresDebrisToKinematicInitialPose` （VisualEffect 项随 GPU 粒子 T3） | 已实现 |
| RT-7 | `ParadigmSampleGateTests.Sample_IsSelfContained_ZeroExternalReferences` | 已实现 |
| RT-8 | `VfxLightBeatTests.Configure_SetsDriverKindAndTargets`（maxSustainSeconds 默认 >0） | 已实现 |
| RT-9 | — （编排内联需编排编译器） | T3待做 |
| BD-1 | `TechniqueFamilyBoundaryGateTests.BindingKeys_ResolveRoundTrip_AndUnknownRejected` | 已实现 |
| BD-2 | `VfxControllerV2Tests.ParameterBlock_DisabledLayerBinding_IsNoOp`（targetIndex=-1 空操作） （变体清单声明面 T3） | 已实现 |
| BD-3 | `RuntimeAssemblyBoundaryTests`（Runtime 程序集零 UnityEditor 引用；System.Reflection 扫描属 T3 收严） （部分面） | 已实现 |
| BD-4 | `TechniqueFamilyBoundaryGateTests.BindingKeys_AreUniqueThreeSegment_AndContentFree`（键面内容自由）+ 绑定表 nameId 整数化（`BindingKeyIds_ResolverAndDispatcher_ShareOneIdSpace`） | 已实现 |
| BD-5 | — （标准参数全层覆盖需编译器绑定生成） | T3待做 |
| AU-1 | `ParadigmSampleGateTests.Sample_ComponentsAreInsideTheRecognizedClosedSet` + `TechniqueFamilyBoundaryGateTests.RecognizedAndExcludedComponentSets_DoNotOverlap` | 已实现 |
| AU-2 | `VfxOutputAuditor`（E8008/E8009/E8010 沿用，`ProductionRulesTests` 覆盖审计管线） | 已实现 |
| AU-3 | `VfxOutputAuditor`（R8007/R8017/R8018 沿用） | 已实现 |
| AU-4 | `VfxOutputAuditor`（E8005/E8006 沿用） | 已实现 |
| AU-5 | `VfxOutputAuditor`（R8015 沿用） | 已实现 |
| AU-6 | `VfxOutputAuditor`（R8016 沿用） | 已实现 |

## 10. 折射可选层（5）

| 编号 | 测试名 | 状态 |
|---|---|---|
| RF-1 | `TechniqueFamilyBoundaryGateTests.RefractionShader_DeclaresBothRoutes`（multi_compile_local 双路；含 `VFX_LiquidBlobRefract` 组合变体） | 已实现 |
| RF-2 | — （产物 Probe 接线） | T3待做 |
| RF-3 | — （编译报告声明） | T3待做 |
| RF-4 | — （Queue 偏移需产物） | T3待做 |
| RF-5 | — （2D 初值面需产物） | T3待做 |

## 11. 画廊（10）

| 编号 | 测试名 | 状态 |
|---|---|---|
| GA-1 | `GallerySceneGateTests.GalleryScenes_Exist_AndAreNotInBuildSettings` | 已实现 |
| GA-2 | `GallerySceneGateTests.GalleryScenes_SatisfyStructuralPredicates`（检视光默认 disabled） | 已实现 |
| GA-3 | `GallerySceneGateTests.GalleryScenes_SatisfyStructuralPredicates` | 已实现 |
| GA-4 | `GallerySceneGateTests.GalleryVolumeProfile_HasExactlyBloomAndTonemapping`（Bloom 可开关） | 已实现 |
| GA-5 | `GallerySceneGateTests.GalleryPageSets_Validate` + `ParadigmSampleGateTests.GalleryPageSets_ResolveExactlyTheSamplePaths` | 已实现 |
| GA-6 | — （翻页交互 PlayMode 谓词） | T3待做 |
| GA-7 | — | T3待做 |
| GA-8 | `T2cParadigmSampleCaptureTests`（PlayMode：LoadSceneInPlayMode 截帧走真实场景） （PlayMode） | 已实现 |
| GA-9 | `GallerySceneGateTests.GalleryAssembly_IsNotReferencedByRuntime` + `DependencyRoots_ExcludeGallery_AndRetiredRoots` | 已实现 |
| GA-10 | `GallerySceneGateTests.GalleryScenes_SatisfyStructuralPredicates`（正交/透视相机环境差异） | 已实现 |

## 12. 显式豁免表（ADR-009 §4 同构）

| 谓词 | 对象 | 理由 | 到期卡 |
|---|---|---|---|
| SG-1 | 材质族 include 库 | HLSL 路线无 .shadersubgraph；sidecar 等价物 = AllowList sha256 行（WL-1 承载完整性） | T3（若引入 Shader Graph 主图则立 sidecar） |
| MG-1 | 主图 4 张 | 逐变体 shader 承载，URP 标签一致性已由 ShaderLibrary_AllShadersImport 查 | T3（`SG_VfxLit` 引入时） |
| PR-1 | PlayerSettings.colorSpace | 工程已 Linear（HG 数值谓词全部在 Linear 语义下通过）；显式断言随编译器 v2 | T3 |
| GP-1~9 | GPU 粒子族 | .vfx 模板未产出（依赖变更需人工裁定，T2B_IMPL_REPORT §6-1）；fail-closed：`vfxTemplates: []`，任何 .vfx 即拒 | T3（引包后逐模板作 VFX Graph 版） |

豁免纪律（§7.5）：豁免项仍跑谓词（对应测试对豁免面做"跳过/命中"双向断言，如 PX-6 三分支）；fail-closed 三路不可豁免——本表各行豁免均不属于三路。状态口径注：前三行（SG-1/MG-1/PR-1）在 §1 状态列计为`条件豁免`（共 3）；GP-1~9 行是豁免背景登记，其状态列为 `T3待做`（.vfx 断言面不存在，fail-closed 已由空 `vfxTemplates` 承载），不占`条件豁免`计数。

## 13. 台账 #12：C_over 系数校准（T2c 单元 E 终值）

**静态估计**（`VfxTechniqueFamilyBoundary.EstimateOverdraw`，32×32 p95，碎片组并集，粒子按发射体+速度×寿命膨胀）对 18 张范式样片逐一量取（2026-09-06，`test-results/t2c-cover-calibration.csv`）：

| 样片（原型_元素） | 3D | 2D |
|---|---|---|
| shield_ice / shield_lightning / shield_poison | 5 / 5 / 5 | 5 / 5 / 5 |
| chain_link_ice / chain_link_lightning / chain_link_poison | 1 / 4 / 1 | 1 / 4 / 1 |
| dissolve_out_ice / dissolve_out_lightning / dissolve_out_poison | 1 / 5 / 1 | 1 / 4 / 1 |

**截帧实测校准面**（T2c-C 截帧管线的 overdraw 代理 = 前景覆盖率，`t2c-review/metrics.json`）：3D 判定页 0.710、2D 判定页 0.070（帧 60，双 Bloom pass 差 <0.01%——代理与 Bloom 无关，符合预期）。

**校准结论与终值**：
1. 两个量纲不同（静态 = 透明层堆叠数，实测代理 = 屏幕前景覆盖率），按规格 §4.4 校准的对象是**膨胀系数**：粒子包围盒膨胀 `速度×寿命×2.0`（直径化）在 18 样片上未产生任何"估计 < 实测可见堆叠"的反例（人工核对 contact-sheet：最重的 shield 系样片可见堆叠 4~5 层，静态估计 5，保守但不过冲）。
2. **膨胀系数终值 = 2.0（维持），网格 32×32（维持），p95（维持）**；无系统性偏差需要修正，白名单 schema 版本不升。
3. 全部样片 C_over ≤ 5 < PM 档上限 6：零 W403、零 E300。lightning 元素（chain/dissolve 4~5）系其锯齿层+辉光层数更多，属预期形态差异。
4. 复核轨道：估计是保守上界而非帧率预测（§4.4 诚实边界）；T3 编译器接线后 CM-1 将把该估计写入编译报告并与实测面持续对照。
