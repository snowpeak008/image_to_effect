# 60 全项目开发经验库

状态：`1.0-draft` 生效经验  
用途：把单个特效的失败与修复递归提升为全项目设计规则、机器门禁和后续计划。具体事故证据仍保留在各 Effect Review；本文件只保存可跨 Archetype 复用的结论。

## 1. 经验提升机制

每次用户拒绝、运行时事故或资源审计失败，必须按以下顺序处理：

1. 在 Effect Review 中记录原始截图、版本、直接根因和修复证据。
2. 判断问题是否可能影响其他元素 Family、Archetype、维度或渲染后端。
3. 可复用结论在本文件登记稳定编号，并写回对应的生产/验收规则。
4. 可自动判断的部分进入 `50_MACHINE_ENFORCEMENT.md` 和测试计划；只能由人判断的部分进入 Gate 5 清单。
5. 后续新效果在 Gate 0 主动检查这些经验，不等待相同问题再次由用户截图发现。

只有“案例记录 → 全局经验 → 规范条款 → 机器/人工门禁”四层都完成，才算经验已经递归到总开发流程。

## 2. Frost Impact 2D 案例摘要

案例：`frost_impact_2d` Revision 2–3，Compiler `impact2d-14` 至 `impact2d-29`。详细证据见 `docs/vfx-reviews/frost_impact_2d/`。

- 工程测试通过但首轮画面出现不透明方块，证明资产存在不等于 Blend/Alpha 正确。
- 统一加法混合和统一衰减导致中心过曝、冰晶过暗、冰环灰白，证明模块必须按视觉职责分配能量与生命周期。
- 完整 512×512 Impact Atlas 虽便于制作，却包含可程序化或拆分复用的 Ring/Mist/Core/Mote；拆解后运行时 PNG 从 `184,844 B` 降到 `22,527 B`，纹理驻留从 `329,040 B` 降到 `66,216 B`。
- 为消除齿轮厚面而把环面 Alpha 降到 `0.025`，又把冰环错误优化成单线，证明性能优化不得以视觉退化为代价。
- 16段环形 Mesh 的边界抖动、半径采样和线性噪声没有同时回绕，在3点钟方向形成黑线；最终通过精确复用首尾顶点、圆周周期噪声和闭合测试修复。
- 用户使用 Free Aspect 观看时，构图尺度与标准16:9证据不同；固定证据链必须保留，同时还要检查常见宽高比下的裁切和可读性。

## 3. 已提升的通用经验

### EXP-001 工程通过与视觉通过必须分离

Compile、Prefab、Manifest、粒子数量、Hash 和截图存在只能证明工程链路成立。用户或授权视觉审查人未签署前，状态必须保持 `pending visual acceptance`。测试不得以“存在暖色/亮色像素”代替形状、质感、层级和动作验收。

### EXP-002 优化必须保留视觉基线

任何纹理压缩、模块拆解、Shader 替换、粒子降档或层级合并，必须在同一相机、时间线、背景和关键帧下与优化前 A/B。若 Source/Build/GPU 指标下降但主体变成细线、规则块、齿轮、重复图章或亮度断层，优化判定失败并回滚到上一可审候选。

一次迭代只解决一个主视觉问题。禁止为了消除“过厚”直接把主体 Alpha 降到接近不可见，也禁止为了消除“过暗”无上限提高 Additive 强度。

### EXP-003 逻辑拆分不等于可见分段

模块可以在拓扑、Recipe 或组合算法上拆为 `8–16` 段，但最终画面不必显示 `8–16` 条接缝。拆解的目标是复用和压缩，不是展示实现结构。连续能量环、光带、液体、笔触和云层默认要求视觉连续；只有 Brief 明确要求破碎时才暴露缺口。

### EXP-004 周期资源必须双重闭合

Ring、Aura、Area 边界、循环 Trail、圆形 Mask 和可平铺 Noise 必须同时满足：

- **几何闭合**：最后边界的内外位置与第一边界精确一致；所有边界扰动以共享边界索引计算，禁止每段独立偏移后再拼接。
- **着色闭合**：UV、Noise、法线、溶解和颜色函数具有周期性；环形噪声优先使用 `cos/sin` 圆周坐标或经过验证的可平铺纹理，禁止在线性 `U=1→0` 处留下不连续采样。
- **验收闭合**：固定检查 `0°/90°/180°/270°` 和 UV seam，放大峰值及衰减帧；不得只观察整体缩略图。

浮点距离“足够小”不能代替可证明的首尾复用。能够共享同一顶点数据时，测试应断言精确相等；无法共享时才使用有依据的屏幕像素容差。

### EXP-005 编译器行为变化必须使 Build 失效

修改粒子曲线、Mesh 生成、Shader 参数映射、模块数量或组合规则后，必须更新 Compiler Version 或把编译器内容 Hash 纳入 Build Hash。否则 Dry Run 可能把旧 Prefab 错判为 `Unchanged`。测试应证明行为变化后 Build Plan 为 Update，正式输出和 Manifest 同步变化，而 GUID保持稳定。

### EXP-006 更换 Shader 时必须清理序列化残留依赖

Material 切换 Shader 后，旧 `_BaseMap`、纹理槽或关键字可能继续留在 YAML 中，并通过依赖扫描把已废弃大图带入 Player。不得只检查 Inspector 当前可见字段；必须从新 Material 状态重写或显式清理，再通过 Manifest/AssetDatabase 递归依赖证明旧 GUID 不可达。

### EXP-007 大小必须分栏报告

PNG/源文件大小、Unity YAML、Build 磁盘、GPU 驻留和压缩近似不是同一个数字。报告至少分为 Source、Build、GPU 三栏；GZip 只能解释 YAML 可压缩性，不能冒充 Player Build。Shared 资源仍需计入“完整依赖驻留”，不能通过移动目录隐藏成本。

### EXP-008 真实播放证据必须来自用户所见链路

证据必须使用保存的 Preview Scene、同一序列化 Camera、自然 Update 和单次 Play。禁止用 Scene View 有利角度、替代相机、手工 Emit、SetParticles、跳时采样或静态 Gold 构图冒充实际播放。证据相机之外，还要记录宽高比和 Game View Scale；Free Aspect 只用于补充检查。

### EXP-009 视觉失败要转成最小防回归测试

机器测试不负责判断“好看”，但必须锁住已经确认的技术根因。例如：Alpha 非空、Blend 类型、峰值裁白上限、完成帧为空、旧纹理 GUID 不可达、环形首尾顶点闭合、周期 Shader 输入和固定相机字段。用户已经发现过的问题，不能只靠下次人工记忆。

### EXP-010 极简遮罩不能无条件拉伸到主体几何

小型 Atlas 适合粒子轮廓和局部破形，但把同一 Mask 跨长距离 Mesh 拉伸、重复叠加，压缩轮廓会显成等高线、编织线、矩形或重复图章。大面积连续能量体应优先由连续几何覆盖与程序化坐标场生成，Atlas 只控制局部粒子。程序化也不得直接叠加多组规则正弦制造“裂隙”，否则会生成渔网；应使用连续噪声扰动，并由人工关键帧检查重复结构。

发现线圈/网格/方块时，必须停止继续调亮度，先定位是 Mask 拉伸、Blend、几何拓扑还是规则函数。废弃实验 Mesh/Material/Texture 必须从生成入口和磁盘同时清理，并用正式 Manifest 的递归依赖证明不可达。

### EXP-011 Idle 状态必须由序列化资产保证

如果 Runtime Prefab 的 Renderer 默认启用，而透明度只由运行时 `Awake/Start` 清零，Unity 未播放的 Scene/Prefab 视图会直接暴露底层 Quad、环形 Mesh 和共享材质。Idle 不可见必须写入 Prefab 的序列化状态：所有受控 Renderer 默认关闭，只有 `Play/Start` 统一开启；AllowTail 完成、Immediate Stop 和 Pool Reset 必须再次关闭。机器测试同时检查 Prefab 静态状态、Play 后启用和 Stop 后关闭。

### EXP-012 多类型验收墙必须同步“可见”，不能只同步“存活”

九宫格、技能墙和批量预览中的 `IsAlive=true` 不代表屏幕上存在可比较内容。Projectile 可能尚未进入 Travel，Impact/Slash 可能已经结束，Trail 也可能已经离开自己的格子；如果只检查生命周期状态，机器测试仍会放过空格、越界和错峰。

批量视觉验收必须同时满足：固定 Cell 边界与标签、统一序列化相机、自然 Update、至少一个所有 Runtime Entry 都有可见主体的同步关键帧、One-shot 重播编排、完成帧清空，以及逐 Cell 的屏幕占用/越界检查。Gallery Driver、标签和布局只能存在于 Preview Scene，必须被生产规则禁止进入任何正式 Runtime Entry；九宫格是比较工具，不得把九个效果合并成一个生产 Prefab。

### EXP-013 透明遮罩只能衰减一次

使用 `SrcAlpha / OneMinusSrcAlpha` 或 `SrcAlpha / One` 时，如果 Shader 已把 RGB 乘过 Mask，再把同一个 Mask 写入 Alpha，硬件混合会再次乘 Alpha，得到近似 `mask²`。这会让边缘、烟雾、笔触和低亮层异常发黑，调高颜色又会导致核心过曝。

标准做法是明确覆盖率的唯一归属：常规 Alpha/Additive 路径由 Alpha 承载 Mask，RGB 保持未预乘；只有材质、Shader 和 Blend State 全部声明为预乘模式时才允许 RGB 预乘。验收必须包含灰阶边缘、峰值裁白率和暗背景缩略图，禁止只看中心亮区。

### EXP-014 重型序列化组件必须评估共享嵌套 Prefab

ParticleSystem、复杂 Trail、VFX Graph、Animator 或大型 Renderer 配置如果在多个 Runtime Entry 中完全相同，不得默认复制进每个正式 Prefab。应先做两种候选的 Source/YAML 与递归依赖 A/B：独立深拷贝可保证隔离，但共享嵌套 Prefab 可显著减少重复序列化。

共享只适用于真正相同且允许统一升级的组件；效果参数仍由根控制器或实例覆盖提供。Manifest 必须同时记录正式 Entry 和共享依赖，Build Hash 必须包含共享 Prefab 的递归依赖 Hash。若共享资产变化会造成不可接受的跨效果传播，则回退到深拷贝并记录 ADR，而不是隐藏这一权衡。

### EXP-015 通用动画不得覆盖类型的空间语义

把同一套 Transform 旋转、缩放或位移循环应用到所有 Renderer，会破坏各 Archetype 的拓扑：Area/Spawn 的地面层可能翻成竖面，Beam 端点可能漂移，Shield 壳和内部图案可能脱离，Screen/UI 布局可能整块旋转出安全区。

共享 Controller 只共享生命周期、事件和清理，不共享未经声明的运动。每个 Profile 必须定义允许变化的轴、空间、锚点和所有者：地面环只绕法线旋转；Trail 由运动头写历史；Beam 由端点写长度；Shield 命中脉冲不改变宿主 Bounds；Screen/UI 只动画局部 Accent，不动画安全区根节点。测试至少覆盖一个持续周期后的朝向、Bounds 和锚点。

### EXP-016 Screen/UI 必须作为屏幕布局验收

Screen/UI Runtime Entry 不能当作普通世界空间 Quad 验收。必须声明 Canvas 模式、Reference Resolution、安全区、Mask 和排序；证据在至少 `16:9 / 4:3 / 1:1` 下检查边缘、中心反馈和关键 UI 避让。全屏边缘或安全区根节点不得加入通用旋转/缩放数组。

Unity UI 的正式依赖来自 `Packages/com.unity.ugui/`，应以精确官方包根进入依赖白名单并记录到 Manifest；不得为了通过审计放宽为整个 `Packages/`。保存 Prefab 时若 `ScreenSpaceCamera` 因没有场景相机退化，场景装配与 Runtime Entry 必须在播放前显式恢复模式并绑定相机。

### EXP-017 共享资源不等于共享主轮廓

Shader、Material、基础 Mesh 和 ParticleSystem Prefab 可以共享，但不同 Archetype 不得把同一个圆环、Quad 或球壳作为主视觉后只换颜色。共享的是构件与实现，不是视觉身份。

每个 Archetype 必须至少有一个独立的主轮廓和主运动：Aura 是围绕宿主的垂直包体；Area 是贴地的持续覆盖；Shield 是承受命中的封闭壳体；Spawn 是地面入口与上升过程；Environment 是无卡片边界的空间体积；Screen/UI 是真实屏幕边缘与中心反馈。九宫格验收应同时检查灰阶轮廓差异，不能只检查颜色和像素存在。

### EXP-018 软粒子卡必须证明边界不可见

雾、烟、光晕等 Billboard/Quad 即使使用程序化噪声，也可能因为 Alpha 未在四边归零、非预乘混合错误或 UV 衰减不足而暴露矩形卡。任何软粒子 Shader 必须在 UV 四边强制 Alpha 为零，并在暗背景、高对比度和动画全周期下检查矩形边界。发现硬边时优先修覆盖率和边缘衰减，不得用降低整体亮度掩盖。

### EXP-019 正式 Screen/UI 资产不得写死 Gallery 布局

Screen/UI Runtime Entry 的正式 Prefab 必须以全屏安全区为默认布局。九宫格、缩略图或对比墙需要的锚点、裁切和缩放只能作为 Preview Scene 的实例覆盖，禁止写入正式 Prefab。否则所谓 Screen/UI 实际只是“右下角单元格 UI”，脱离 Gallery 后无法使用。

Screen/UI 必须同时提供独立全屏预览场景。九宫格只验证类型覆盖和同步播放，16:9 全屏场景才是边缘晕影、方向提示、中心反馈、遮挡比例与衰减时序的权威视觉入口。

### EXP-020 Shader 与 C# 的 SmoothStep 语义不同

Shader `smoothstep(edge0, edge1, value)` 是阈值函数；Unity C# `Mathf.SmoothStep(from, to, t)` 是按已归一化 `t` 在两个输出值之间插值。直接把 Shader 写法移植到 C# 会让本应为零的阶段提前可见，破坏 Dissolve、Dash、Telegraph 和多阶段技能时序。

C# 阈值必须显式写为 `Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge0, edge1, value))`，窗口必须由进入门与退出门相乘。测试至少覆盖进入前、窗口内和退出后，不能只看动画中间帧。

### EXP-021 同一 Unity 项目的 Batch 进程必须串行

Compile、Build、EditMode 和 PlayMode 即使看似独立，也会竞争同一项目的 Library、AssetDatabase、测试结果和退出流程。禁止对同一 `projectPath` 并行启动多个 Unity Editor/Batch 进程；并行只允许用于 Unity 之外的只读分析。

若误并发导致退出卡住，只能在核对精确 PID、启动时间、命令行和项目绝对路径后终止该 PID，并将本轮结果作废。不得因为其中一个日志看似成功就沿用部分结果，必须清理旧 XML 后串行重跑。

### EXP-022 扩展性保护测试不得把正式输出目录写死

“保护旧成品”应断言旧 Recipe Hash、Runtime Entry GUID、Manifest/目录内容 Hash 和必要依赖保持不变；不应断言 `Generated` 永远只含当时的三四个目录。后者会把计划内新增能力错误报告为回归，并迫使开发者在“删掉新功能”和“跳过旧测试”之间做错误选择。

集合断言必须与意图一致：保护项使用 `protected ⊆ current`，临时输出则单独断言其精确前缀不存在。只有产品规格明确声明封闭集合时才允许 `current == frozen`。

### EXP-023 隔离历史测试必须显式声明规则代际

把旧成品 Recipe 复制成临时 ID 做 Patch、回滚或 AI 证据验证时，它的层级和本地材质可能天然不符合后来启用的 strict 输出规范。此类测试必须使用稳定、精确、可审计的隔离 ID，并在项目规则中登记 `legacy_audit`；不得为了让历史测试通过而放宽所有新 EffectId 的 strict 默认规则。

隔离例外只豁免结构/推荐预算迁移，不豁免 Missing Script、Editor/Preview 组件、越界依赖等运行安全错误。测试完成后必须只清理自己的 Recipe/history/output，不能用“Generated 最终等于旧固定集合”作为清理手段。

### EXP-024 能力测试夹具必须先是合法完整 Recipe

测试非法组合、参数越界或 Archetype 冲突时，应从一份通过 Domain/Manifest 校验的正式 Recipe 深拷贝，再只变异目标字段。空 module、缺 stage 或虚构模板会先触发无关必填错误，让测试看似“成功拦截”却没有验证目标能力规则。

每个非法组合测试必须断言目标稳定错误码和精确路径；仅断言 `HasErrors=true` 不足以证明合法性表工作。

### EXP-025 Recipe 参数必须服从正式 Manifest，不能反向放宽模板迁就素体

能力素体使用现有模板时，仍必须服从模板公开的 min/default/max。素体想做得更小、更淡或更短，不构成放宽 Manifest 安全范围的理由。首次构建失败时先校对 Recipe 值与 binding 声明；只有产品需求证明模板范围本身错误，才通过独立契约变更修改 Manifest。

### EXP-026 Preview Scene 测试只依赖自有稳定标识

Prefab 实例在 Scene YAML 中不保证保存源 Prefab 根对象的可搜索名称。批次预览应创建自己拥有的稳定 `Cell_<index>_<id>` 根节点，并以该节点、Prefab Source GUID 和 Runtime Entry 组件做断言；不得扫描 YAML 中偶然出现的 `m_Name` 作为场景完整性证据。

Preview Scene 可构建、Cell 数量正确和 Runtime Entry 引用正确仍只属于工程验收。视觉质量、同步播放和可读性继续由用户在最终视觉阶段签署。

### EXP-027 共享风格资产必须作为依赖，而不是复制成局部输出

风格变体只改变材质参数、共享 Shader、共享 Mesh/Mask/LUT 时，正式 Runtime Entry 的 `ownedOutputs[]` 只拥有自身 Prefab 与真正局部资源；共享材质和纹理必须登记在 `dependencies[]`。不得为了让每个特效“看起来完整”而复制一套材质或 PNG 到每个 Generated 目录，否则会重新引入此前已经确认的体积膨胀和所有权清理风险。

测试应同时证明局部纹理字节为零、`ownedOutputs[]` 没有局部 `.mat`，并验证共享依赖可解析。Manifest 指标字段不得由测试臆测；断言必须以当前 Manifest 契约中的 `localTextureBytes`、`dependencyResidentTextureBytes` 和 `ownedOutputs[]` 为准。

### EXP-028 编辑器工作流不得代替用户签署视觉结论

Studio 的 Review 页可以运行 Schema、幂等、预算和生命周期机器检查，也可以记录人工复选项，但它不能自动勾选形状、层级、动作、消散和深度感。只有用户明确勾选全部人工项、填写 reviewer 并确认写入时，才允许生成 `REVIEW.md`；在本轮“集中到最后验收”的安排下，所有新内容必须保持 `pending user visual acceptance`。

### EXP-029 图形证据录制器不得混入普通 PlayMode 回归

调用 `Camera.Render`、写 PNG 或依赖真实图形设备的测试属于按需证据录制器，必须标为 `Explicit`。普通 PlayMode 回归只验证运行时状态、事件、生命周期、确定性与清理；否则无头批处理可能在 URP 粒子绘制的原生层崩溃，既得不到 XML，也会把“录图环境问题”误报成产品逻辑失败。

每个录图器都必须保留可按名称显式运行的入口。最终视觉验收需要证据时，使用图形模式串行执行它；全量自动回归不得隐式重录或改写已有视觉证据。

### EXP-030 新 Archetype 的价值必须体现在外部驱动协议，而不只是新轮廓

Decal、WeaponTrail、LifeCycle、Portal 和 Loot 如果只有不同 Mesh/颜色，仍然只是旧类型换皮。每类必须暴露其不可替代的 Runtime 协议：Decal 的 surface key/法线/oldest-first 堆叠，WeaponTrail 的双端点与速度阈值，LifeCycle 的外部 Renderer MPB 注入，Portal 的 pair id/role，Loot 的 rarity 与外部 pickup endpoint。Destruction 的核心则是相同 seed/输入下确定性的表现层假物理，禁止用 Rigidbody 偶然运动替代。

验收先断言这些行为，再把人眼视觉留到最终阶段。Preview Driver 只负责提供测试输入，必须被生产规则禁止进入 Runtime Entry。

### EXP-031 Patch 构建器必须按成品所有权分派，不能只看是否存在 style 字段

Recipe 有 `style` 不代表它由 Styled Compiler 拥有；旧 formal fireball 在兼容迁移后同样可以带 style，但仍必须走原编译器和原 Prefab 命名/层级。Patch 事务应依据新 Archetype、已存在 Manifest 的 `compilerVersion` 或明确的受管 Recipe 路径选择构建器。只按 `style != null` 分派会改变旧输出文件名大小写，破坏 GUID/测试并绕过注入式故障编译器。

新增 `set_archetype_param` 时，Patch 的前置和后置验证都必须调用同一 `ArchetypeParameterRegistry`；事务仍遵守 Generated 快照、先构建后提交文本和失败回滚。拾取到达判定触发 Stop 前必须把位置精确吸附到目标点，不能在容差范围内提前停下留下可见偏差。

### EXP-032 内容语义、行为能力与视觉风格必须正交

元素族不能把火、冰、电的语义参数塞进 `behavior` 或 `style`。`behavior` 只描述轨迹、命中、发射和时序；`style` 只描述渲染语言与调色；`content` 才描述 `spark_count`、`chain_count`、`poison_tick` 等成品语义。三者分别由注册表校验，才能让同一个能力素体安全换元素和风格，而不复制运动逻辑。

内容 Patch 只能使用 `/content/parameters/{registeredParameter}` 的稳定路径并替换已有字段；新增、改名或跨成品挪用字段必须失败。每批至少验证一个合法 Patch 和一个精确 `E706` 非法目标。

### EXP-033 规格登记不等于运行实现，能力必须由采样断言闭环

W3–W8 运行测试发现 `dash` 已存在于 Capability Registry，但采样器此前落入默认直线积分，导致 distance/duration 契约没有生效。能力 token 的完成条件必须同时包括：注册表、Recipe、纯逻辑 sampler 分支、事件/轨迹断言和 Runtime Entry 接线。任何一项缺失都只能算登记，不能算能力完成。

### EXP-034 批处理测试路径和场景隔离属于证据契约

Unity 子进程工作目录不可靠，`-testResults` 必须传绝对路径；否则测试可能实际运行却被误报为“XML 未生成”。EditMode 场景检查不得在未保存的 untitled scene 上创建 Additive scene，也不得关闭最后一个加载场景。只读批次检查应顺序用 `OpenSceneMode.Single` 打开正式 Preview，避免产生临时 Scene 资产或卸载警告。

### EXP-035 Runtime 协议必须改变真实几何或渲染状态

把 `source/target`、`bounce`、`fill_ratio` 等字段存进组件不等于能力完成。端点束必须让 LineRenderer 的采样点发生变化，奖励飞入必须让 UI 元素沿弧线移动，格挡反弹必须实际启用 ParticleSystem Collision。每项协议都要断言可观察状态，而不是只断言字段被钳制。

### EXP-036 Canvas 内容与世界内容必须使用不同的运行时构件

Screen/UI 与 Game/UI 的正式 Runtime Entry 必须使用 Canvas/Graphic、安全区和 RectTransform 协议，并保持零 ParticleSystem；Environment/Hit Feedback 才使用 Renderer、粒子和世界端点。共享控制器可以统一生命周期，但不能让 UI 退化成摆在相机前的 Quad，也不能让世界特效依赖屏幕分辨率。

### EXP-037 MonoBehaviour 文件名是序列化契约的一部分

用于 Preview 的 MonoBehaviour 若作为同文件第二类型存在，Unity 可能在场景重载时无法按脚本资产恢复引用。每个需要序列化到 Scene/Prefab 的 MonoBehaviour 必须位于同名 `.cs` 文件；预览驱动仍由项目规则禁止进入生产 Runtime Entry。

### EXP-038 编译器实现变化必须进入 Build Hash

Recipe 未变并不代表输出应 Unchanged。任何改变 Prefab 结构、粒子模块、运行时接线或资源依赖的编译器代码修改，都必须改变 `compilerVersion` 或由可重复的实现签名进入 Build Hash；否则旧 Manifest 会错误短路重建。测试应在版本变化后先确认一次 Update，再确认第二次 Unchanged。

### EXP-039 风格参数必须改变时间或材质协议，不能只登记 token

像素、手绘图集和蒸汽机械分别需要按 `snap_fps / atlas_fps / step_fps` 离散时间；全息故障需要同 seed/时间片确定性；幽魂需要受控透明节拍。风格 token 只有在 Parser、Validator、共享材质、Runtime 参数和机器断言全部接通后才算完成。

### EXP-040 风格扩展共享的是构件，不复制成品贴图

图集、符号、星云、星点、Facet 与 Gear 必须集中在 `Shared/Styles`，Manifest 以 dependency 引用，成品 `localTextureBytes=0`。小型程序图集优先于为每个成品生成一张大图；变体只能新建 Recipe/Prefab，原 default Recipe 与 Manifest 必须逐字节不变。

### EXP-041 Composite 必须引用依赖，不能复制子特效层级

组合大招与角色套装只拥有控制器、时间轴描述和子 Runtime Entry Prefab 引用。首次播放建立固定实例池，复播只 Reset/重用；复制子 Prefab 层级会同时破坏更新传播、资源所有权、内存预算和 Detach/Bake 的未来边界。

### EXP-042 组合预算必须按真实存活区间计算

峰值不能把各子项总成本简单相加，也不能假设一个阶段结束就会自动消失。应使用子 Manifest 的 duration 与时间轴 play/stop 计算同一时刻的活跃集合；为降低峰值加入的 stop 必须在 Runtime 使用确定性的 Immediate 语义，否则 Manifest 和实际画面会分叉。超峰值应先调整编排和复用，不得先放宽门禁。

### EXP-043 依赖路径只能来自子 Manifest 的 Runtime Entry

`Assets/VFX/Generated/<id>/VFX_<id>.prefab` 只是常见约定，不是权威路径。历史资产可能保留大小写或专用文件名；组合编译器必须读取子 Build Manifest 的 `runtimeEntry.path`，再把真实路径和 dependency hash 写入自己的依赖图。根据 id 猜文件名会在 Windows 上侥幸加载，却在字节审计或大小写敏感平台失败。

### EXP-044 行为合法性由能力矩阵决定，不能由视觉名称推断

看起来像“冲刺斩”不代表 Slash 可直接声明 `motion.dash`，看起来像“环形盾”也不代表 Shield 自动支持 `emission.ring`。新成品必须先查 Capability Registry 的 Archetype × motion/hit/emission/timing 合法表；需要新组合时先扩能力协议和采样测试，不能为了一个画面绕过 Validator。

### EXP-045 Composite Patch 必须走组合专用预检与事务构建

Composite 的总时长和峰值档不同于 Projectile；通用 DryRun 会把合法 Boss 演出误判为移动端超时。Patch 前置验证应使用组合注册表、依赖存在性与 simultaneous peak；事务构建仍保留 Generated 快照、先构建后提交 Recipe/history 和失败回滚。

### EXP-046 Unity 2022 PlayMode 结果与进程退出必须双门禁

全量 PlayMode 曾两次写出 43/43 通过 XML，却在 Editor 关闭阶段由已完成的 Scene AsyncOperation Finalizer 访问已销毁 Scene Manager，返回原生异常退出码。测试程序集应在 OneTimeTearDown 中、引擎仍存活时主动完成 GC finalizer drain；验收必须同时要求 XML `failed=0` 与 Unity 进程 exit 0，任何一项单独成功都不能冒充通过。

### EXP-047 Unity 2022.3 URP 类型化诊断必须自持矩阵、格式与坐标约定

离屏诊断相机的 View 矩阵必须包含 Unity 相机空间的 Z 翻转，使相机前方对象落在负 Z；URP 中不得依赖 `CommandBuffer.SetViewProjectionMatrices` 或 `Camera.AddCommandBuffer`。诊断 Shader 必须接收显式 View/Projection 矩阵，并通过 `Graphics.ExecuteCommandBuffer` 在已设置的 Render Target 与 Viewport 上执行。CPU 投影历史与 Render Target 像素比较还必须依据 `SystemInfo.graphicsUVStartsAtTop` 统一 Y 原点；禁止用碰巧一致的截图方向代替坐标契约。

`R32_UInt` Object-ID 路径必须使用 `Blend Off`、ShaderLab `Integer` 属性和 `Material.SetInteger`；浮点属性、开启混合或隐式格式转换都可能得到无报错的全零缓冲。必需 ID 必须在真实深度测试下物理可见，语义标记被主体遮挡时应调整标记几何或取证机位，禁止关闭深度伪造可见性。Object-ID、Depth、Trail/Renderer Mask 等类型化证据不得回退为 Beauty/RGBA 截图或按颜色猜测。

### EXP-048 正式诊断实现变化必须使旧证据失效，机器通过不授予视觉签署

任何影响正式捕获、Shader、坐标映射、对象标记或证据验证的源码修复，都必须先把旧候选与旧证据归档为 rejected，再提升工具版本和相应 Contract revision，重签源码/合同/证据 Hash，并从干净候选重新运行；禁止让修复后的验证器继续消费修复前的证据。

类型化证据和机器门禁通过只能证明工程协议成立，不能证明特效形状、层级、质感、时序或设计一致性合格。用户视觉签署权始终独立保留；机器 `pass` 不得自动写成用户视觉 `accepted`。

### EXP-049 为通过诊断而新增的可见物，必须先成为合同视觉层

当类型化 Mask、Object-ID 或受光量测暴露“正式特效没有可绘制主体”时，不能临时塞入一个 Mesh 只为让诊断出现非零像素。任何会进入 Beauty、Runtime Prefab 或最终玩家画面的载体，都必须先在 Design Contract 中声明责任、几何、材质、时序、挂点和预算，并在 Implementation Trace 中绑定精确 Runtime 路径；否则它仍是未授权的诊断替身，即使机器量测通过也必须拒绝。

共享材质的自发光尤其要隔离：材质资产保持中性默认值，正式主体由运行时、逐 Renderer 的 `MaterialPropertyBlock` 注入 HDR emission；受光 Receiver 不得继承这份自亮参数。Editor 中临时设置的 PropertyBlock 不是可靠的 Prefab 序列化合同，必须由 Runtime Entry 在 `Awake`/`OnEnable`/`Play` 重新应用，并用清理、共享材质不污染和精确层绑定测试共同证明。

### EXP-050 多拓扑事件必须驱动真实可见载体，不能停在 Trace

`on_split`、`on_emit`、`on_hit(n)` 的数量、时间和方向全部正确，仍不等于玩家能看到分裂、齐射或跳跃。凡是一个事件代表一个独立子弹、源点、目标或分段，Runtime 必须把事件接到真实 Renderer/Particle/Line/Transform 状态；单一 Core 加单一 Marker 反复覆盖只能证明事件消费，不能证明拓扑执行。

同屏数量较小且预算敏感时，优先用一个手动批量 ParticleSystem 承载多实例，并提供数量、位置、方向、尺寸和当前模式的确定性读回；测试必须把这些读回逐项与 Trace 的 `After/Position/Time` 对齐，同时断言 Stop/Reset 清空、峰值粒子、PS 和 Renderer 预算。需要一个素体演示多个模式时，模式顺序、参数与时长必须进入 Recipe Stage/behavior，并由 Manifest 的 Recipe hash/revision 与 compilerVersion 绑定；禁止把三段演出只写在 Preview Driver 或按 EffectId 隐式特判。

Gallery 还必须缩放或裁切 Runtime Entry，使最大轨迹和多实例包络留在所属 Cell 内，并明确显示 `visual_pending`。机器可证明载体存在、协议一致和不越预算，仍不能把新候选自动标成用户视觉通过。

### EXP-051 Trace 数据必须拥有对应的有界可见载体

Trace 中的 alpha、宽度、亮度、反射方向、汇聚源和 hop 时间只有被 Runtime 写入真实 LineRenderer、ParticleSystem、Transform 或 MaterialPropertyBlock，才构成视觉执行。测试既要对齐 Trace 与公开读回，也要读取真实 Renderer/PropertyBlock 状态；仅由同一组件回报“已执行”的字段仍可能是自证循环。固定数组、固定子 Renderer 或一个手动粒子批次优先于逐帧 Instantiate，并必须同时门禁 Stop/Reset 清理和峰值预算。

### EXP-052 端点、阻挡与多段拓扑是 Runtime 协议，不是 Preview 演出

持续束应公开 Source/Target 的 Transform 与显式坐标协议，并把长度、平铺和响应帧数作为读回；扫射不能在采样后被通用固定端点覆盖。遮挡必须使用明确拥有者的物理/障碍 probe，取首个 blocker，未配置时 fail-closed，不能悄悄回退到合成距离。反射、汇聚和跳线必须分别拥有真实分段线、多源线或有界多点线；Preview Driver 只可移动端点/障碍或发出 Start/Stop/Cancel 输入，不得暗藏正式运行语义。

### EXP-053 已拒绝候选不可覆写，后续候选必须另立待签状态

用户拒绝记录、原话、当时候选状态和已确认技术原因必须保持原文；后续授权重做时新增独立小节、compiler version 和状态根，不能把旧记录改成“已修复所以通过”。新候选即使完成静态编译、机器测试和确定性读回，也只能标为 `next_candidate / VISUAL_PENDING`；在用户视觉签署前不得出现 `ACCEPTED`，不得提升到 L3/L4，也不得把未来待运行命令写成现有证据。

### EXP-054 视觉槽引用必须落到固定容量的真实载体

`impact_slot`、`tick_visual_slot` 或 `residue_slot` 通过 Recipe 引用校验，只能证明依赖合法；如果 Runtime 仍只保存字符串，玩家看不到组合结果。Compiler 必须把已解析槽绑定到正式 Runtime Entry 内的固定容量载体，并公开配置 ID、绑定结果、执行次数、序号、可见数量和位置。测试还要从真实 ParticleSystem 反读数量与位置，与执行器读回逐项交叉，避免组件自报形成自证循环。

槽载体应一次创建、循环复用，Stop/Reset 清空，峰值容量和 Renderer/PS/材质引用同时受预算门禁；Preview 不得临时实例化槽视觉来掩盖正式 Prefab 缺线。

### EXP-055 可中断能力的结束必须是可观察尾段

`Complete` 与 `Cancel` 两个方法和两个事件字符串并不足以完成双出口协议。蓄力释放/取消、引导收束/溃散、移动区域完成/撤销必须在 Runtime Entry 内形成不同的真实几何或粒子尾段，并在有限时间后自动清理；测试应在 Stop 后检查尾段名称、物理载体和结束时清空。Pool Reset 还必须清除上一个外部拥有者传入的路径等状态，要求调用方在下一次 Play 前重新绑定。Preview Driver 只负责按顺序发输入，不能自己画尾段。

### EXP-056 Gallery 有界布局必须用能力最大包络反推

有半径、路径或多点拓扑的能力不能沿用通用 Gallery 偏移。应以合同最大半径/路径乘 Entry scale，连同锚点偏移、标签安全距和 Cell 边界一起做数值门禁；预览场景再用稳定状态根标明新旧候选。缩放只能解决展示隔离，不能替代 Runtime 中真实的形状、事件层和退出演出，也不能自动形成视觉结论。

### EXP-057 多段反射与移动遮挡必须按物理可观察状态取样

`N` 段反射折线最多只能消费 `N-1` 个 bounce 顶点。先读取更多 bounce、最后再 clamp 段数，会错误地把“下一次反射后的出射方向”用于当前可见尾段，使末段反折或越界；构造每一段时必须同步限制事件消费数、折点数和 LineRenderer 段数，并逐段核对方向与衰减。

Unity 2022.3 中，代码移动 Transform 后在下一次 FixedUpdate 前读取 `Collider.bounds` 可能得到旧包围盒。要求移动障碍后两帧内延伸/截断的显式有界 occlusion probe，必须在读取前调用 `Physics.SyncTransforms()` 或使用等价的显式同步协议；否则测试中的 Transform 已改变，而 Runtime 仍会消费陈旧遮挡位置。

## 4. 对后续所有特效的强制检查

以下检查适用于 Projectile、Impact、Slash、Aura、Area、Beam、Trail、Shield、Spawn、Environment 和 Screen/UI：

- Gate 0：记录视觉角色、能量层级、`whole/segmented/procedural` 决策和连续性要求。
- Gate 1：先冻结参考图与可测关键帧，再做资源优化。
- Gate 3：构建后审计真实递归依赖，禁止废弃纹理隐性残留。
- Gate 5：人工比较形状、质感、层级、时序、接缝、重复和常见宽高比。
- Gate 6：同时报告视觉差异与 Source/Build/GPU 收益；视觉失败时性能收益不得使候选通过。
- Gate 7：Compiler 行为变化必须触发 Update；幂等只适用于真实相同输入和行为。

## 5. 当前机器化缺口

以下项目已成为正式计划，但在实现完成前必须人工执行，不能误称自动覆盖：

- 通用 Ring/Tile Mesh 首尾精确闭合审计；
- Shader/纹理周期采样声明与 seam 截图检查；
- Compiler 内容 Hash 自动进入 Build Hash，减少人工版本号遗漏；
- 固定16:9之外的 4:3、1:1、超宽预览裁切检查；
- 优化前后关键帧感知差异和模块屏幕占比报告。
