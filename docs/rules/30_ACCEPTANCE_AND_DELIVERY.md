# 30 验收、性能与交付

## 1. 验收顺序

所有特效按同一顺序通过门禁（与下文 Gate 编号一一对应）：

```text
Gate 0  Brief/Reference
Gate 1  Visual Spike + 用户视觉方向确认
  （Recipe/Template/Compiler 集成）
Gate 2  Runtime/WYSIWYG
Gate 3  结构与依赖
Gate 4  生命周期与池化
Gate 5  视觉验收（正式签署）
Gate 6  性能
Gate 7  构建与回滚
Gate 8  Player 与发布
```

Gate 1 确认的是视觉方向；Gate 5 是基于 Runtime 成品的正式视觉签署，两者不可互相替代。禁止在 Gate 1 视觉方向未确认前投入完整回归、逐帧审计和发布工作。

## 2. Gate 0：Brief

MUST 记录：

- Archetype、Dimension、Lifecycle、Topology、Attachment；
- 参考图只约束哪些特征；哪些元素明确不做；
- 动作方向、持续时间、峰值时间和消失时间；
- 颜色层级、主体轮廓、次级元素数量级；
- 目标相机、背景、分辨率和平台 Profile；
- 用户可观察的通过/失败条件。
- Module Decomposition Table：每个视觉角色、来源、变体、Pivot/方向、目标投影像素、Atlas/复用计划；完整概念图明确标记为非 Runtime 资产。
- `60_ENGINEERING_LESSONS.md` 中适用的 `EXP-*` 编号，以及它们对应的设计约束、机器测试或人工检查；没有适用项时也要明确记录已完成筛选。

## 3. Gate 1：Visual Spike

- 只制作最小视觉样片，不先扩展正式 Schema。
- 使用最终目标渲染后端的代表性路径。
- 提供峰值帧、残影帧和短动画。
- 提供独立视觉模块与 Atlas Layout/Pack Preview；检查 Alpha、裁边、方向、Pivot 和同时出现时的重复感。
- 用户未确认前不得写“视觉通过”。
- Spike 失败可删除/归档，不进入正式 Templates。

## 4. Gate 2：Runtime WYSIWYG

- 每个 `EffectId + Dimension + Target View Profile` MUST 指定唯一权威 Preview Scene；不得在同一验收结论中混用多个未声明场景。
- 每个权威 Preview Scene 使用唯一序列化 MainCamera；可按已登记 pose table 切换多视角，但相机对象保持唯一，并明确 Clear、背景、FOV/Orthographic Size、HDR、MSAA、Culling、Post Processing。
- 真实启用 Runtime Controller，由正常 Update/事件推进。
- 禁止手工 Emit、SetParticles、强行开关 Phase、跳时 Sample 作为正式视觉证据。
- 证据采集使用同一相机和同一 Runtime Prefab。
- Scene View 自由角度只可调试，不能替代 Game 视觉验收。

## 5. Gate 3：结构与依赖

- 一个 Runtime Entry；默认一个 Runtime Prefab，非 Prefab Entry 必须有获批 waiver。
- GameObject、深度、Renderer、ParticleSystem、Trail、Material 和 Texture 数量有报告。
- 无 Preview/Editor/Test 组件。
- 无 Missing Script/Material/Shader。
- 无未引用 Local Asset。
- 无重复 Shared Asset 副本。
- Dependency Hash 与 Build Hash 可复算。
- Prefab GUID 在更新构建中保持稳定。
- Runtime Entry 不得依赖 Concepts/ArtSource；Atlas 单元和消费者契约可追溯，Local/Shared 中无无理由副本。

## 6. Gate 4：生命周期与池化

通用测试：

- 第一次 Play；
- 播放中 Stop immediate；
- 播放中 Stop allow-tail；
- 完成后返回池；
- 同一实例重复 Play `100` 次；
- 随机位置、旋转和缩放重用；
- 无跨次 Particle/Trail/MPB/事件残留；
- 无运行时新增 Material；
- Domain Reload/Scene 切换安全。

类型专项：

- Projectile：Launch/Travel/Impact、瞬移 Clear、外部 Transform 驱动。
- Impact：不同法线和表面朝向。
- Slash：翻转、不同方向、统一 Anchor。
- Aura：持续 10 分钟、Refresh/Stack/Stop、宿主销毁。
- Area：持续 10 分钟、范围缩放、地形坡度、相机进出。
- Beam：端点移动、断线、零长度、目标销毁。
- Environment：场景切换、LOD、剔除、多相机。
- Screen/UI：分辨率、宽高比、安全区、UI Mask。

## 7. Gate 5：视觉验收

视觉由人审，机器指标用于防止已知回归。

人工 MUST：

- 同参考图/Brief 对比；
- 检查时间顺序、轮廓、色彩层级、焦点和消失；
- 检查 2D 翻转或 3D 多视角；
- 检查游戏距离可读性；
- 逐模块检查参考图中的 Core/Main/Ring/Trail/Mist/Debris 是否被实现为对应层级，禁止用单层规则占位图替代多层目标后声明通过；
- 检查重复 Sprite、运动朝向、Atlas 变体选择和后期衰减；
- 对 Ring、Aura、Area 边界、循环 Trail 和平铺模块，在峰值与衰减帧放大检查 `0°/90°/180°/270°`、首尾接缝和重复周期；
- 使用权威宽高比完成正式签署，并补查 `4:3`、`1:1` 与超宽视口的裁切、尺度和可读性；Free Aspect 不能替代登记的正式视口；
- 资源优化候选必须与上一可审版本同镜头、同时间线 A/B；出现细线化、齿轮化、重复图章、接缝或亮度断层时必须拒绝，不能用体积收益抵消视觉失败；
- 明确签署 `pass / conditional pass / reject`。

机器 MAY/MUST（按类型）：

- 帧数、时长、空帧；
- Runtime Entry 在未调用 `Play/Start` 时必须序列化为不可见；不能依赖 `Awake/Start` 才隐藏原始 Quad、Mesh 或粒子 Renderer。播放后按契约启用，Stop/Pool 后再次全部关闭；
- Anchor 屏幕偏差；
- Particle live count、最大尺寸、面积、Alpha；
- Renderer 是否启用；
- 关键参数读回；
- 闭环 Mesh 首尾顶点/法线/UV 精确相等，或有依据的屏幕像素容差；
- 周期 Shader/纹理在 seam 两侧的采样连续性；
- Material/Prefab 递归依赖中已废弃纹理 GUID 不可达；
- 帧 Hash 或感知差异阈值。

机器指标不得把“像素存在”误写成“视觉好看”。

## 8. Gate 6：性能

当前静态 Profile：

| Profile | Peak particles | Materials | Trails | Duration | Local 独占 Texture | 完整依赖驻留 Texture | Overdraw | 含义 |
|---|---:|---:|---:|---:|---:|---:|---|---|
| mobile_medium | 200 | 8 | 2 | 6s | `<= 8 MB` | MUST 报告，阈值待真机 | SHOULD 报告；参考线 `3x` | 当前静态门禁，不是真机认证 |
| pc_editor | 1000 | 32 | 8 | 30s | `<= 32 MB` | MUST 报告，阈值待目标场景 | SHOULD 报告；参考线 `6x` | Editor 上限，不代表发布性能 |

- Duration 上限只适用于 `instant / one_shot` 生命周期；`sustained / looping / event_driven` 效果不受 Duration 约束，改为满足 Gate 4 的 10 分钟持续测试和下文稳定态报告要求。
- `Local 独占 Texture` 指该效果拥有的本地纹理解压后估算值；`完整依赖驻留 Texture` 是从 Runtime Entry 递归可达的全部纹理驻留估算，包含 Shared。两者 MUST 同时报告，避免以 Shared 名义隐藏单效果启动所需的大纹理。
- Overdraw 在 `1.0-draft` 期间是 SHOULD/report-only，`3x/6x` 仅为审阅参考线，超出时记录风险但不自动阻断 Gate 6。
- Overdraw 只有在固定相机、分辨率、关键帧集合、透明覆盖计算公式、采集工具及允许误差后，才可通过规则版本升级为 MUST；Editor Overdraw 视图截图本身不能作为可重复数值门禁。

每项特效 MUST 报告：

- GameObject/Renderer/ParticleSystem/Trail 数量；
- 峰值粒子和平均粒子；
- Unique Material、Shader Pass 和估计 Draw Call；
- 透明覆盖/Overdraw 风险；
- Texture/Mesh 内存；
- Texture Source 文件大小、Build 磁盘大小、GPU Resident Memory、Atlas 尺寸/格式/Mipmap/利用率/消费者；三种大小不得混用；
- CPU Update、Render Thread、GPU 时间；
- 单实例与目标并发实例数。

循环 Aura、Area、Beam、Environment 额外报告稳定态平均成本和 10 分钟趋势。真机性能认证只有在明确设备、场景、分辨率、并发数和采样方法后才能宣称通过。

报告项中未设阈值的指标（Draw Call、CPU/GPU 时间等）当前只要求如实记录，作为将来设定阈值和回归对比的基线；不得因"未超阈值"而免于记录。

## 9. Gate 7：构建与回滚

- Validate、DryRun、Build 分离。
- Build 使用 staging + atomic commit。
- 首次构建失败不得留下目录或 `.meta`。
- 更新构建失败必须恢复完整文件 Hash 和 Prefab GUID。
- 第二次相同构建的文件集合/Hash 相同。
- Stale output 只删除 Manifest 所有且无外部引用的明确文件。
- Patch 同时回滚 Recipe、History 和 Generated Output。

## 10. Gate 8：Player 与发布

源码阶段 MUST：

- Unity compile 通过；
- EditMode/PlayMode 产品测试通过；
- Windows Player Build 通过；
- 正式 Runtime Entry 及其 Runtime 测试可从 Player 加载；Preview Scene 只有被明确列为 Player 验收夹具时才要求进入 Player Build；
- Runtime 程序集不引用 UnityEditor；
- 最终 Player 不依赖 Recipe、Manifest、Preview 工具和证据。

以下按用户决定，暂不作为源码阶段硬门禁：

- Git tag；
- Package Registry；
- 外部发布；
- 多设备真机认证；
- Cocos/UE 适配。

## 11. 证据保留

活动仓库默认只保留：

- 一份最终 Brief；
- 一份最终短动画；
- 一份关键帧表；
- 一份 metadata/测试摘要；
- 一份人工结论。

失败尝试、逐帧原图、完整测试日志和 AI 原始 cohort 移到外部 artifacts/archive；Unity Assets 中不得保留视觉证据。

## 12. 验收报告最小模板

```text
EffectId / Revision / Archetype / Dimension
Recipe Hash / Build Hash / Prefab GUID
Runtime Entry kind / path / GUID
Shared and Local dependencies
Hierarchy and performance counts
Preview scene/camera
Visual review result
Automated test result
Known limitations / waiver
Final status and reviewer
```
