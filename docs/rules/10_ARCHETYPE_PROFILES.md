# 10 特效类型配置

本文件定义 Archetype 的差异规则。未列出的新类型先选择最接近的生命周期和空间拓扑，遵守 `00_CORE_RULES.md`，再补配置；不得复制整个 Compiler 建立孤立协议。

## 1. 通用 Phase 词汇

Phase 是可选语义，不是强制 Hierarchy：

| Phase | 用途 |
|---|---|
| enter | 出现、预兆、启动 |
| active | 主表现或持续状态 |
| transition | 形态变化或事件过渡 |
| exit | 收尾、消散 |
| tail | 不再发射但允许残留自然消亡 |

Archetype MAY 定义更精确的事件，如 `on_launch`、`on_hit`、`on_tick`。禁止为了兼容旧 Projectile 而把 Slash、Aura、Area 伪装成 Launch/Travel/Impact。

## 2. Projectile

特征：移动体、事件驱动、通常包含 Launch/Travel/Impact。

MUST：

- 分离 Gameplay 运动/碰撞和视觉播放；VFX 接收位置、方向、速度和命中事件。
- 支持 Launch、Travel、Impact 独立播放及完整序列。
- Travel Transform 可由外部驱动。
- Impact 后主体/Trail 正确停止，尾迹不跨对象池重用位置。
- 2D/3D 可共享语义，但 Template 必须声明 Dimension。

建议 Hierarchy：Root + Core + Trail + Embers + Launch + ImpactFlash + Burst + Shockwave，普通 `6–10` 个节点。

## 3. Standalone Impact

特征：世界点瞬时爆发，无 Travel。

MUST：

- 输入命中位置、法线、表面类型和可选强度。
- 主中心、径向释放、冲击环和残留按时间排序。
- 法线对齐不能让 Billboard/Decal 穿入表面。
- 一次性完成后返回池。

SHOULD：Root + Flash + Burst + Ring + Debris/Residue，`4–8` 个节点。Impact Only 从 Projectile 中单独播放不等于独立 Impact Archetype；必须有自己的 Recipe、Prefab 和 API。

## 4. Slash

特征：方向弧、短时间 Sweep、残影。

MUST：

- 输入起点/枢轴、方向、朝向和可选长度/宽度。
- 主体、内刃、残影、消散共享同一语义锚点。
- Reveal 必须表达扫出过程，不允许整张静态弧突然闪现冒充动作。
- 残影通过时间、颜色、UV/溶解表达；若空间滞后，必须由明确运动参数产生。
- 3D 必须多角度可读；2D 必须支持左右翻转且不破坏 UV。

SHOULD：Root + Main + Afterimage + Sparks + Dissipation + 可选 Ignition，`5–9` 个节点。

## 5. Aura

特征：持续循环、挂载角色/骨骼、可叠层。

MUST：

- 明确 Start、Refresh、Stop、AllowTail。
- 明确跟随位置、旋转和缩放的策略；骨骼丢失时安全停止或回退。
- 循环粒子不能通过 Duration 自行假装结束。
- 多个 Aura 同时存在时定义叠加、替换或合并规则。
- 进行长时间稳定性、对象池复用和角色瞬移测试。

SHOULD：Root + BaseRing + Orbiters + BodyGlow + RisingParticles + 可选 BuffIconLink，`4–10` 个节点。

## 6. Area

特征：世界区域持续存在，可能周期 Tick。

MUST：

- 明确形状（圆、扇形、矩形、体积）、半径/尺寸、地面投影和边界可读性。
- Gameplay Area 是权威范围；VFX 只显示，不反向决定命中。
- 支持 Enter、Active、Tick、Exit/Collapse。
- 处理地形高度、坡度、深度交叉和相机远近。
- 循环内容必须可剔除、可降级，并进行持续运行测试。

SHOULD：Root + Boundary + Interior + TickPulse + AmbientParticles + Exit，`5–12` 个节点；复杂体积可使用 Complex 预算。

## 7. Beam / Link

特征：同时依赖起点和终点的持续线段。

MUST：

- 每帧或事件更新两个端点，不通过子节点查找 Gameplay 目标。
- 定义断线、遮挡、端点丢失和长度为零时的行为。
- 主束、噪声、起点和终点效果使用统一长度/方向数据。
- UV Tiling、宽度和噪声不随长度意外拉伸。

SHOULD：Root + BeamBody + BeamNoise + StartCap + EndCap + Sparks，`5–9` 个节点。

## 8. Trail / Motion Streak

特征：依附运动对象形成历史轨迹。

MUST：

- 明确 Local/World Space、最小顶点距离、寿命和宽度曲线。
- 对象池重用、瞬移、传送和停顿必须 Clear。
- Trail 自身不是 Projectile Gameplay 运动。

SHOULD：Root + Trail + 可选 Edge/Sparks，`2–5` 个节点。

## 9. Shield / Barrier

特征：包围体、持续、可响应命中。

MUST：

- 明确宿主 Bounds、缩放策略和深度模式。
- 支持 Spawn、Idle、HitPulse、Break、Stop。
- 多次 HitPulse 不得无上限创建材质或对象。
- 透明排序、交叉和相机内外观察必须验证。

SHOULD：Root + Shell + Fresnel/Pattern + HitPulse + BreakParticles，`4–9` 个节点。

## 10. Spawn / Summon / Transform

特征：复合一次性演出，通常驱动多个子效果。

MUST：

- 使用时间线或事件编排现有子效果，不复制其共享资产。
- 明确 Gameplay 对象何时出现/隐藏；VFX 不自行生成权威角色。
- 中断时所有子效果可停止并归还池。

SHOULD：使用 Composite Controller + Nested Prefab；`8–16` 个节点。

## 11. Environment / Weather

特征：大范围、长时间、相机或区域相关。

MUST：

- 使用独立性能 Profile，不套用单次技能预算。
- 支持相机跟随区域、剔除、LOD、密度降级和长时间运行。
- 不把全局环境系统打包成普通技能 Prefab。
- Runtime Entry 默认仍可使用 Prefab；若效果属于场景级全局系统，MAY 经 waiver 使用 Scene Service 或专用 Runtime Asset，并提供与通用生命周期接口的适配器。
- 多相机和场景切换时状态明确。

Complex 预算起步，必须有专项性能审查。

## 12. Screen / UI VFX

特征：Canvas 或屏幕空间反馈。

MUST：

- 明确 Screen Space Overlay/Camera/World Space Canvas。
- 支持分辨率、宽高比、安全区和 UI Mask。
- 不依赖 3D Preview Camera 的 FOV。
- 不遮挡关键 UI；闪烁和强光需考虑可访问性选项。

SHOULD：Root + Main + Accent + Particles，`3–7` 个节点。

## 13. Composite Effect

Composite 是组合方式，不是新的视觉 Renderer。

- MUST 通过 Nested Prefab 或运行时子效果引用组合已批准 Effect。
- MUST 有单一总控制器和统一 Stop/Reset。
- MUST 计算递归依赖与总预算。
- MUST 防止循环引用。
- 子效果版本变化必须改变 Composite dependency hash。

## 14. Decal / Surface Mark

特征：与世界表面法线对齐的轻量残留印记。

MUST：

- Runtime API 接收落点、表面法线和稳定 surface key；沿法线加入极小 bias，禁止明显悬浮或穿插。
- 每个 surface key 执行 oldest-first 叠加上限；默认第 4 次命中替换最旧实例。
- 生命周期结束、替换和对象池回收都必须清理注册状态。
- `archetypeParameters` 必须含 `size/lifetime/stack_limit`。

SHOULD：Root + SurfaceBody + Edge/Reveal + Residue，`3–5` 个节点，simple 预算。

## 15. WeaponTrail

特征：由真实武器刀根/刀尖端点驱动的持续历史带；不同于独立 one-shot Slash。

MUST：

- 每帧由外部传入 blade root、blade tip 和 delta time，不从 Gameplay 层查找骨骼。
- `speed_threshold` 以下在 `fade_time` 内淡出；再次快挥可恢复。
- 历史采样点严格限制 `8–16`，对象池重用、瞬移和 Stop 时清空。
- `archetypeParameters` 必须含 `speed_threshold/history_points/fade_time`。

SHOULD：Root + EndpointLine + Trail + Sparks，`2–5` 个节点，simple 预算。

## 16. Destruction

特征：表现层确定性碎块与尘土，不拥有 Gameplay 物理。

MUST：

- 初速、重力曲线和最多两次视觉反弹由 seed 与索引确定；不得依赖 `Rigidbody/Physics` 获得核心结果。
- 可接收外部 impulse，但相同 seed/输入必须产生相同轨迹。
- `archetypeParameters` 必须含 `piece_count/explode_force/debris_lifetime`；piece count 限 8–12。

SHOULD：Root + 8–12 Fragments + Dust，`10–14` 个节点，complex 预算。

## 17. LifeCycle / Death-Rebirth

特征：作用于外部角色 Renderer 的消亡或聚合演出；不同于场地门户 Spawn。

MUST：

- 外部显式绑定 Renderer 集合，使用 MPB `_Dissolve/_DissolveEdgeColor`，禁止实例化角色材质。
- Stop/Reset 不销毁、不禁用 Gameplay Renderer；只恢复 MPB 生命周期值。
- `direction` 仅允许 `up/down/radial`，并与 inverse entrance 共用一套协议。
- `archetypeParameters` 必须含 `duration/direction/edge_color`。

SHOULD：Root + BodyOverlay + Edge + Ash/Assemble Particles，`3–7` 个节点，complex 预算。

## 18. Portal / Paired Teleport

特征：同一 Recipe 的 entry/exit 两实例，以 `pair_id` 配对。

MUST：

- 外部配置 `pair_id` 与 role；Runtime Entry 不自行搜索或传送 Gameplay 对象。
- 穿越事件统一为入口吸入、隐没、出口吐出三段；缺失配对时安全保持视觉或停止。
- `archetypeParameters` 必须含 `pair_id/portal_radius/swirl_speed`。

SHOULD：Root + Ring + Interior + Flow + Burst，`4–8` 个节点，complex 预算。

## 19. Loot / Pickup

特征：世界空间掉落标识与飞向外部拾取端点的收束过程；不同于 Screen/UI 奖励飞行。

MUST：

- 稀有度固定 `1–5`，驱动颜色、强度和规模；禁止每档复制材质。
- 拾取目标点由外部注入；到达端点后立即 Stop 并可安全复用。
- `archetypeParameters` 必须含 `rarity/pickup_speed/beam_height`。

SHOULD：Root + BaseRing + Beam + Sparkle + PickupArc，`4–7` 个节点，simple 预算。
