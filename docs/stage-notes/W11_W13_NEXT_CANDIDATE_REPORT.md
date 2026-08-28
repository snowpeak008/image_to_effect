# W11–W13 环境 / 打击反馈 / 大招组合 next-candidate 实施记录

状态：`NEXT_CANDIDATE_VISUAL_PENDING`  
日期：2026-08-25  
边界：本记录说明 W11、W12、W13 全新并行候选的源码与隔离机器门禁；不产生用户视觉签署，不声明 L3/L4，也不改写旧 rejected 候选、旧 Preview、正式 W24 evidence 或 write-once 路径。Canonical Generated/Preview 晋升尚未执行。

## 1. 历史结论与新候选边界

旧 W11/W12/W13 批量拒绝继续保持有效，用户原话逐字保留为：

> 拒绝，无法商用，后续的特效类，都不验收了，这是通病了，都是同样的拒绝，无法商用，

上述历史结论仍只描述旧候选。其后用户另行授权全项目持续开发并把视觉验收统一延后到最后，本轮据此建立全新 candidate id、Recipe 根、Generated 根和 Preview Scene；没有覆盖旧字节：

- compiler：`w11-w13-next-candidate-1`
- Recipe：`Assets/VFX/Recipes/W11W13NextCandidate/{W11,W12,W13}`
- Generated：`Assets/VFX/Generated/W11W13NextCandidate/<new-id>`
- Shared：`Assets/VFX/Shared/W11W13NextCandidate`
- Preview：`VFXPREVIEW_W11_ENVIRONMENT_NEXT_CANDIDATE.unity`、`VFXPREVIEW_W12_HIT_FEEDBACK_NEXT_CANDIDATE.unity`、`VFXPREVIEW_W13_ULTIMATE_NEXT_CANDIDATE.unity`
- 新 id：W11 `w11nc_` 7 个、W12 `w12nc_` 7 个、W13 `w13nc_` 6 个；旧 id 只作为 `sourceId` 或只读依赖

`W11W13NextCandidateAuthoring.BuildAll()` 不调用旧 `IndependentContentAuthoring` 或 `CompositeAndHeroKitAuthoring`，也不调用 W1、W3–W10、W15、W16、W-C1–W-C3 或 W24 正式作者器。既有共享 Ring/Burst/Ribbon/Cone/Shard 只做 `LoadAssetAtPath` 依赖校验；本入口不会重建旧共享库。Composite build hash 同时纳入依赖 Manifest 哈希，依赖预算变化不会被 unchanged 快路径跳过。

## 2. 可观察实现载体

### W11 环境与天气（7）

| 候选 | 独立载体与运行语义 |
|---|---|
| rain | 近景拉伸雨丝、中景雨幕、贴地溅环、远雾；`SetIntensity` 连续插值，`SetWind` 写入各层世界速度，`SetLayerDensities` 独立控制近/中/远密度 |
| sandstorm | 世界空间横流沙幕、滚沙团、贴地跑沙与偶发飞砾；没有全屏 UI 伪装能见度 |
| mist | 三层异速雾带、撕裂边缘和独立呼吸深度层 |
| leaves | 近/中景不同速度粒子层、真实翻转与着地滑移载体 |
| fireflies | 游曳粒子、两颗独立缠绕体和近景大光斑层 |
| ambient dust | 普通微尘、光带内增密尘层和真实世界空间光束载体 |
| waterfall | 10 点有深度弯曲水帘、两条白丝、水雾、溅珠和下游泡沫；斜视时存在真实 z 跨度 |

全部 W11 Prefab 为 sustained、固定 seed、最多 5 个 ParticleSystem，容量不超过 160、Renderer 不超过 6；Immediate 清空，AllowTail 有界结束。Preview 使用单个环境舞台、地面/纵深接收物与透视斜机位，一次只播放一条，避免七个天气互相叠成不可辨背景。

### W12 打击反馈与连携（7）

| 候选 | 独立载体与运行语义 |
|---|---|
| hit flash | 外部 Renderer 数组 + MPB `_FlashAmount/_HitTint/_HitEdgeWidth`；Shader 实际消费白闪与边缘色，Play 前已有 MPB 完整保存，Reset 恢复；不替换材质 |
| critical | 硬裂纹 Mesh、四角星、倾斜冲击环和金色碎屑；one-shot 峰值在前段 |
| parry | 扇形金属火花使用真实 world collision/bounce，另有接触环和落地细尾 |
| knockup | 独立地环、竖直气浪柱、上升核心线和抛起碎屑，列体沿 y 轴真实增长 |
| combo | 1–5 级分别启用一条真实 Stack Ring，而不是单色枚举；固定粒子容量，层级变化不分配新对象 |
| reaction | 两个可运行时换色的独立能量体从两端会聚，之后才激活第三色融合体、螺旋和碎粒 |
| lifesteal | 20 点世界空间下垂束、两个动态端点和沿目标→施法者方向逆流的可见 mote |

W12 Preview 为 3×3 有界格、7 个外部 Capsule 假人和一个 Scene-only 驱动器；Runtime Prefab 不包含 Preview helper 或标签。

### W13 Composite / Ultimate（6）

每个新 Composite Prefab 根只有一个 `IVfxRuntimeEntry` 和四个本地阶段根：Intro / Primary / Release / Tail。旧子 Prefab 只以序列化引用进入固定池，未复制其层级到新 Prefab；每次 Play 复用池，Reset 会还原四阶段根姿态。时间轴 cue 具备 play/stop、位置、旋转、缩放与可选事件；camera hint 仍是纯数据通道，Runtime Prefab 不拥有相机，独立 Preview 驱动器分别消费 zoom / shake / slowmo 并在退出时恢复相机与 `Time.timeScale`。

- dragon：连续蓄力体 → 龙首轮廓 → 扫动吐息体积 → 余燃场。
- meteor：预警场 → 6 个独立陨石位置/6 个同源 pooled impact → 连续冲击 → 尘幕收束。
- frozen domain：外扩边界 → 持续领域 → 5 个独立冰刺 → 全域碎裂。
- judgement：三层符文 → 连续聚能核 → 空间巨柱 → 灰羽尾。
- demon gate：血阵 → 深门框 → 破门手 → 威吓波；在 `gate_formed` 与 `hand_release` 两个精确命名门暂停，错误事件不能放行。
- blade tempest：拔刀连续体 → 同一 `slash_3d_stylized` 引用的 8 个空间实例 → 风暴体积 → 收刀闪。

本地结构遵守 complex 上限 16 GameObject / 深度 3 / Renderer 14；Recipe 同时冻结 Composite 峰值档 200 particles / 10 PS / 10 materials / 14 renderers。这里的预算与结构检查只属于机器门禁，不是视觉质量结论。

## 3. 源码与当前静态结果

新增：2 个 Runtime、2 个 Editor、2 个 EditMode（其中 1 个 Preview）、1 个 PlayMode test source、1 个 URP Shader、20 个 JSON Recipe；加上 8 个文件夹 sidecar，共 28 个源码/Recipe/Shader 文件和 36 个稳定 `.meta`。

没有启动、关闭或控制 Unity。使用 Unity 2022.3.62f3c1 随附 Roslyn 及现有 Bee reference assemblies 做了四层 focused static compilation：

| 静态目标 | 结果 |
|---|---|
| W11–W13 Runtime add-on | exit 0 |
| W11–W13 Editor plan/authoring add-on | exit 0 |
| W11–W13 EditMode + Preview tests | exit 0 |
| W11–W13 PlayMode tests | exit 0 |

20/20 JSON 已完成语法、id 唯一、7/7/6 分组和唯一状态值审计；36 个新增 `.meta` 的 GUID 在全 `project` 范围内唯一；7 个新增 C# 均有稳定同路径 sidecar。未执行 Unity import、Shader import、Prefab/Manifest build、Test Runner、Game View capture 或用户验收，因此当前唯一状态是 `NEXT_CANDIDATE_VISUAL_PENDING`。

禁写审计对新增 Runtime / Editor / Tests 扫描 `EditorApplication.Exit`、外部 Process/Unity 启动、File/Directory/Asset 删除以及 `VfxStyleSharedLibrary.EnsureAll`，命中数为 0。Canonical 当前也不存在本包的 Generated 根、3 个新 Preview Scene、Materials 根或新 Manifest；`.codex_tmp` 仅承载 Roslyn 临时 DLL，不在同步清单内。

## 4. 隔离 Unity 入口与预期定向门禁

executeMethod：

```text
VFXComposer.Editor.NextCandidates.W11W13NextCandidateAuthoring.BuildAllForBatch
```

W13 canonical-overlay 修复使用严格窄入口：

```text
VFXComposer.Editor.NextCandidates.W11W13NextCandidateAuthoring.BuildW13ForBatch
```

该入口只验证/构建 `Group("W13")` 六项、只处理 `MAT_W13_NextCandidate`、只写 W13 Preview；不构建 W11/W12，也不调用旧 builder。W13 修复门禁必须使用方法级 filter，不能运行仍会调用 `BuildAll()` 的三 Scene 全组 Preview 回归。

该方法不调用 `EditorApplication.Exit`，由外部批处理统一管理进程退出。先执行 build，再执行：

| testPlatform | filter | 预期 |
|---|---|---:|
| EditMode | `VFXComposer.Tests.EditMode.W11W13NextCandidateEditModeTests` | 4/4 |
| EditMode | `VFXComposer.Tests.EditMode.W11W13NextCandidatePreviewTests` | 1/1 |
| PlayMode | `VFXComposer.Tests.PlayMode.W11W13NextCandidateRuntimeTests` | 7/7 |

预期数只描述测试源码，尚不是 Unity Test Runner 结果。必须同时核对 Unity exit code、XML `failed=0` 与 test count；机器通过后状态仍保持 `NEXT_CANDIDATE_VISUAL_PENDING`，最终画面由用户统一签署。

## 5. 共享回归面

新增类型进入既有 Runtime/Editor/Test asmdef，且 W13 只读引用旧依赖。隔离构建成功后至少串行回归：

- 旧 W11/W12：EditMode `VFXComposer.Tests.EditMode.W11W17IndependentContentTests`；PlayMode `VFXComposer.Tests.PlayMode.W11W17IndependentRuntimeTests`。
- 旧 W13/W18：EditMode `VFXComposer.Tests.EditMode.W13W18CompositeAndHeroKitTests`；PlayMode `VFXComposer.Tests.PlayMode.W13W18CompositeRuntimeTests`。
- 风格/元素依赖：`StyleAndStudioTests`、`StyledVfxRuntimeTests`、`W3W8ElementFamilyTests`、`W3W8ElementRuntimeTests`，以及已完成的 W1/W3–W10/W15/W16 next-candidate filters。
- W-C1/W-C2/W-C3：Projectile、Beam、TimingArea 的 EditMode / Preview / PlayMode 定向 filters。

## 6. 精确 source sync 清单

### 6.1 C#、Shader 与文件夹 sidecar

| 文件（同时同步同路径 `.meta`） | GUID |
|---|---|
| `Packages/com.vfxcomposer.unity/Runtime/W11W13NextCandidate/W11W13NextCandidateController.cs` | `4405247fb6bbc356ed57a8e09f292951` |
| `Packages/com.vfxcomposer.unity/Runtime/W11W13NextCandidate/W11W13NextCandidatePreviewDriver.cs` | `e7f6bdc3c4c75c43610a0513d031cb56` |
| `Packages/com.vfxcomposer.unity/Editor/W11W13NextCandidate/W11W13NextCandidatePlan.cs` | `6608fbd470d23024aeffe245558b1c91` |
| `Packages/com.vfxcomposer.unity/Editor/W11W13NextCandidate/W11W13NextCandidateAuthoring.cs` | `a734fa96b1b6d34a92a6a106bee60fe1` |
| `Packages/com.vfxcomposer.unity/Tests/EditMode/W11W13NextCandidateEditModeTests.cs` | `d377ab8f6aa57d2d55f2829cc83a57e7` |
| `Packages/com.vfxcomposer.unity/Tests/EditMode/W11W13NextCandidatePreviewTests.cs` | `bdc039d6bb74629fdb71316a916245ba` |
| `Packages/com.vfxcomposer.unity/Tests/PlayMode/W11W13NextCandidateRuntimeTests.cs` | `5aee3928252e04954b583b456dc3333b` |
| `Assets/VFX/Shared/W11W13NextCandidate/Shaders/W11W13NextCandidateLayeredUnlit.shader` | `70776f9a8b468361c206ba11734f7ef1` |

文件夹 sidecar：

- `Runtime/W11W13NextCandidate.meta` `444731558ea7496c98cf575ba2e162cf`
- `Editor/W11W13NextCandidate.meta` `ff00264a59b51eb6c0a106b1f25aa9ad`
- `Assets/VFX/Shared/W11W13NextCandidate.meta` `ae3f44ca6b82d070d090924bb0eac091`
- `Assets/VFX/Shared/W11W13NextCandidate/Shaders.meta` `20c1d1a3d673a01a2c9cbe4ed55290b1`
- `Assets/VFX/Recipes/W11W13NextCandidate.meta` `27da0f2fd817e1e31050b4d5f426c3ff`
- `Assets/VFX/Recipes/W11W13NextCandidate/W11.meta` `bbb38168830be80e432742fecdda965b`
- `Assets/VFX/Recipes/W11W13NextCandidate/W12.meta` `f9b4b33941b520cf4785bede8ec2f5ec`
- `Assets/VFX/Recipes/W11W13NextCandidate/W13.meta` `334ebfbaa8377f1aec427a6ad03c7a7c`

共享 `Packages/com.vfxcomposer.unity/Editor/NextCandidates.meta`（GUID `f70609173adb1b4083ef4d2528235a1e`）不属于本包文件树；全项目同步如包含该 sidecar，唯一来源为 W17/W18 并发包，本包不创建或覆盖它。

### 6.2 Recipe source（同时同步 `.json.meta`）

| id | GUID |
|---|---|
| `w11nc_ambient_dust_volume` | `12481374a815001c6eb07712946de705` |
| `w11nc_falling_leaves_volume` | `6e0d09665e5412c93ab8bf96423130e8` |
| `w11nc_fireflies_volume` | `fd4c7b726d698eeae96acd10138889bb` |
| `w11nc_mist_fog_volume` | `68854e379cd5a7044a8ad89ce8530920` |
| `w11nc_rain_weather_volume` | `52046f0974debc484b374f312c3e047b` |
| `w11nc_sandstorm_weather_volume` | `7fe60a984ab3f6d7f7dae6cee9787364` |
| `w11nc_waterfall_env_3d` | `5a4b53470b99aae249e4f003a185f243` |
| `w12nc_combo_surge_aura_2d` | `e628dcde22ee4c93748c2cb2c4bf2ae8` |
| `w12nc_critical_strike_impact_2d` | `32a9871e1820f3ef65ce1c646a95966b` |
| `w12nc_elemental_reaction_burst_2d` | `518507186d211db9209d892faf79113b` |
| `w12nc_hit_flash_status_2d` | `4c119d57bfc46510e3700c3d620e453e` |
| `w12nc_knockup_launcher_impact_3d` | `b00b8d6f8c2b6ecdc9cbda4a75f50744` |
| `w12nc_lifesteal_link_beam_2d` | `d8e77df5d6502dba1d3dc4998255a133` |
| `w12nc_parry_spark_impact_3d` | `9ed3176f097dd7354d5853bbc53501ad` |
| `w13nc_blade_tempest_ultimate_3d` | `38fe97ca048e13442fabcdc61157d095` |
| `w13nc_demon_gate_boss_3d` | `64054aafa7124df89cdd3f5d698f1693` |
| `w13nc_dragon_breath_ultimate_3d` | `978ed9bc2c5ab753a28a7e5f58d0a2c9` |
| `w13nc_frozen_domain_ultimate_3d` | `2b794717f4a58a57989e86556421e9fe` |
| `w13nc_judgement_ray_ultimate_3d` | `32a7a066876d1730693795f0f02d7876` |
| `w13nc_meteor_shower_ultimate_3d` | `63159878b916410e5aa796f02b77ca3f` |

### 6.3 executeMethod 后的隔离生成物

- `Assets/VFX/Shared/W11W13NextCandidate/Materials.meta`、4 个 MAT 与各自 `.mat.meta`；
- `Assets/VFX/Generated/W11W13NextCandidate.meta`，以及 20 个 `<id>.meta`、Prefab、Prefab meta；
- `ProjectSettings/VFXComposer/BuildManifests/<20 new-id>.manifest.json`；
- 三个新 Preview Scene 与各自 `.unity.meta`。

不同步 `Library`、`Logs`、`Temp`、`UserSettings`、`.codex_tmp`、任何 W24 evidence/write-once 目录，也不同步旧 W11/W12/W13 Prefab、Scene、Recipe、Manifest 或拒绝报告。

## 7. 隔离 Unity 与 canonical-overlay 终审（2026-08-25）

第 3、4 节中的“未执行/预期”是源码停笔时的历史边界。主 Goal 随后在隔离 shadow 完成全包机器门，并针对 W13 依赖闭包做第二轮窄修复与独立终审。

### 7.1 初次全包机器门

- Build 退出码 0；20 Recipe / 20 Prefab / 20 manifest、三组 `7/7/6` 与三座 Preview 均存在。
- 定向 Edit/Preview/Play 为 `4/4 + 1/1 + 7/7`；20 份共享回归 XML 合计 `96/96`，failed/skipped 为 0。
- W11/W12 载体、预算、Preview 与生产 Prefab 清理均通过；W13 四阶段、依赖池、camera hints、named gates、Meteor 六实例和 Blade 八 Slash 实例均通过。

初次终审随后发现 W13 使用的 17 个旧依赖中，shadow 有 13 份 manifest 与 canonical 字节不一致，Slash 还有 6 个实体/meta 漂移。候选在 shadow 内虽自洽，但若只晋升候选文件，canonical 首次 Build 会重写六个 W13；因此该批不作为最终 canonical-overlay 证据。

### 7.2 W13-only canonical-overlay 修复

- Canonical 新增 `BuildW13ForBatch`；Authoring/Edit/Preview 三份当前 SHA-256 分别为 `B8CC175C8CAAD1588486633A9D47CD6D2E7E3E723F043BBBBE8ABC285CE2BD35`、`EBB296EE324FD49B1E31E3E6E6BF53223C848A959824F1A0C1C47704E1C764F6`、`A80C21F00F3FB68274DB6380C50C15F0D4DDA7F74F64D9C4AB718C63CDDAAA65`；完整 Editor 与 EditMode Roslyn 均 exit 0。
- Shadow 仅用 canonical 覆盖 13 份旧依赖 manifest 与 Slash 的 6 个已证实漂移文件；17/17 dependency manifest、runtime Prefab/GUID 与 Slash 本地闭包共 95 项字节复核 mismatch 0。Overlay 是只读权威输入，不属于候选晋升。
- W13-only Build 两次均 exit 0。方法级门禁：W13 Edit `1/1`、W13 Preview `1/1`、timeline Play `1/1`、DemonGate/Blade Play `1/1`。
- 保护快照覆盖 205 个文件：17 个旧依赖闭包，以及 W11/W12 next outputs、manifests、Preview、materials；Build、四项测试和再次 Build 前后 changed 0。
- 由 canonical Recipe、compiler 四源和 17 份 manifest 原始字节独立重算，六个 W13 recipeHash/buildHash `6/6` 精确匹配；31 个 serialized sourcePrefabs 按 canonical runtime path、GUID、root fileID 和重复顺序 `31/31` 对齐。
- 六个生产 Prefab 均只有 Intro/Primary/Release/Tail 四个直接阶段根、controller 1、preview driver 0；峰值均低于 `200 particles / 10 PS / 10 materials / 14 renderers`。

独立终审结论为 canonical-overlay/30-file staging **GO**（P0=0、P1=0）。唯一 P2：W13 Preview 每次跨进程 `NewScene + SaveScene` 会重分配 local fileID，Scene SHA 观察为 `6734… → E466… → 8B89…`，虽然 `.meta`/GUID 稳定，因而不得宣称全部 30 文件跨进程字节可重现。该 Scene 只用于 Preview，不进入候选 buildHash/production Prefab；其语义仍为 Camera 1、driver 1、entries/hitTargets 6、sequential true、replay 4 秒、selection 9 秒，Preview 与两条 Play 门均通过。

2026-08-26 已按该生成物白名单把 30 个**全新**文件机械晋升到 canonical：Generated 根 meta 1、六个 W13 folder meta、六个 Prefab+meta 12、六个 manifest、Materials meta 1、MAT_W13+meta 2、W13 Preview+meta 2。复制前目标 `0/30` 存在；复制后 `30/30` 存在且与已审 shadow 源逐文件 SHA-256 mismatch 0，总字节数 `362,709`，按白名单顺序对 `{path,sha256,bytes}` 紧凑 JSON 计算的 set hash 为 `434c8adf1a7ec693ce9601f17818668deb10484c2c330cee24d2d1666777e343`。本次没有启动 Unity，也没有复制 overlay 输入、W11/W12 输出、17 个旧依赖、Slash closure、Library、Logs、Temp、UserSettings 或 `.codex_tmp`。机器 GO 与文件晋升仍不构成视觉通过，状态保持 `NEXT_CANDIDATE_VISUAL_PENDING`。
