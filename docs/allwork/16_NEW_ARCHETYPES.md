# W15 新增 Archetype：贴花 / 武器拖尾 / 破坏 / 死亡重生 / 传送 / 掉落（10 个特效）

> 实施状态（2026-08-25）：6 类规则、10 个 strict Runtime Entry、运行协议、语义 Patch 与批次 Preview 的源码及机器门禁已完成；用户已拒绝当前视觉候选。拒绝点为 Decal 缺三表面贴附、WeaponTrail 缺快慢挥差异、Destruction 缺完整破碎、LifeCycle 未绑定角色溶解、Portal 缺出入口时序差异、Loot 五档主要只换颜色；当前未授权重做。见 `docs/stage-notes/W15_NEW_ARCHETYPES_REPORT.md`。

> 目标：在既有 14 类之外**新增 6 个 Archetype**，每个先在 `docs/rules/10_ARCHETYPE_PROFILES.md` 登记差异规则（MUST/SHOULD/建议 Hierarchy），再产首批代表特效。这是类型维度的真正扩容，不是既有类的填充。
> 批次预览场景：`VFXPREVIEW_NewArchetypes.unity`。

## 1. 新 Archetype 定义（先登记规则，后做内容）

| Archetype | 特征 | 关键协议点 |
|---|---|---|
| **Decal 贴花** | 世界表面残留印记（弹痕/焦痕/血迹/冰纹） | 表面投影对齐法线；寿命-淡出策略；同点叠加上限与替换策略；不穿插表面 |
| **WeaponTrail 武器拖尾** | 跟随武器骨骼/挂点的持续刀光带 | 双端点（刀根/刀尖）外部驱动；速度阈值触发显隐；历史采样平滑；与 Slash 区分（Slash 是独立一次性演出，Trail 跟随真实挥动） |
| **Destruction 破坏** | 物体碎裂/爆散的视觉层 | 接收外部碎块 Mesh 或用程序碎片；碎块物理为表现层假物理（确定性曲线，非 Physics 引擎依赖）；尘土衔接 |
| **LifeCycle 死亡/重生** | 角色消亡与登场的全身演出 | 作用于外部 Renderer 集合（MPB 溶解通道，复用 W12 材质注入协议）；与 Spawn 区分（Spawn 是场地门户，LifeCycle 是身体本身） |
| **Portal 传送** | 成对出入口 + 穿越瞬间 | 双端配对协议（同一 Recipe 两实例 role=entry/exit）；穿越事件三段：入吸-隐没-出吐 |
| **Loot 掉落/拾取** | 掉落物光柱、稀有度标识、拾取飞收 | 稀有度分级参数（1–5 级色阶与规模表内置）；拾取端点外部驱动；世界空间（区别于 W16 的 UI 层奖励飞行） |

## 2. 首批特效清单

| id | Archetype | 维度 | 一句话 |
|---|---|---|---|
| scorch_decal_3d | Decal | 3D | 焦痕贴花：落点烧灼环+余烬明灭+冷却淡出 |
| frost_decal_3d | Decal | 3D | 冰纹贴花：命中点冰晶蔓延生长再消融 |
| katana_trail_weapon_3d | WeaponTrail | 3D | 刀光拖尾：速度触发、快挥亮慢挥隐 |
| energy_whip_trail_2d | WeaponTrail | 2D | 2D 武器光鞭拖尾（骨骼双端点驱动） |
| crate_break_destruction_3d | Destruction | 3D | 木箱爆散：8–12 块假物理碎片+尘土 |
| crystal_shatter_destruction_3d | Destruction | 3D | 水晶碎裂：碎片折光+悬浮微尘缓落 |
| death_dissolve_lifecycle_3d | LifeCycle | 3D | 死亡溶解：燃边自下而上+灰烬剥离上飘 |
| hero_entrance_lifecycle_3d | LifeCycle | 3D | 登场：落地冲击+身体由光聚合成形 |
| twin_portal_3d | Portal | 3D | 成对漩涡门：入口吸入丝流、出口吐出波 |
| loot_beam_pickup_3d | Loot | 3D | 掉落光柱五档稀有度+拾取飞收弧线 |

## 3. 规格卡要点（开发时按 00 号规格卡字段展开）

- **scorch/frost_decal**：Decal 用面片投影（不引 URP DecalProjector 依赖，保持 Unlit 面片+法线对齐）；同点第 4 次命中替换最旧；frost 版蔓延用 Reveal 生长。参数：size、lifetime、stack_limit。预算：粒子 ≤16 / Renderer ≤3（残留类必须极轻）。
- **katana_trail**：历史采样 8–16 点 Catmull-Rom 平滑；`speed_threshold` 以下 0.15s 内淡出；风格变体直接吃 W1 style 块（neon/inkwash 拖尾即两份变体 Recipe）。预算：Renderer ≤3 / 粒子 ≤16（沿途火星可选）。
- **crate_break/crystal_shatter**：碎片假物理=初速+确定性重力曲线+2 次地面反弹衰减；crystal 版碎片带 Fresnel 折光与更慢的微尘。参数：piece_count、explode_force、debris_lifetime。预算：粒子 ≤56 / PS ≤3 / Renderer ≤7。
- **death_dissolve/hero_entrance**：MPB 溶解阈值扫描（`VfxDissolveEdge` 复用），燃边色可换元素系；entrance 是 dissolve 的逆过程+落地 Impact 引用（复用 quake_stomp 弱化变体）。参数：duration、edge_color、direction（上/下/径向）。
- **twin_portal**：两实例经 `pair_id` 关联（Recipe 参数，Runtime 只读）；穿越事件由外部触发，入口端丝流吸入 0.2s、出口端 0.15s 后吐出波。参数：portal_radius、swirl_speed、palette。
- **loot_beam**：稀有度表内置 Recipe（1 白 2 绿 3 蓝 4 紫 5 金橙）：光柱高度/粗细/环数/星闪率随档位；拾取时柱体收束为弧线飞向目标端点。参数：rarity 1–5、pickup_speed。

## 4. 批次验收
通用 DoD + 附加：6 个新 Archetype 的规则条目先于内容合入 `10_ARCHETYPE_PROFILES.md` 并复核不与既有类重叠；Decal 三表面角度（地面/墙面/45°）无穿插证据；WeaponTrail 用预览场景的自动挥舞驱动器验收（驱动器不进正式 Prefab）；loot_beam 五档同屏对比截图。

### 4.1 用户视觉拒绝记录（2026-08-25）

用户签署：**拒绝**。

> 拒绝；W15仅有六类Archetype的概念轮廓，Decal缺少三表面贴附，WeaponTrail缺少快慢挥差异，Destruction缺少完整破碎表现，LifeCycle未绑定角色溶解，Portal缺少出入口时序差异，Loot五档主要只换颜色；设计与实现不同步，整体未达到商用级视觉完成度。

当前候选记为 `rejected`。机器门禁只证明合同、运行协议和生命周期检查通过，不证明上述视觉设计已落地。“未达到商用级”仅指视觉制作完成度，不作许可或法律解释。本次签署未授权重做、批次移交、Unity 源码/资产修改或下一候选生成；详细时间阶段与技术核对见 `docs/stage-notes/W15_NEW_ARCHETYPES_REPORT.md` §5。
