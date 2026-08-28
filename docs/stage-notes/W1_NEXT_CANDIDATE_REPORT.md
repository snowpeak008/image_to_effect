# W1 风格底座 next-candidate 实施记录

状态：`W1_NEXT_CANDIDATE_VISUAL_PENDING`  
日期：2026-08-25  
边界：本记录说明新的 W1 源码候选、隔离 Unity 机器门禁与共享回归；不产生用户视觉签署，也不改写旧候选的拒绝证据。

## 1. 历史结论与本轮授权

旧候选 `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples.unity` 继续保持 `rejected`。用户原话逐字保留为：“拒绝；W1样例尺寸未统一，Stylized基准过大、Dark frost过弱，Holy新星与Holo射线跨格、裁切并遮挡标签；场景缺少格内约束和8种style token的完整视觉对比，整体未达到商用级视觉完成度。”

本轮另建全新候选，不覆写旧 Scene、旧 Generated 输出、旧报告段落或任何 evidence/shadow/write-once 路径。新候选的源码入口为 `VFXComposer.Editor.Style.W1NextCandidateAuthoring`，compiler version 为 `w1-style-next-candidate-1`；未来构建的独立 Scene 路径为 `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity`。

## 2. 新候选的固定比较契约

- 画布固定为 4 × 3：前 8 格逐一展示 `stylized/cartoon/pixel/inkwash/semireal/holo/dark/neon`，第 9–11 格展示三份“能力 + 皮肤”，第 12 格只显示有界契约图例。
- 11 个效果格均以 root scale `1.00` 运行，并声明同一 local envelope：中心 `(0, 0.02, 0)`、尺寸 `1.56 × 1.08 × 0.50`。旋转载体按全角度对角线收紧；Nova 的 Ring/Burst 从归一化基准尺寸扩张，不再覆盖为原始 Mesh scale。
- 每格使用独立 layer（8–18）和只渲染该 layer 的正交 Camera；Camera `rect` 就是该格 effect viewport，因此跨格内容会被硬裁剪在本格。
- 标签位于单独的 Screen Space Overlay safe band。每个 effect viewport 与全部 11 个 label viewport 均不相交；效果 Camera 也不渲染 UI layer。
- 所有条目使用 2.00 秒确定性播放，Preview driver 明确执行 `replay → 0.30 s clean gap → replay`；clean gap 内所有 renderer 必须关闭。

## 3. 八种 token 的真实差异载体

每个 token 格使用三个真实 `MeshRenderer`，复用既有正式 Shared Material/Mesh，不创建格内材质副本。序列化 `visualSignature` 不包含 token/label，而由 Shader、Material 的 `_StyleMode/_Outline/_ShadingSteps/_NoiseScale/_DstBlend`、Mesh 顺序和运行时 timing profile 共同组成。

| token | Shared Shader / mode | 三层载体 | timing profile |
|---|---|---|---|
| `stylized` | LayeredRamp / 0 | Ribbon + Ring + Burst | PaintedSweep |
| `cartoon` | LayeredRamp / 1 | Burst + Ring + Quad | CelBounce |
| `pixel` | PixelQuantize / 3 | Quad + Shard + Burst | PixelStep |
| `inkwash` | InkBrush / 4 | Ribbon + Quad + Ring | InkBleed |
| `semireal` | SoftNoise / 2 | Quad + Ring + Burst | SoftTurbulence |
| `holo` | HoloFresnel / 5 | Ring + Quad + Burst | HoloScan |
| `dark` | DissolveEdge / 6 | Ring + Burst + Shard | RitualPulse |
| `neon` | LayeredRamp / 7 | Ring + Ribbon + Burst | NeonBeat |

所有八格使用相同三层 target-size 数组，而不是按 token 放大根节点。Dark 使用更高的 `baseIntensity=1.24` 和运行时能量下限；该差异由 Runtime 状态与测试读取，不依赖文字标签。

现有 SoftNoise、PixelQuantize、InkBrush、HoloFresnel、DissolveEdge Shader 入口继续复用其既有 Properties 与 LayeredRamp fallback/mode 分支；本轮未改共享 Shader。实际 Game View 可辨性仍必须在未来隔离 Unity 渲染后检查。

## 4. 三份 trace-backed“能力 + 皮肤”

| 格 | 真实执行载体 | 可观察行为 |
|---|---|---|
| fan + wave / cartoon | 固定 5 个 Shard MeshRenderer | `CapabilitySampler` 的 5 条 `on_emit/fan` 方向直接决定朝向；每帧位置来自 wave trace，并映射/钳制到统一 envelope |
| charge + occlude / holo | 9 点 LineRenderer + Charge Ring | charge trace 的 width stage 改变真实线宽和 glyph scale；0.90 秒障碍距离切换改变真实末端，记录 occlusion transition |
| telegraph + nova / holy palette | Telegraph Ring + Nova Ring + 单个 12-triangle Burst MeshRenderer | 0.65 秒 warning 后切换真实 renderer；Burst 的 12 个三角射线与 trace 的 12 条 `on_emit/ring` 事件对应，避免用 12 个临时 GameObject 扩散预算 |

三份组合不是旧 Prefab 的标签式复刻；它们各自在 `W1NextCandidateRuntimeEntry` 中建立 CapabilitySampleTrace，并用 trace frame/event 驱动真实 Renderer/Transform/LineRenderer。

## 5. 预算与可观察不变量

| 项 | 固定上限 | 当前拓扑最大值 |
|---|---:|---:|
| GameObject | 10 | 6（fan + wave） |
| Renderer | 6 | 5（fan + wave） |
| ParticleSystem | 0 | 0 |
| distinct shared Material / Entry | 1 | 1 |

运行时公开 `ReadBudget()`、`TryGetCurrentLocalBounds()`、`IsInsideDeclaredEnvelope()`、`VisibleRendererCount`、`ReplayCount`、`OcclusionTransitions`、`NovaVisibleMoteCount`、`LastBeamWidth/Endpoint` 等观测点。Prefab 序列化为空闲不可见；`Play/Stop/ResetForPool` 不实例化 Material，并使用 `MaterialPropertyBlock` 更新颜色、相位与强度。

Build hash 绑定 canonical recipe hash、compiler version、runtime implementation signature、逐项 Shared 依赖 hash 和 Unity version。Manifest build hash 与 Prefab 同时命中才返回 `Unchanged`；Preview signature 未变化时不重写 Scene。W1 compiler 只遍历 `Assets/VFX/Recipes/StyleGallery` 的 11 份冻结 descriptor，不调用共享 Style Library builder、W-C1/W-C2/W-C3 builder 或 W3+ builder。

## 6. 本轮源码与静态结果

新增源码：

- Runtime：`W1NextCandidateRuntimeEntry.cs`、`W1NextCandidatePreviewDriver.cs`、`W1NextCandidateCell.cs`
- Editor：`W1NextCandidateAuthoring.cs`
- EditMode：`W1NextCandidateEditModeTests.cs`
- Preview：`W1NextCandidatePreviewSceneTests.cs`
- PlayMode：`W1NextCandidatePlayModeTests.cs`
- Recipe：`Assets/VFX/Recipes/StyleGallery/*.default.json` 共 11 份，schema `w1-style-next-candidate/v1`

源码落盘阶段没有启动或关闭 Unity。使用项目已有 Bee response file 与 Unity 2022.3.62f3c1 随附 Roslyn，对 Runtime、Editor、EditMode+Preview、PlayMode 四个程序集逐一静态编译，四项均 exit 0。唯一输出为既有 `StyledVfxController.portalRadius/swirlSpeed` 两条 CS0414 warning；新增 W1 文件没有静态编译 error/warning。

7 个新增 C# 源以及 StyleGallery folder + 11 份冻结 JSON 均已配稳定唯一 GUID 的 source-side `.meta`；本轮未触碰任何既有 `.meta` 或旧生成物。随后在隔离工作副本中执行了 Unity import、build 和定向测试，实际结果见下一节。

## 7. 隔离 Unity 实际结果与复现入口

2026-08-25 已在隔离工作副本中完成一次真实 Unity 2022.3.62f3c1 构建和测试。首次运行暴露并修复了三类真实问题：旧 `Arial.ttf` 入口失效；同一对象同时挂 `Image` 与 `Text` 的 UI 组件冲突；旋转载体、波形端点和 nova 最坏姿态越过声明 envelope。修复后 compiler runtime signature 升为 `fixed-envelope-v3|token-carriers-v2|fan-wave-batch-v2|charge-occlude-line-v2|telegraph-nova-burst-v6|viewport-clip-v1`，并从最终字节重新构建。

隔离结果如下：

- batch-safe build：Unity exit 0；11/11 Prefab、11/11 authoritative Manifest 和新 Preview Scene 均已生成；Manifest compiler 均为 `w1-style-next-candidate-1`。
- W1 EditMode：`3/3` passed。
- W1 Preview EditMode：`1/1` passed。
- W1 PlayMode：`4/4` passed。
- 共享 EditMode：`StyleAndStudioTests 6/6`、`ProjectileCapabilityTests 7/7`、`BeamCapabilityTests 5/5`、`TimingAreaCapabilityTests 6/6`、`CapabilityPreviewSceneTests 3/3`、`BeamCapabilityPreviewSceneTests 1/1`、`W3W8ElementFamilyTests 4/4`。
- 共享 PlayMode：`StyledVfxRuntimeTests 2/2`、`CapabilitySkinDemoRuntimeTests 1/1`、`ProjectileCapabilityRuntimeTests 2/2`、`BeamCapabilityRuntimeTests 9/9`、`TimingAreaCapabilityRuntimeTests 5/5`、`W3W8ElementRuntimeTests 3/3`。

`StyledVfxRuntimeTests` 的 XML 已写出并显示 `2/2 passed` 后，隔离 Unity 子进程未自行退出；等待后只终止了该精确 shadow PID，未触碰 canonical Unity。该异常只记录为 runner 退出行为，不改写已经封存的 XML 测试结果。

复现时仍须在没有其他 Unity 进程占用的隔离工作副本中只构建 W1：

```powershell
$unityExe = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$isolatedProject = '<isolated-worktree>\project'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -executeMethod VFXComposer.Editor.Style.W1NextCandidateAuthoring.BuildAllForBatch -quit -logFile '<absolute-log-dir>\w1-next-candidate-build.log'
```

Editor 菜单入口：

- `Tools > VFX Composer > Style > Build W1 Next Candidate (Batch Safe)`
- `Tools > VFX Composer > Style > Build W1 Next Candidate and Preview`

三个 W1-only filter 必须串行执行，并同时核对 Unity exit code 与 XML 中的失败数：

| testPlatform | full filter | 预期 |
|---|---|---:|
| EditMode | `VFXComposer.Tests.EditMode.W1NextCandidateEditModeTests` | 3/3 |
| EditMode（Preview） | `VFXComposer.Tests.EditMode.W1NextCandidatePreviewSceneTests` | 1/1 |
| PlayMode | `VFXComposer.Tests.PlayMode.W1NextCandidatePlayModeTests` | 4/4 |

```powershell
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform EditMode -testFilter VFXComposer.Tests.EditMode.W1NextCandidateEditModeTests -testResults '<absolute-result-dir>\w1-edit.xml' -logFile '<absolute-log-dir>\w1-edit.log'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform EditMode -testFilter VFXComposer.Tests.EditMode.W1NextCandidatePreviewSceneTests -testResults '<absolute-result-dir>\w1-preview.xml' -logFile '<absolute-log-dir>\w1-preview.log'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform PlayMode -testFilter VFXComposer.Tests.PlayMode.W1NextCandidatePlayModeTests -testResults '<absolute-result-dir>\w1-play.xml' -logFile '<absolute-log-dir>\w1-play.log'
```

隔离机已执行、后续重建仍应回归这些共享编译面的既有 filter：

- EditMode：`StyleAndStudioTests`、`ProjectileCapabilityTests`、`BeamCapabilityTests`、`TimingAreaCapabilityTests`、`CapabilityPreviewSceneTests`、`BeamCapabilityPreviewSceneTests`、`W3W8ElementFamilyTests`
- PlayMode：`StyledVfxRuntimeTests`、`CapabilitySkinDemoRuntimeTests`、`ProjectileCapabilityRuntimeTests`、`BeamCapabilityRuntimeTests`、`TimingAreaCapabilityRuntimeTests`、`W3W8ElementRuntimeTests`

## 8. 共享风险

- 本轮没有修改既有 Runtime/Editor/Shader/Mesh/Material 源文件，但新增类型进入既有 Runtime、Editor 和 Test asmdef 的共享编译面，因此上列 W-C1/W-C2/W-C3、旧 W1 和 W3+ 回归仍不可省略。
- compiler 只读依赖既有 8 份 Shared Style Material 和 6 类 Shared Mesh；如果隔离副本未先包含这些正式资产，构建会 fail-fast，而不会偷偷重建共享库。
- Cell Camera/layer、UI safe band 和实际 Renderer bounds 的机器断言只能证明结构约束；最终构图、辨识度和视觉完成度仍需用户查看新 Scene 后另行签署。当前状态保持 `W1_NEXT_CANDIDATE_VISUAL_PENDING`。

## 9. 隔离构建后的精确同步清单

本轮当前 7 组源码/sidecar（`.cs` 与同路径 `.cs.meta` 均须同步）：

- `Packages/com.vfxcomposer.unity/Runtime/Components/W1NextCandidateRuntimeEntry.cs` + `.cs.meta`（GUID `e9ed73ca752771625be14d9419e5c5d6`）
- `Packages/com.vfxcomposer.unity/Runtime/Components/W1NextCandidatePreviewDriver.cs` + `.cs.meta`（GUID `471862304714b6bb0eaf3e1301df9b00`）
- `Packages/com.vfxcomposer.unity/Runtime/Components/W1NextCandidateCell.cs` + `.cs.meta`（GUID `d580ac3b8b8d8edde34c0f1f83a32274`）
- `Packages/com.vfxcomposer.unity/Editor/Style/W1NextCandidateAuthoring.cs` + `.cs.meta`（GUID `d1abc4bc0e2c87cf5a29cf5338359b91`）
- `Packages/com.vfxcomposer.unity/Tests/EditMode/W1NextCandidateEditModeTests.cs` + `.cs.meta`（GUID `9c998e436229bb7398825ed973fb4910`）
- `Packages/com.vfxcomposer.unity/Tests/EditMode/W1NextCandidatePreviewSceneTests.cs` + `.cs.meta`（GUID `34776e675bf26045bb961b29594493bf`）
- `Packages/com.vfxcomposer.unity/Tests/PlayMode/W1NextCandidatePlayModeTests.cs` + `.cs.meta`（GUID `8d3bcaa7fc0217038c4eec4d9685a45d`）
下列 Recipe 及身份 sidecar 也都是 executeMethod 之前必须同步的 source input：

- `Assets/VFX/Recipes/StyleGallery.meta`（GUID `08a382501d3da24ffa6035e010667d8f`）
- `Assets/VFX/Recipes/StyleGallery/<id>.default.json` 与对应 `.json.meta`，其中 `<id>` 精确为下列 11 项

| `<id>` | JSON `.meta` GUID |
|---|---|
| `style_orb_stylized_2d` | `32176185c1d73cff24b45502b53675a1` |
| `style_orb_cartoon_2d` | `bda5594a3efb569d94babd24d70b0df9` |
| `style_orb_pixel_2d` | `91f29fb67ff2db8684c4a8ea49df0951` |
| `style_orb_inkwash_2d` | `d47f407e0029f40b0675664666d61bd8` |
| `style_orb_semireal_3d` | `44f3cc88f90584a0992cb911c57f39c6` |
| `style_orb_holo_3d` | `7b5af74da912bb4c6ebfc567b32102e2` |
| `style_orb_dark_3d` | `91898dff0aa87ea575dcc399062cd861` |
| `style_orb_neon_2d` | `0306cdf1bb6247904aac87dcb1ec3f0d` |
| `fan_wave_cartoon_showcase_2d` | `7daada57624fd8bb095e2fe726c7d752` |
| `charge_occlude_holo_showcase_3d` | `769de1c6e3e65b7851f036b8fed6875b` |
| `telegraph_nova_holy_showcase_3d` | `535e7c6cc35f483218824c23df046e58` |

executeMethod 成功后只同步下列新生成物，不同步 Library/Logs/UserSettings：

- `Assets/VFX/Generated/<id>.meta`
- `Assets/VFX/Generated/<id>/VFX_<id>.prefab`
- `Assets/VFX/Generated/<id>/VFX_<id>.prefab.meta`
- `ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json`
- `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity`
- `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity.meta`

文档同步项没有 `.meta`：`docs/allwork/01_STYLE_SYSTEM.md`、`docs/stage-notes/W1_W2_STYLE_STUDIO_REPORT.md`、`docs/stage-notes/W1_NEXT_CANDIDATE_REPORT.md`、`docs/stage-notes/FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md`（仅 W1 行）与 `docs/allwork/00_INDEX_AND_ACCEPTANCE.md`（仅 W1 当前状态）。
