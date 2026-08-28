# W9/W10/W16 风格专项 next-candidate 实施记录

状态：`NEXT_CANDIDATE_VISUAL_PENDING`
日期：2026-08-25
边界：本记录说明新的源码候选、隔离 shadow 构建、定向/共享机器门禁与独立终审；机器 GO 不替用户签署视觉结论，不覆盖旧 Prefab、旧 Scene、旧 Manifest、旧报告或任何 formal evidence/write-once 路径。状态继续为 `NEXT_CANDIDATE_VISUAL_PENDING`。

## 1. 历史结论与独立身份

旧 `VFXPREVIEW_Style2D.unity`、`VFXPREVIEW_Style3D.unity`、`VFXPREVIEW_StylePack2.unity` 及其 32 个旧 Generated 条目继续保持 `rejected`；原批量拒绝原文仍在 `W9_W10_W16_STYLE_SPECIALS_REPORT.md`。新候选使用 compiler `style-special-next-candidate-1`，所有 effect id 固定为 `<old-id>_next_candidate`，因此不会命中旧 Prefab/Manifest 路径。

隔离 shadow 中已物化的独立 Scene：

- W9：`Assets/VFX/Preview/VFXPREVIEW_Style2D_NextCandidate.unity`
- W10：`Assets/VFX/Preview/VFXPREVIEW_Style3D_NextCandidate.unity`
- W16：`Assets/VFX/Preview/VFXPREVIEW_StylePack2_NextCandidate.unity`

## 2. 实际可观察载体

### W9：真实 Material 命中与离散管线

10 个条目加载正式 `Assets/VFX/Shared/Styles/Materials/MAT_Style_<token>.mat`，不新建本地 Material。每项由 3–5 个正式 Shared Mesh 组成；运行时每帧以 `MaterialPropertyBlock` 写入 `_PrimaryColor / _SecondaryColor / _AccentColor / _GlobalAlpha / _Phase / _Intensity / _GlitchOffset`。Pixel 与 Cartoon 相位分别按 12fps/18fps 离散；Ink 以非线性 bleed/flyaway 扩张。公开 `MaterialHitCount`、`LastMaterialPhase`、`LastMaterialIntensity`、`VisibleRendererCount`，测试不会以标签或 `IsAlive` 代替实际材质状态。

### W10：生命周期与时序

10 个条目统一执行 `Anticipation → MaterialHit → Sustain → Dissolve`，但采用独立 motion profile：Explosion、SustainedPlume、MuzzleFlash、HoloBarrier、HoloScan、GlitchBlink、RitualSummon、SoulDrain、DemonEruption。摄魂束为真实 9 点 `LineRenderer`，其余为真实 MeshRenderer 拓扑；公开 `CurrentPhase`、`PhaseTransitionCount`、`MaterialHitCount`、`PeakVisibleRendererCount` 和持续循环次数。Descriptor 冻结每项 `duration / releaseNormalized / sustainEndNormalized`，使枪口焰硬清、仪式顺序点亮、闪现可见空窗、逆流束等能被机器按时间观察。

### W16：六组真实 A/B 组合示例

12 项按 `new, variant` 相邻排序为 lowpoly、crystal、candy、cosmic、steampunk、ghost 六组。每侧都序列化 `pairFamily / pairRole / sourceBaseId`，并由 style Material、不同 Mesh 组合、motion profile 与 `semanticCode` 共同形成 `CombinationSignature`；测试要求同组 style token 相同、两侧 signature 不同且都至少有三个真实可见 Renderer。

## 3. Preview 与预算不变量

- W9/W10：5 × 2；W16：4 × 3，W16 每一组 A/B 在相邻单元。W10 的 Preview scheduler 在每次 replay 间交替正视与 `(17°, -18°)` 斜视，保留原计划双视角检查。
- 每单元独占 layer 8–19 与 Camera culling mask；effect viewport 与 Overlay label safe band 不相交，硬裁剪防止跨格和遮挡标签。
- 统一 local envelope：中心 `(0, 0.02, 0)`，尺寸 `1.92 × 1.24 × 0.72`；Runtime 可计算真实 Renderer bounds 并报告是否越界。
- 单条目上限：GameObject 9、Renderer 7、ParticleSystem 0、distinct shared Material 1。当前静态拓扑最大为 root + 6 carriers、6 Renderers、0 ParticleSystem、1 Material。
- Prefab 空闲态 Renderer 全关；`Play / Stop / ResetForPool` 清理 MPB 与变换，不实例化 Material。

## 4. 冻结 descriptor 清单

每个下列 source id 都对应 `Assets/VFX/Recipes/StyleSpecialsNextCandidate/<W9|W10|W16>/<source-id>_next_candidate.default.json` 及同路径 `.json.meta`：

- W9（10）：`pixel_burst_impact_2d`、`pixel_sword_slash_2d`、`pixel_heal_aura_2d`、`anime_smear_slash_2d`、`poof_smoke_spawn_2d`、`anime_charge_aura_2d`、`ink_slash_2d`、`ink_splash_impact_2d`、`ink_dragon_trail_2d`、`fireball_2d_pixel`。
- W10（10）：`real_explosion_impact_3d`、`smoke_plume_area_3d`、`muzzle_flash_impact_3d`、`holo_barrier_shield_3d`、`holo_scan_area_3d`、`glitch_blink_transform_3d`、`blood_ritual_spawn_3d`、`soul_drain_beam_3d`、`demon_eruption_impact_3d`、`prismatic_shield_3d_holo`。
- W16（12，按 A/B 顺序）：`poly_burst_impact_3d`、`boulder_projectile_3d_lowpoly`、`gem_lance_projectile_3d`、`crystal_shield_3d_crystal`、`candy_pop_impact_2d`、`healing_bloom_aura_2d_candy`、`nebula_orb_projectile_3d`、`summoning_portal_2d_cosmic`、`steam_vent_burst_impact_3d`、`volt_shield_3d_steampunk`、`phantom_wail_area_2d`、`spectral_trail_3d_ghost`。

## 5. 源码、隔离构建与机器结果

新增源码（每个 `.cs` 均有同路径稳定 `.cs.meta`）：

- Runtime：`Runtime/StyleSpecialsNextCandidate/StyleSpecialNextCandidateRuntimeEntry.cs`、`StyleSpecialNextCandidateCell.cs`、`StyleSpecialNextCandidatePreviewDriver.cs`；文件夹有稳定 `.meta`。
- Editor：`Editor/Style/StyleSpecialNextCandidateAuthoring.cs`。
- EditMode：`Tests/EditMode/StyleSpecialNextCandidateEditModeTests.cs`、`StyleSpecialNextCandidatePreviewSceneTests.cs`。
- PlayMode：`Tests/PlayMode/StyleSpecialNextCandidatePlayModeTests.cs`。

源码阶段先使用项目现有 Unity 2022.3.62f3c1 Bee response file 与随附 Roslyn，Runtime、Editor、EditMode、PlayMode 四程序集逐一静态编译，四项 exit 0；唯一 warning 是既有 `StyledVfxController.portalRadius/swirlSpeed` 两条 CS0414，新文件无静态编译 error/warning。该静态结果是进入隔离 Unity 前的基线；其后已经真实执行 Unity import/build/Test Runner，结果见 §5.2。

### 5.1 JSON / GUID / 禁写审计

- 32/32 descriptor 可独立 parse；分组为 W9 `10`、W10 `10`、W16 `12`，id `32/32` 唯一，carrier 数全部落在 `[3,6]`。六组 W16 family 均精确为 `new + variant`。排序后逐文件 SHA 串的集合 hash：`sha256:8ca053678d23858962542b8940245548757241a538ed4ef5e0859dcba2c3c3ba`。
- 本候选新增 44 个 `.meta`：缺 GUID `0`、候选内重复 `0`、与项目其余 `.meta` 碰撞 `0`。
- 新 compiler 静态扫描未出现 `DeleteAsset / MoveAsset / File.Write* / Directory.Delete / AssetDatabase.CreateAsset`，也不引用旧 StyleSpecial build 入口、旧 Scene 名、shadow、artifacts 或 S0a/S0b/S3。
- 隔离 shadow 已物化 32 个 production Prefab、32 份 strict Manifest 与 3 份目标 `*_NextCandidate.unity`；32 份 Recipe 同步并通过 build 核对。canonical `project` 中 `Assets/VFX/Generated/*next_candidate*` 目录数仍为 `0`，三份新 Scene 仍不存在，说明这些输出尚未晋升。
- 独立审计核对 67 份 production `.meta`：GUID `67/67` 唯一，且没有旧候选 GUID 引用；三份 Preview 的 `LegacyRuntime` header 与逐格 label 均完整，没有因字体回退修复丢失标题或条目标识。
- 三份旧 Scene 当前留档 hash 分别为 Style2D `sha256:154bdb01834ed1da85429fd856d7edc417ee5d99ebb2b1a670624c33d2bbf592`、Style3D `sha256:c8666ffa95e19d4fdfac5fca4aef69cccc7cc5e002232d29af29f0034a1686bb`、StylePack2 `sha256:2361b6644b5d7ddae40c7bde4afa57b60d83d358851e8958cfa44789e94518f1`。

七份源码 SHA-256：

| 文件 | SHA-256 |
|---|---|
| `Runtime/StyleSpecialsNextCandidate/StyleSpecialNextCandidateRuntimeEntry.cs` | `0b44bbe305a9799d480075b39665d954ed6acb4a01ba112138bd9bbbef92b1b9` |
| `Runtime/StyleSpecialsNextCandidate/StyleSpecialNextCandidateCell.cs` | `89681981cd7cfe0f3c21703e9995e6c9ea9b3749739d505fb91135121d2083b3` |
| `Runtime/StyleSpecialsNextCandidate/StyleSpecialNextCandidatePreviewDriver.cs` | `1b94df33c562d9c5ab2bbe6f108c9f376d7ec1dabe1db4e6556a8b6953675cd3` |
| `Editor/Style/StyleSpecialNextCandidateAuthoring.cs` | `670cc75a550fcbf1195bc58e7824b6aeafeeac171108e8036c36e527278d17e0` |
| `Tests/EditMode/StyleSpecialNextCandidateEditModeTests.cs` | `98bb766b40eb4748b6b4a36572a91594b46b2728b383bc09e53e70fd45c8bef6` |
| `Tests/EditMode/StyleSpecialNextCandidatePreviewSceneTests.cs` | `7bc995843cc02da124b216e84fb5dadbbeff4676cbd4b96559b2cefb2dd3bc55` |
| `Tests/PlayMode/StyleSpecialNextCandidatePlayModeTests.cs` | `187267e85ae2810321ac340b6e03ba54b41b3a6c66e015d505f2b40fdad41733` |

### 5.2 Unity Test Runner 与独立终审

隔离 shadow `.codex_tmp/w24-fresh-20260825-0628/project` 的最终 batch build 日志为 `.codex_tmp/style-special-next-candidate-results/build-r2.log`，exit 0。定向结果如下：

| 门禁 | 当前 XML | 结果 |
|---|---|---:|
| EditMode | `.codex_tmp/style-special-next-candidate-results/edit.xml` | 4/4 |
| Preview EditMode | `.codex_tmp/style-special-next-candidate-results/preview.xml` | 1/1 |
| PlayMode | `.codex_tmp/style-special-next-candidate-results/play.xml` | 4/4 |

另执行 16 份共享回归，聚合 `67/67`；定向 `9/9` 与共享回归合计 `76/76`，全部 `failed=0`。共享回归的当前 XML 保留在 `.codex_tmp/style-special-next-candidate-results/regressions/`。独立终审结论为 **GO，P0=0、P1=0**。

上述 `.codex_tmp` XML、日志和独立审计结论是本轮隔离 shadow 的操作证据，不属于旧 W9/W10/W16 候选的 historical formal evidence，也未写入 W24 formal/write-once evidence。旧报告中的 6/6、3/3、全量 Edit/Play XML 只证明旧候选；不能与本轮 next-candidate 的 `76/76` 合并、替换或解释成旧拒绝翻案。机器 GO 也不等于用户视觉签署，状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`。

## 6. 已执行的隔离 Unity入口与复现边界

本轮实际执行目标为 `.codex_tmp/w24-fresh-20260825-0628/project`；下列模板保留为相同隔离边界下的复现入口：

```powershell
$unityExe = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$isolatedProject = '<isolated-worktree>\project'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -executeMethod VFXComposer.Editor.Style.StyleSpecialNextCandidateAuthoring.BuildAllForBatch -quit -logFile '<absolute-log-dir>\style-special-next-candidate-build.log'
```

定向 filter：

| testPlatform | full filter | 预期 |
|---|---|---:|
| EditMode | `VFXComposer.Tests.EditMode.StyleSpecialNextCandidateEditModeTests` | 4/4 |
| EditMode（Preview） | `VFXComposer.Tests.EditMode.StyleSpecialNextCandidatePreviewSceneTests` | 1/1 |
| PlayMode | `VFXComposer.Tests.PlayMode.StyleSpecialNextCandidatePlayModeTests` | 4/4 |

本轮共享回归已按上述新类型进入共享 asmdef 编译面的风险执行，聚合结果见 §5.2；这些 filter 继续作为后续晋升前的复现清单：EditMode `W9W10W16StyleSpecialTests`、`StyleAndStudioTests`、`W1NextCandidateEditModeTests`、`W1NextCandidatePreviewSceneTests`；PlayMode `W9W10W16StyleRuntimeTests`、`StyledVfxRuntimeTests`、`W1NextCandidatePlayModeTests`，并包括 W-C1/W-C2/W-C3 与 W3–W8 的定向 Edit/Play filter。

## 7. 隔离构建后的精确同步边界

本节是未来晋升的白名单，不代表本轮已经同步；截至本报告回填，32 个 Prefab、32 份 Manifest 与 3 份 Preview Scene 仍只位于隔离 shadow，未晋升 canonical。

执行前同步本报告 §5 的 7 个 `.cs + .cs.meta`、`Runtime/StyleSpecialsNextCandidate.meta`、`Assets/VFX/Recipes/StyleSpecialsNextCandidate.meta`、`W9.meta/W10.meta/W16.meta`，以及 §4 精确列出的 32 个 `.json + .json.meta`。

执行成功后，仅同步每个 `<id>=<source-id>_next_candidate` 的：

- `Assets/VFX/Generated/<id>.meta`
- `Assets/VFX/Generated/<id>/VFX_<id>.prefab` 与 `.prefab.meta`
- `ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json`
- 上述三份 `*_NextCandidate.unity` 与各自 `.unity.meta`

不允许同步 `Library/Logs/UserSettings`，不允许把新输出覆盖或重命名为旧 id，也不触碰 W24 formal evidence、S0a/S0b/S3、shadow 或用户签署记录。
