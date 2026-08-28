# W15 新 Archetype next-candidate 实施记录

状态：`W15_NEXT_CANDIDATE_VISUAL_PENDING`
日期：2026-08-25
边界：本记录说明全新的 W15 并行候选源码、隔离 shadow 构建、定向机器门禁与独立终审结果；机器 GO 不产生用户视觉签署，不声明 L3/L4 通过，也不改写旧 rejected 候选或任何 formal evidence/write-once 路径。状态继续为 `W15_NEXT_CANDIDATE_VISUAL_PENDING`。

## 1. 历史结论与新候选边界

旧 W15 候选继续保持 `rejected`，用户原话逐字保留为：“拒绝；W15仅有六类Archetype的概念轮廓，Decal缺少三表面贴附，WeaponTrail缺少快慢挥差异，Destruction缺少完整破碎表现，LifeCycle未绑定角色溶解，Portal缺少出入口时序差异，Loot五档主要只换颜色；设计与实现不同步，整体未达到商用级视觉完成度。”

本轮没有修改旧 `W15NewArchetypeAuthoring`、旧 `StyledVfxController`、旧 `NewArchetypePreviewDriver`、旧 Recipe、旧 Generated Prefab、旧 Preview Scene 或旧 W15 tests。新候选使用独立身份和路径：

- compiler version：`w15-next-candidate-1`
- Recipe：`Assets/VFX/Recipes/W15NextCandidate`
- Generated：`Assets/VFX/Generated/W15NextCandidate`
- Shared：`Assets/VFX/Shared/W15NextCandidate`
- Preview：`Assets/VFX/Preview/VFXPREVIEW_W15_NEXT_CANDIDATE.unity`
- 10 个新 Recipe id 均以 `w15nc_` 开头；旧 id 只作为报告映射，不作为写入目标

`W15NextCandidateAuthoring.BuildAll()` 只遍历这 10 个新定义；它不调用旧 W15 builder、W-C1/W-C2/W-C3 builder、W1 builder 或其他批次 builder。

## 2. 六类真实可观察运行载体

| 缺口 | next-candidate 运行载体 | 可观察不变量 |
|---|---|---|
| Decal 三表面贴附 | `AttachToSurface(surfaceKey, point, normal, tangent)` 正交化切线，以 `Quaternion.LookRotation(normal,tangent)` 对齐；三层 Mesh/Particle 沿本地深度递增 | Preview 同时放置 GROUND、WALL、SLOPE_45 真实支撑面；位置含固定 `0.006` surface bias；同 surface key 的固定 stack limit 会回收最旧实例 |
| WeaponTrail 快/慢挥 | 每帧输入真实 blade root/tip，按世界位移/`deltaTime` 测速；8–16 个历史采样点经 Catmull-Rom 中点平滑后生成 swept ribbon 动态 Mesh | Preview 左右并排运行高速 203°/0.36 s 与低速 100°/1.9 s 轨迹；高速生成 ribbon，低于 threshold 的慢挥保持无 ribbon 并按 fade time 衰减，不由标签/枚举伪造 |
| Destruction 完整破碎 | intact hold 后，单个动态 Mesh 内生成 8–12 个独立四边碎片；每片由 seed/index 得到不同初速度、旋转、阻尼和解析两次弹跳 | 所有碎片参与运动，爆点只发一次 dust；无 Rigidbody/Physics；AllowTail 或 Immediate 后 Mesh、Particle、Renderer 全清；同 seed/index/age 精确复播 |
| LifeCycle 角色溶解 | `BindCharacterRenderers(Renderer[])` 将 MPB 的 `_Dissolve`、bounds、direction、edge color 写到外部可见模型 | Preview 各有一具 6-part Death/Entrance 角色并使用真正消费 `_Dissolve` 的 `CharacterDissolve` Shader；死亡 0→1、入场 1→0；Reset 恢复 MPB 且不禁用 gameplay Renderer |
| Portal 出入口差异 | 同 pair 的 Entry/Exit 各自有 ring/interior、Entry funnel、Exit burst 和方向 flow line | Entry 在 0.00–0.20 s 收束吸入后进入 transit；Exit 在 0.35 s 前完全延迟隐藏，随后 0.35–0.55 s 外扩喷出；两端流向相反且使用不同形态载体 |
| Loot 五档 | rarity 1–5 分别生成 circle/diamond/hexagon/crown/star 动态几何，层数为 1–5 | cadence、pulse peak、beam width/height、sparkle rate/capacity 均随档位改变；pickup 使用 17 点可见二次曲线并在终点池化，不是只切颜色 |

Runtime 统一实现 `IVfxRuntimeEntry`，支持 `Play`、`Stop(AllowTail)`、`Stop(Immediate)`、`ResetForPool` 和事件入口。运行时动态 Mesh 在 Awake/Play 复用，Reset 只清空，不创建材质实例；颜色、相位、透明度和 dissolve 使用 `MaterialPropertyBlock`。

## 3. Preview、格内约束与预算

新 Preview 固定为 10 格、单正交 Review Camera、每格 `3.80 × 2.55 × 2.40` BoxCollider：

- 2 个 Decal Recipe × 3 个真实表面 = 6 个同时可见贴附实例；
- 2 个 WeaponTrail Recipe × fast/slow = 4 个同时比较实例；
- 2 个 Destruction、2 个绑定角色 LifeCycle；
- 1 个 Portal Recipe 的 Entry/Exit 双端；
- 1 个 Loot Recipe 的 5 个 rarity 同屏实例；
- 总计 21 个 W15 runtime entry，固定 6.00 秒 reset/rebind/replay 周期。

Destruction 在 Preview 中固定 root scale `0.46`；Preview EditMode 门禁对 12 个碎片、0–1.90 秒的 33 个时间采样逐一做 cell-local 包络断言。其他比较实例的 root、surface、endpoint 和 pickup target 同样固定在各自 cell bounds 内。

Prefab 硬预算：

| Archetype | Renderer 上限 | Particle capacity 上限 | Transform 上限 |
|---|---:|---:|---:|
| Decal / WeaponTrail | 3 | 24 | 10 |
| Destruction | 3 | 56 | 16 |
| LifeCycle | 2 | 24 | 10 |
| Portal | 5 | 0 | 16 |
| Loot | 5 | 24 | 10 |

所有生产 Prefab 必须恰有一个 `IVfxRuntimeEntry`，必须为空闲不可见、无 Rigidbody、无 Preview driver、无本地纹理；Manifest enforcement 必须为 `strict`，build hash 必须绑定 canonical Recipe、compiler version、新 Runtime/Shader implementation signature、Shared Mesh 依赖 hash 和 Unity version。Prefab + Manifest build hash 同时命中才允许返回 `Unchanged`。

## 4. 定向门禁

新增 W15-only 门禁：

- `VFXComposer.Tests.EditMode.W15NextCandidateTests`（3 tests）：10 份新 Recipe 的严格 Domain/RecipeValidator、旧 rejected W15 source/recipe/scene/test 构建前后 SHA-256 不变、两次构建 GUID/buildHash 幂等、具体 carrier/预算/Manifest/池空闲/双出口。
- `VFXComposer.Tests.EditMode.W15NextCandidatePreviewTests`（1 test）：10 格/21 entry/单相机/固定 bounds；三表面真实法线与 anchor 对齐；fast/slow、角色 shader bind、Portal 双端、Loot 五档；Destruction 全片全时间包络。
- `VFXComposer.Tests.PlayMode.W15NextCandidateRuntimeTests`（8 tests）：六类逐类真实语义；Destruction seed/replay/两次弹跳；曲线 pickup；全部 Archetype 的 AllowTail + Immediate 清理和第二次 Play；新增测试会从 AssetDatabase 真实加载并实例化全部 10 个 production Prefab，逐一覆盖两个 Stop 路径和复播。

隔离 shadow 的最终 Unity Test Runner 结果为 EditMode `3/3`、Preview EditMode `1/1`、PlayMode `8/8`，全部 `failed=0`、`skipped=0`。最终 XML 分别为 `.codex_tmp/w15-next-candidate-results/w15-edit.xml`、`w15-preview.xml`、`w15-play-r3.xml`；同目录较早的 `w15-play.xml`、`w15-play-r2.xml` 只保留为修复过程记录，不能替代或与 `r3` 累加成最终结果。

## 5. 源码、隔离构建与机器结果

新增实现：

- Runtime：`W15NextCandidateController.cs`、`W15NextCandidatePreviewDriver.cs`
- Editor：`W15NextCandidateAuthoring.cs`
- EditMode：`W15NextCandidateTests.cs`、`W15NextCandidatePreviewTests.cs`
- PlayMode：`W15NextCandidateRuntimeTests.cs`
- Shader：`W15NextCandidateLayeredUnlit.shader`、`W15NextCandidateCharacterDissolve.shader`
- Recipe：`Assets/VFX/Recipes/W15NextCandidate/*.default.json` 共 10 份

源码阶段先使用项目已有 Bee response file 与 Unity 2022.3.62f3c1 随附 Roslyn 做了四层静态编译，作为进入隔离 Unity 前的基线：

| 静态目标 | 结果 |
|---|---|
| 完整 Runtime response + W-C 当前窄增量 + W15 Runtime | exit 0；只有既有 `StyledVfxController.portalRadius/swirlSpeed` 两条 CS0414 |
| W15 Editor authoring（引用现有 Editor assembly + W15 Runtime add-on） | exit 0；无输出 |
| W15 EditMode + Preview tests | exit 0；无输出 |
| W15 PlayMode tests | exit 0；无输出 |

另做了 10/10 JSON 语法解析与 id 唯一检查；22 个新增 source-side `.meta` 的 GUID 在全 `project` 范围内均只出现一次，6 个新增 C# 均存在同路径 `.cs.meta`。

随后在隔离 shadow `.codex_tmp/w24-fresh-20260825-0628/project` 真实执行 Unity batch build，最终构建日志为 `.codex_tmp/w15-next-candidate-results/w15-build-r2.log`，exit 0。构建后核对为 10 份 Recipe、10 个 production Prefab、10 份 strict Manifest 和 1 份 `VFXPREVIEW_W15_NEXT_CANDIDATE.unity`。独立终审结论为 **GO，P0=0、P1=0**；旧 W15 protected 集合构建前后 `11/11` SHA-256 exact，未被新 builder 覆写。

共享样式面在修复后的最终回归为 EditMode `6/6` 与 PlayMode `2/2`，对应当前 XML `.codex_tmp/style-special-next-candidate-results/regressions/style-studio.xml` 和 `.codex_tmp/w15-next-candidate-results/regressions/styled-play-final.xml`。这些 XML、日志及独立审计结论都是本轮 `.codex_tmp` 隔离 shadow 的操作证据，不属于旧 W15 的历史 formal evidence，也没有写入 W24 formal/write-once evidence；旧 rejected 证据只证明旧候选，不能被本轮结果改写或合并解释。

本轮没有把 shadow 中的 10 个 Prefab、10 份 Manifest、Shared Material 或新 Preview Scene 晋升到 canonical `project`；canonical 中对应 Generated 根和新 Scene 仍不存在。机器 GO 只表示新候选通过当前技术门禁，不是 Game View 捕获、商用品质结论或用户视觉结果，因此当前仍只能写作 `W15_NEXT_CANDIDATE_VISUAL_PENDING`，不得写成 W15 accepted、视觉通过或 L3/L4 通过。

## 6. 已执行的隔离 Unity 门禁与复现入口

本轮实际执行目标为 `.codex_tmp/w24-fresh-20260825-0628/project`；下列模板保留为同边界复现入口，只能指向独立工作副本：

```powershell
$unityExe = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$isolatedProject = '<isolated-worktree>\project'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -executeMethod VFXComposer.Editor.Archetypes.W15NextCandidateAuthoring.BuildAllFromCommandLine -quit -logFile '<absolute-log-dir>\w15-next-candidate-build.log'
```

Editor 菜单入口：`Tools > VFX Composer > Archetypes > Build W15 Next Candidate (Visual Pending)`。

只执行下列 W15-only filters，并同时核对 Unity exit code、XML `failed=0` 和预期 test count：

| testPlatform | full filter | 预期 |
|---|---|---:|
| EditMode | `VFXComposer.Tests.EditMode.W15NextCandidateTests` | 3/3 |
| EditMode（Preview） | `VFXComposer.Tests.EditMode.W15NextCandidatePreviewTests` | 1/1 |
| PlayMode | `VFXComposer.Tests.PlayMode.W15NextCandidateRuntimeTests` | 8/8 |

```powershell
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform EditMode -testFilter VFXComposer.Tests.EditMode.W15NextCandidateTests -testResults '<absolute-result-dir>\w15-edit.xml' -logFile '<absolute-log-dir>\w15-edit.log'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform EditMode -testFilter VFXComposer.Tests.EditMode.W15NextCandidatePreviewTests -testResults '<absolute-result-dir>\w15-preview.xml' -logFile '<absolute-log-dir>\w15-preview.log'
& $unityExe -batchmode -nographics -projectPath $isolatedProject -runTests -testPlatform PlayMode -testFilter VFXComposer.Tests.PlayMode.W15NextCandidateRuntimeTests -testResults '<absolute-result-dir>\w15-play.xml' -logFile '<absolute-log-dir>\w15-play.log'
```

隔离构建/Test Runner 已成功，下一步仍只能进入新 Scene 的用户视觉审核；机器门禁不能替代 Game View 构图、可读性、节奏和商用品质签署。

## 7. 共享编译面风险与回归 filters

本轮没有修改任何既有共享 Runtime/Editor/Shader/Recipe/asmdef；新增类型会进入既有 Runtime、Editor 与 Test asmdef 的共享编译面。新 authoring 只读复用现有 Shared Quad/Ring/Burst/Cone/Shard Mesh，若隔离副本缺少依赖则 fail-fast，不会重建共享库。

本轮已经完成独立终审所要求的共享样式修复回归；以下完整窄回归清单继续作为复现与后续晋升前检查边界：

- W-C1：EditMode `VFXComposer.Tests.EditMode.ProjectileCapabilityTests`、`VFXComposer.Tests.EditMode.CapabilityPreviewSceneTests`；PlayMode `VFXComposer.Tests.PlayMode.ProjectileCapabilityRuntimeTests`、`VFXComposer.Tests.PlayMode.CapabilitySkinDemoRuntimeTests`。
- W-C2：EditMode `VFXComposer.Tests.EditMode.BeamCapabilityTests`、`VFXComposer.Tests.EditMode.BeamCapabilityPreviewSceneTests`；PlayMode `VFXComposer.Tests.PlayMode.BeamCapabilityRuntimeTests`。
- W-C3：EditMode `VFXComposer.Tests.EditMode.TimingAreaCapabilityTests`；PlayMode `VFXComposer.Tests.PlayMode.TimingAreaCapabilityRuntimeTests`。
- W1：EditMode `VFXComposer.Tests.EditMode.StyleAndStudioTests`、`VFXComposer.Tests.EditMode.W1NextCandidateEditModeTests`、`VFXComposer.Tests.EditMode.W1NextCandidatePreviewSceneTests`；PlayMode `VFXComposer.Tests.PlayMode.StyledVfxRuntimeTests`、`VFXComposer.Tests.PlayMode.W1NextCandidatePlayModeTests`。
- rejected W15 防回退：EditMode `VFXComposer.Tests.EditMode.W15NewArchetypeTests`；PlayMode `VFXComposer.Tests.PlayMode.W15NewArchetypeRuntimeTests`。

## 8. 精确同步清单

executeMethod 前必须同步下列 source input；不得从 shadow/用户项目反向取随机 `.meta`。

### 8.1 C# 与稳定 sidecar

| source（同时同步同路径 `.cs.meta`） | GUID |
|---|---|
| `Packages/com.vfxcomposer.unity/Runtime/W15/W15NextCandidateController.cs` | `01bd8cd2c7eb0326d6b104b95626f45c` |
| `Packages/com.vfxcomposer.unity/Runtime/W15/W15NextCandidatePreviewDriver.cs` | `ca3192808073c49621c9e57b7894c856` |
| `Packages/com.vfxcomposer.unity/Editor/Archetypes/W15NextCandidateAuthoring.cs` | `101f583db4c3c48b4527de30748ac12d` |
| `Packages/com.vfxcomposer.unity/Tests/EditMode/W15NextCandidateTests.cs` | `005e4aec41c47b403e47e49befb368fb` |
| `Packages/com.vfxcomposer.unity/Tests/EditMode/W15NextCandidatePreviewTests.cs` | `74243ed4b2d15f420422f9e63a948ec9` |
| `Packages/com.vfxcomposer.unity/Tests/PlayMode/W15NextCandidateRuntimeTests.cs` | `5a0e18491c4641c0c9ea9c164e07b5f7` |

新 Runtime folder sidecar：`Packages/com.vfxcomposer.unity/Runtime/W15.meta`，GUID `ecdd75c8b9b8caf0016918724b61918f`。

### 8.2 Recipe 与 Shader source input

- `Assets/VFX/Recipes/W15NextCandidate.meta`（GUID `b1a1fbb3d50b934e554c58e17419cdf4`）
- `Assets/VFX/Shared/W15NextCandidate.meta`（GUID `0c5f069900f5da7bd9c2f9695213fdb0`）
- `Assets/VFX/Shared/W15NextCandidate/Shaders.meta`（GUID `f3a058b496aeccfb94ec64e3d43dfa6b`）
- `W15NextCandidateCharacterDissolve.shader` + `.shader.meta`（GUID `463d4455e99b6b0b2a0a2ae2283c66f4`）
- `W15NextCandidateLayeredUnlit.shader` + `.shader.meta`（GUID `5a2a948e9dd73c9f66a8b83d67819658`）

| Recipe `<id>.default.json`（同时同步 `.json.meta`） | GUID |
|---|---|
| `w15nc_crate_break_destruction_3d` | `5db66836c134af01dd4794c2967fcc53` |
| `w15nc_crystal_shatter_destruction_3d` | `ab4f49dcc95927057f84e40ed7de7ba2` |
| `w15nc_death_dissolve_lifecycle_3d` | `ae3eb0688dfcbc09968094f37713c25b` |
| `w15nc_energy_whip_trail_2d` | `e4307b480d4a83b1d86df8216a059b57` |
| `w15nc_frost_decal_3d` | `5193654052b6c714b9263ed2b1eb0e6b` |
| `w15nc_hero_entrance_lifecycle_3d` | `0d0ab7b4f12306d61a6c51dbae5a1051` |
| `w15nc_katana_trail_weapon_3d` | `e611b7813c35a2472fbbdc7a00c6f6d1` |
| `w15nc_loot_beam_pickup_3d` | `e251125e962b2962de19d1db125029b0` |
| `w15nc_scorch_decal_3d` | `23fcf7a8216a7435517fb04ba884e7d2` |
| `w15nc_twin_portal_3d` | `d8ae7e8a7bd534c9ebee5cacf6bd3ceb` |

### 8.3 executeMethod 成功后允许同步的新生成物

以下是未来晋升时的白名单，不代表本轮已经同步；截至本报告回填，shadow 生成物仍未晋升 canonical。

- `Assets/VFX/Shared/W15NextCandidate/Materials.meta`
- `Assets/VFX/Shared/W15NextCandidate/Materials/MAT_W15NC_{Decal,WeaponTrail,Destruction,LifeCycle,Portal,Loot}.mat` 及各自 `.mat.meta`
- `Assets/VFX/Shared/W15NextCandidate/Materials/MAT_W15NC_CharacterDissolve.mat`、`MAT_W15NC_PreviewSurface.mat` 及各自 `.mat.meta`
- `Assets/VFX/Generated/W15NextCandidate.meta`
- 对上述 10 个 `<id>`：`Assets/VFX/Generated/W15NextCandidate/<id>.meta`、`<id>/VFX_<id>.prefab`、`<id>/VFX_<id>.prefab.meta`
- 对上述 10 个 `<id>`：`ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json`
- `Assets/VFX/Preview/VFXPREVIEW_W15_NEXT_CANDIDATE.unity` 与 `.unity.meta`

不同步 `Library`、`Logs`、`Temp`、`UserSettings`、任何 evidence/shadow/write-once 路径，也不同步旧 W15/W-C1/W-C2/W-C3/W1 的 Generated 输出。

文档同步项没有 `.meta`：`docs/stage-notes/W15_NEXT_CANDIDATE_REPORT.md` 与 `docs/stage-notes/FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md` 的 W15 对应行。旧 `W15_NEW_ARCHETYPES_REPORT.md` 和旧拒绝段不改。
