# 原型目录 v1（Prototype Catalog v1）

状态：`DRAFT`（T2a 产出，2026-09-05，待主 agent 验收、用户审"分类与数量"）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）
配套文档：`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`RECIPE_V2_SCHEMA_DRAFT.md`、`T2A_REPORT.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

T2a 完成后三轴覆盖到：

| 轴 | 本文档覆盖 | 说明 |
|---|---|---|
| **原型轴（内容·结构）** | **58 个原型**，覆盖 ADR-010 §3 全部七大类（六个可枚举大类 + 编排级组合规则） | 由"画面职责"系统性穷举得出，不由业务需求圈定；每原型给出层结构 / 节拍 / 2D-3D 投影 / 接口 / 六档降级 / 元素敏感度 |
| **元素轴（内容·物理）** | 本文档只声明每个原型"哪些层对元素敏感"；元素本体 13 个 + 无元素/纯物理类见 `ELEMENT_CATALOG_v1.md` | 原型 × 元素解耦：元素预设通过"层语义槽"自动注入 |
| **风格轴** | 本文档不含任何风格内容；首批卡通 / 像素见 `STYLE_CATALOG_v1.md` | 风格作为材质子图 / 参数集叠加，不进原型结构 |
| **维度轴** | **每个原型**都给出 2D / 3D 投影差异 | 同一原型两种投影，不是两套原型 |

**用户只审本文档 §3 的分类总览表与数量**；每原型细节放权给 T2b / T3。

---

## 1. 穷举方法（如何保证覆盖面）

### 1.1 按"画面职责"而不是按"外观"分类

外观由元素与风格轴决定，若按外观枚举（"火焰类 / 冰霜类 / 闪电类…"）会把三轴重新耦合，得到的是 N×M 张列表而不是 N 个正交单元。原型只回答一个问题：**这个特效在画面里承担什么结构性职责**——它占据什么空间、按什么时间形态存在、附着于什么主体。因此穷举沿三个结构维度展开，然后取"在 Unity 里层结构 / 节拍 / 接口三者至少一项有实质差异"的交叉格为一个原型；仅参数不同的格合并为同一原型的参数。

**结构维度 A：空间形态（effect occupies what）**

| 代号 | 形态 | 典型层需求 |
|---|---|---|
| S-P | 点（point）：一个局部核心 | core / flash / light |
| S-L | 线（line）：两端有定义的连接或轨迹 | link / beam_column / trail |
| S-A | 面（area）：贴附于地面 / 墙面 / 屏幕平面的覆盖 | ground / decal / surface |
| S-V | 体（volume）：占据一片空间的雾、云、场 | body / fog |
| S-S | 壳（shell）：包裹一个主体的封闭或半封闭表面 | mesh_shell / edge |
| S-M | 多体（multi-body）：多个离散子体的集合 | emission / debris / orbit |

**结构维度 B：时间形态（effect exists how）**

| 代号 | 形态 | 节拍骨架 |
|---|---|---|
| T-I | 瞬发（instant）：单次爆发，< 0.5 s | launch→impact→end |
| T-T | 行进（travelling）：随一个运动主体移动，有明确起点终点 | launch→travel→impact→end |
| T-S | 持续（sustained）：循环存在直到外部结束 | launch→sustain(loop)→end |
| T-Q | 序列（sequenced）：多个内部阶段按顺序推进 | launch→stage_k…→end |
| T-D | 衰减（decaying）：出现后单调衰减直到消失 | impact→end(long) |

**结构维度 C：附着主体（effect attaches to what）**

| 代号 | 主体 |
|---|---|
| H-W | 世界坐标固定点 |
| H-B | 角色 / 物体本体（跟随变换，可能贴附网格） |
| H-K | 挂点（武器、手、头顶等子变换） |
| H-2 | 两个主体之间 |
| H-U | UGUI 画布内的 RectTransform |

三维交叉共 6×5×5 = 150 格；剔除在 Unity 中不可表达（如 UGUI 内的体积雾）、与其他格仅参数差异、或属于资产外事项（见 §2）的格之后，剩余 **58 个原型**，再按 ADR-010 §3 的七大类归档。每个原型节首行标注其结构坐标（如 `S-P · T-T · H-W`），用于审计"是否有两个原型坐标与层结构都相同"（若有即应合并）。

### 1.2 层的定义与技术族选择规则

- **层（layer）** 是原型内一个独立的视觉职责单元，编译后对应预制体中一个子节点（含一个渲染器 / 粒子系统 / 光源）。层名使用 §2.1 的共享层角色词表，保证元素预设可以按"层语义槽"注入。
- 每层列出**可选技术族**（只能来自 ADR-010 §4 的 5 族：程序化材质 `material` / GPU 粒子 `gpu_particles` / CPU 粒子 `cpu_particles` / 网格几何 `mesh` / 局部光 `local_light`），标注 ★ 为首选；多个可选项表示同一职责允许不同族实现（六档降级正是沿这个可选集下滑）。
- **局部光是一等成员**（ADR-010 §5）：凡是有"发光"语义的原型，`light` 层为必需层，而不是装饰。低端档位的策略是"把光烘进材质发光"而不是删掉这个职责。
- **必需 / 可选**：必需层缺失则原型不成立（编译器拒绝）；可选层缺失仍是同一原型。

### 1.3 合并规则（避免膨胀）

以下差异**不构成新原型**，全部作为参数或元素 / 风格轴表达：

- 命中规则（单击 / 穿透 / 分裂 / 反弹 / 弹跳）→ 原型特有参数 `hitRule`
- 发射规则（单发 / 扇形 / 齐射 / 环形）→ `emissionPattern` + `count`
- 尺寸、时长、速度、颜色 → 标准参数
- 形态"像什么"（火 / 冰 / 雷…）→ 元素轴
- 渲染取向（描边 / 量化 / 写实…）→ 风格轴
- 正 / 负面语义（增益 vs 减益）→ 同一原型，色板 + 运动方向参数（如 `flowDirection: up|down`）

---

## 2. 明确排除项与共享词表

### 2.0 明确排除项（不是原型，不是层）

| 排除项 | 理由 | 用户侧替代 |
|---|---|---|
| 后处理（Bloom / 色差 / 径向模糊 / Vignette / 全屏扭曲 Volume） | 资产外（ADR-010 §4） | 预制体开放 `intensity` 与生命周期事件，用户在自己的 Volume 上接 |
| 相机（震屏 / FOV 推拉 / 慢动作 / 相机切换） | 资产外 | 订阅 `impact` 等事件自行驱动 |
| UI 布局 / 面板动画（RectTransform 位移、缩放、Canvas Group 透明度） | 资产外；UI 内原型只提供**材质与粒子**，不改 UI 本身 | 用户自己的 UI 动画系统 |
| 场景全局光（Directional Light、环境光、Skybox、Light Probe、Reflection Probe） | 资产外；局部光仅指预制体子节点上的 Point / Spot / Light2D | — |
| 时间缩放（`Time.timeScale`、Timeline 全局暂停） | 资产外 | 预制体开放 `speed` 参数 |
| 屏幕空间"全屏过渡"（屏幕擦除、黑场、镜头脏斑） | 属后处理 / UI 布局；世界空间的过渡幕（E07）是原型，屏幕空间的不是 | — |
| **序列帧 / flipbook / sprite sheet / 逐帧动画贴图** | 素材纪律禁止（ADR-010 §5），形态一律程序化 | 材质族的噪声 / SDF / 阈值 / 顶点位移 |
| 外部烘焙体积数据（VDB、烟雾模拟缓存、Alembic）、外部特效工具产物 | 只依赖 Unity（ADR-010 §1） | 材质族体积噪声 + GPU 粒子 |
| 音效 | 资产外 | 订阅事件 |
| 伤害数字 / 文字弹出 | 属 UI 布局；HUD 脉冲（F06）只做材质与粒子 | — |
| 折射 / 屏幕扭曲层 | 在 URP 内属材质族（Scene Color 节点），**但依赖用户 URP 资产开启 Opaque Texture，违反"对外部零假设"**。v1 不作为任何原型的层；列为未决问题（见 `T2A_REPORT.md` §6） | 若用户自行开启则可由 T2b 以"可选扩展层"评估 |

### 2.1 共享层角色词表（layer role vocabulary）

元素预设按这些角色名注入（见 `ELEMENT_CATALOG_v1.md` §2），因此原型表里的层名**必须**取自本表（可加后缀区分同角色多层，如 `emission.sparks` / `emission.smoke`）。

| 角色 | 语义 | 默认可选族 |
|---|---|---|
| `core` | 核心体：主体形态最亮 / 最实的部分 | material · mesh |
| `body` | 体积 / 包裹：核心外围的体积感、气雾、能量场 | material · gpu_particles · cpu_particles |
| `edge` | 边缘 / 轮廓：菲涅尔、锐边、外发光带 | material |
| `trail` | 尾迹：跟随运动主体的拖尾 | mesh(Trail 网格) · material · gpu_particles · cpu_particles |
| `emission` | 发射体 / 碎屑：离散小粒子（火星、水珠、碎片、雨滴、萤火） | gpu_particles · cpu_particles |
| `ground` | 地面接触：贴地环、阵、投影圈 | material(quad/环 UV) · mesh |
| `decal` | 表面残迹：燃痕、湿迹、裂纹、冻痕（有寿命衰减） | material(投影 quad / URP Decal Projector) |
| `flash` | 瞬时闪光：一帧到数帧的高亮 | material · local_light |
| `shock` | 波前 / 冲击环：向外扩张的环或面 | material · mesh |
| `beam_column` | 光束柱体：两端受控的拉伸柱 | mesh(圆柱 / 带状) + material |
| `link` | 连接线：两个动点之间的线 / 弧 / 链 | mesh(LineRenderer / 程序化带) + material |
| `mesh_shell` | 几何壳 / 笼：封闭或半封闭的包裹面 | mesh + material |
| `debris` | 破碎块：刚体或脚本驱动的块状碎片 | mesh(预破碎 + Rigidbody) · gpu_particles(网格粒子) · cpu_particles(网格渲染) |
| `surface` | 持续表面场：水面 / 熔岩面 / 沼面等大面积可动表面 | mesh(细分平面) + material(顶点位移) |
| `light` | 局部光：Point / Spot（3D）或 Light2D（2D），随节拍变化 | local_light |
| `orbit` | 环绕体：绕主体运动的离散物 | gpu_particles(轨道场) · cpu_particles · mesh |
| `column` | 竖直光柱 / 束：从地面向上的柱体 | mesh(圆柱 / 双 quad) + material |
| `veil` | 幕 / 墙：大面积竖直面 | mesh(平面) + material |
| `cloth` | 布料 | mesh(Cloth 组件 / 顶点位移) |
| `fill` | UI 填充：矩形 / 条形区域内的材质效果 | material(UGUI Graphic 材质) |
| `frame` | UI 边框 / 轮廓流光 | material(UGUI Graphic 材质 SDF 边) |

### 2.2 标准节拍与标准事件

所有原型的节拍用四个标准阶段描述，原型自行映射（没有的阶段标"—"）：

| 阶段 | 事件 id | 含义 |
|---|---|---|
| 起手（Launch） | `launch` | 出现 / 预示 / 蓄势 |
| 持续（Travel / Sustain） | `travel` 或 `sustain` | 行进类用 `travel`，持续类用 `sustain`（循环，直到外部 `end` 或超时） |
| 峭点（Impact） | `impact` | 命中 / 爆发 / 破裂 |
| 收尾（End） | `end` | 消散、残留衰减 |
| 完全结束 | `complete` | 所有层清空，可回池（只发不收） |

事件分**入事件**（外部调用触发阶段，如 `impact`）与**出事件**（阶段进入 / 完成时对外广播，如 `onImpact` / `onComplete`）。运行时组件同时提供两者。原型特有事件（如护盾的 `hitAt(point)` / `break`）在各节列出。

### 2.3 标准参数（全部原型共有）

| 参数 | 类型 | 范围 | 语义 |
|---|---|---|---|
| `palette` | Gradient（≥3 色，HDR） | — | 覆盖元素默认色板 |
| `intensity` | float | 0.0 ~ 2.0 | 发光 / 发射率 / 光强的统一倍率 |
| `scale` | float | 0.25 ~ 4.0 | 空间尺度倍率 |
| `speed` | float | 0.25 ~ 3.0 | 节拍与材质流动速度倍率 |
| `seed` | uint | — | 噪声 / 随机相位种子 |

### 2.4 六档基准预算（tier budget baseline）

档位代号：手机低 `ML`、手机中 `MM`、手机高 `MH`、PC 低 `PL`、PC 中 `PM`、PC 高 `PH`。每原型的降级表只写各层策略，数值上限由本表约束（T2b 成本模型细化）。

| 预算项 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| GPU 粒子（VFX Graph） | 禁用 | 禁用 | ≤ 2 k | ≤ 5 k | ≤ 20 k | ≤ 100 k |
| CPU 粒子（ParticleSystem）峰值 | ≤ 60 | ≤ 150 | ≤ 300 | ≤ 400 | ≤ 600 | ≤ 1000 |
| 材质噪声采样层数 / 张 | 1（静态或单层滚动） | 2 | 2 + 细节 | 2 | 3 | 3 + 顶点位移 |
| 局部光数 / 预制体 | 0（烘进材质发光） | 1，无阴影 | 2，无阴影 | 2，无阴影 | 3，≤ 1 盏阴影 | 4，≤ 2 盏阴影 |
| 破碎块（Rigidbody） | ≤ 8（无刚体，脚本 / 材质位移） | ≤ 16（简化碰撞） | ≤ 32 | ≤ 48 | ≤ 96 | ≤ 200 |
| Cloth | 禁用（顶点位移代替） | 禁用 | 低分辨率 | 低 | 中 | 高 |
| 透明叠加层上限（同像素 overdraw） | 2 | 3 | 4 | 4 | 6 | 8 |
| 程序化网格顶点 / 层 | ≤ 256 | ≤ 512 | ≤ 2 k | ≤ 2 k | ≤ 8 k | ≤ 32 k |

降级表单元格记法：`关` 关闭该层；`C:n` CPU 粒子上限 n；`G:n` GPU 粒子上限 n；`材1/2/3` 材质噪声层数；`材:静` 材质无噪声动画（仅渐变 / 阈值）；`几:低/中/高` 几何细分档；`光:烘` 局部光关闭并把发光烘进材质；`光:1/2/阴` 光数 / 带阴影；`块:n` 破碎块数；`布:关/低/中/高`；`同上` 与左格相同。

### 2.5 2D / 3D 投影通则

每原型节的"投影差异"只写该原型的特殊点；以下通则默认适用：

| 职责 | 3D 表达 | 2D 表达 |
|---|---|---|
| 体积 / 气雾 | 体积噪声（视线方向多次采样或球壳法线噪声） | quad 上的平面 UV 流动噪声 + 极坐标 UV |
| 几何形体 | 真实网格（球 / 环 / 锥 / 圆柱 / 程序化） | quad / Sprite 网格 + 材质 SDF 形状；环用极坐标 UV 而非环面网格 |
| 粒子空间 | 三维发射体、深度排序 | 发射体压平到 XY，Z 仅用于层内排序；Sorting Layer + Order in Layer 显式声明 |
| 局部光 | Point / Spot Light（URP） | Light2D（Point / Freeform / Sprite Light），需 2D Renderer |
| 排序 | 透明队列 + 深度 | 每层显式 `sortingOrder` 偏移 |
| 阴影 | 网格可投影 | 无阴影；必要时"假阴影" quad |
| 破碎 | Rigidbody + Collider（Box / Convex） | Rigidbody2D + PolygonCollider2D，碎片为 Sprite 网格切片 |
| 布料 | Cloth 组件 | Cloth 不可用 → 细分 quad 顶点位移波 |
| 贴地 | 贴合地面法线的 quad / URP Decal Projector | 平面 quad，Sorting Layer 置于角色之下 |
| 挂点 | 骨骼 / 子变换 | 子变换（Sprite 角色也有骨骼时同 3D） |
| UGUI 内 | 同 2D（Canvas 空间） | 同 |

---

## 3. 分类总览表（用户审阅面）

**总计 58 个原型 + 编排级组合规则 1 节**。ADR-010 §3 预计 40~60，本表 58。

| 大类 | 数量 | 原型（id） |
|---|---|---|
| **A 战斗动作** | **13** | A01 直线投射物 `projectile_linear` · A02 抛物投射物 `projectile_ballistic` · A03 追踪投射物 `projectile_homing` · A04 光束 `beam` · A05 近战挥击 `melee_sweep` · A06 命中爆发 `impact_burst` · A07 地面范围 `area_ground` · A08 坠落打击 `strike_descending` · A09 扩散环 `wave_ring` · A10 连锁 `chain_link` · A11 位移 `displacement` · A12 牵引链接 `tether_link` · A13 召唤显现 `summon_manifest` |
| **B 状态持续** | **10** | B01 地面光环 `aura_ground` · B02 体表光环 `aura_body` · B03 环绕体 `orbitals` · B04 护盾 `shield` · B05 蓄力汇聚 `charge_gather` · B06 引导施法 `channel_sustain` · B07 挂点标记 `attach_marker` · B08 武器附魔 `weapon_enchant` · B09 侵蚀状态 `afflict_erode` · B10 束缚禁锢 `restrain_bind` |
| **C 反馈事件** | **9** | C01 受击反应 `hit_reaction` · C02 暴击强调 `critical_emphasis` · C03 治疗恢复 `heal_restore` · C04 强化获得 `empower_rise` · C05 拾取吸收 `pickup_absorb` · C06 消散退场 `dissolve_out` · C07 登场显现 `entrance_in` · C08 格挡弹反 `parry_deflect` · C09 形态切换 `transform_shift` |
| **D 物理破坏** | **8** | D01 刚体破碎 `fracture_burst` · D02 结构崩塌 `collapse_sequence` · D03 布料动态 `cloth_dynamic` · D04 液体飞溅 `liquid_splash` · D05 液体流 `liquid_stream` · D06 表面残迹 `surface_decal` · D07 弹性形变 `soft_deform` · D08 地面裂陷 `ground_rupture` |
| **E 环境氛围** | **9** | E01 降落天气粒子 `weather_precip` · E02 空气粒子流 `ambient_airflow` · E03 雾烟云体 `fog_volume` · E04 持续表面场 `surface_field` · E05 喷发源 `vent_emitter` · E06 传送门 `portal_gate` · E07 世界过渡幕 `sweep_curtain` · E08 屏障墙 `barrier_wall` · E09 光源体 `light_body` |
| **F UI 内特效** | **9** | F01 按钮反馈 `ui_button_feedback` · F02 持续流光 `ui_shine_loop` · F03 奖励爆发 `ui_reward_burst` · F04 揭示 `ui_reveal` · F05 合成融合 `ui_merge_fuse` · F06 HUD 脉冲 `ui_hud_pulse` · F07 能量条 `ui_bar_energy` · F08 收集飞行 `ui_collect_fly` · F09 面板入场 `ui_panel_enter` |
| **G 编排级** | 规则 1 节 | `orchestration`：多原型时间线组合规则（§10），不逐个枚举组合 |
| **合计** | **58** | — |

ADR-010 §3 骨架逐项对照：投射物（A01–A03）、光束（A04）、斩击（A05）、冲击（A06）、范围（A07/A08）、召唤（A13）、位移轨迹（A11）、连锁（A10）｜光环（B01/B02）、Debuff（B09/B10）、护盾（B04）、蓄力（B05）、挂点（B07/B08）｜受击（C01）、暴击（C02）、治疗（C03）、获得（C04/C05）、消散（C06）、登场（C07）｜破碎（D01）、崩塌（D02）、残骸（D01 收尾层 + D06）、布料（D03）、液体飞溅（D04/D05）｜天气粒子（E01）、雾烟云（E03）、水面熔岩（E04）、传送门（E06）、过渡（E07）｜按钮（F01）、奖励（F03）、抽卡（F04）、合成（F05）、HUD 提示（F06）｜编排级（§10）。骨架未点名但由穷举补出的：A09 扩散环、A12 牵引链接、B03 环绕体、B06 引导施法、C08 格挡弹反、C09 形态切换、D07 弹性形变、D08 地面裂陷、E02 空气粒子流、E05 喷发源、E08 屏障墙、E09 光源体、F02/F07/F08/F09。

---

## 4. A 战斗动作（Combat Actions）

### A01 直线投射物 `projectile_linear`

坐标：`S-P · T-T · H-W`

**职责**：一个自带发光核心的运动主体沿近似直线从发射点行进到命中点，并在命中处交接给爆发。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 主体形态：程序化噪声 / SDF 决定的亮核 | ★material（球壳 / quad 上 SDF + 噪声）· mesh（低面球 + 顶点位移） | 必需 |
| `body` | 核心外围的能量体积或气雾包裹 | ★material（体积噪声）· gpu_particles（贴核心的稠密粒子）· cpu_particles | 可选 |
| `trail` | 尾迹，记录行进路径 | ★mesh（Trail 网格 + 流动材质）· gpu_particles（尾流带）· cpu_particles | 必需 |
| `emission.sparks` | 沿途脱落的碎屑 / 火星 / 液滴 | ★gpu_particles · cpu_particles | 可选 |
| `flash.launch` | 发射瞬间的口部闪光 | ★material（径向 SDF 闪）· local_light | 可选 |
| `light` | 随主体运动的局部光 | ★local_light | 必需 |

**节拍**

| 阶段 | 事件 | 时长量级 | 说明 |
|---|---|---|---|
| 起手 | `launch` | 0.05 ~ 0.2 s | `flash.launch` 一次性；`core` 从 0 缩放到 1 |
| 飞行 | `travel` | 0.3 ~ 3 s 或外部驱动位置 | `trail` 持续记录；`light` 随体 |
| 命中 | `impact` | 交接 | 停止 `core/body/emission`，`trail` 停止记录；`impact` 本身由 A06 承担（作为编排子原型或本原型的内嵌子层，见 §10） |
| 收尾 | `end` → `complete` | 0.2 ~ 0.6 s | `trail` 自然衰减，`light` 淡出 |

**2D/3D 投影差异**

| 层 | 3D | 2D |
|---|---|---|
| `core` | 低面球 + 视线方向体积噪声 / 菲涅尔核 | 单 quad，极坐标 SDF 圆核 + 平面噪声；朝向沿运动方向旋转 |
| `body` | 球壳法线噪声 + 深度淡出 | 第二张 quad 叠加，比 core 大 20~60 %，排序在 core 之下 |
| `trail` | Trail 网格，宽度曲线，材质沿 U 流动 | 同 Trail 网格（2D 也是网格），Sorting Layer 置于 core 之下 |
| `light` | Point Light，range 与 `scale` 联动 | Light2D Point，falloff 与 `scale` 联动 |

**对外接口（特有参数）**

| 参数 | 类型 | 范围 | 语义 |
|---|---|---|---|
| `coreRadius` | float | 0.05 ~ 2.0 (m) | 核心半径 |
| `trailLength` | float | 0 ~ 10 (m) 或 0 ~ 2 (s) | 尾迹长度 |
| `trailWidth` | float | 0.01 ~ 1.0 | 尾迹基础宽度 |
| `emissionPattern` | enum | `single/fan/ring/volley` | 发射规则（多子体由编排层实例化，本原型只描述单体） |
| `hitRule` | enum | `single/pierce/split/bounce` | 命中规则，影响 `impact` 是否结束 travel |
| `spin` | float | 0 ~ 20 (rad/s) | 核心自旋 |

事件：标准 `launch/travel/impact/end/complete` + 位置驱动方法 `setTravelPose(position, rotation)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 材1 · 几:低 | 材2 | 材2 | 材2 | 材3 | 材3 + 顶点位移 |
| `body` | 关 | 材1 | 材2 | 材2 | G:500 或 材3 | G:2000 |
| `trail` | 网格 · 材:静 | 网格 · 材1 | 网格 · 材2 | 同上 | 网格 · 材2 + G:300 | 网格 · 材3 + G:1000 |
| `emission.sparks` | 关 | C:20 | G:200 | G:400 | G:1500 | G:5000 |
| `flash.launch` | 材:静 | 材1 | 材1 + 光 | 同上 | 同上 | 同上 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`core` 形态（锐利 / 圆润 / 分叉）、`trail` 形态（连续带 / 断裂片段 / 滴落）、`emission.sparks` 运动场（上升 / 下坠 / 湍流）高度随元素变化；`light` 色温与闪烁模式随元素；`flash.launch` 仅色板随元素。

---

### A02 抛物投射物 `projectile_ballistic`

坐标：`S-P · T-T · H-W`（与 A01 区别：轨迹有重力弧线与落点预示层，层结构多一层 `ground.landing`）

**职责**：受重力的弧线运动主体，飞行中提供落点预示，落地后交接给爆发或范围。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 主体，通常更实（块 / 团） | ★mesh（低面几何 + 顶点位移）· material | 必需 |
| `body` | 包裹体 | ★material · cpu_particles | 可选 |
| `trail` | 弧线尾迹，比 A01 更松散、受重力下垂 | ★gpu_particles（重力场）· cpu_particles · mesh | 必需 |
| `emission.drips` | 沿弧线脱落并下坠的碎屑 | ★cpu_particles · gpu_particles | 可选 |
| `ground.landing` | 落点预示圈（收缩 / 闪烁） | ★material（极坐标 SDF 环） | 可选 |
| `light` | 随体局部光 | ★local_light | 必需 |

**节拍**：起手 `launch` 0.05~0.2 s；飞行 `travel` 0.5~2.5 s（弧线由外部或内建 `apexHeight/flightTime` 驱动，`ground.landing` 在 travel 开始 30 % 时出现并随剩余时间收缩）；命中 `impact` 交接；收尾 `end` 0.2~0.8 s。

**2D/3D 投影差异**：3D `core` 用真实几何投影阴影（PM+），`ground.landing` 贴合地面法线；2D 弧线在 XY 平面，`ground.landing` 是落点处的椭圆 quad（Sorting Layer 在角色下），`core` 的"高度"用 Y 位移 + 缩放暗示。

**对外接口（特有参数）**：`apexHeight` float 0.5~10 m；`flightTime` float 0.3~3 s；`landingPreview` bool；`landingRadius` float 0.2~3 m；`tumble` float 0~10 rad/s（核心翻滚）；`hitRule` 同 A01。事件同 A01，另出事件 `onApex`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 几:低 · 材:静 | 几:低 · 材1 | 几:中 · 材2 | 同上 | 几:高 · 材2 | 几:高 · 材3 + 顶点位移 |
| `body` | 关 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `trail` | C:15 | C:40 | G:400 | G:800 | G:3000 | G:8000 |
| `emission.drips` | 关 | C:10 | C:40 | G:200 | G:600 | G:2000 |
| `ground.landing` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`core` 基础形（块 / 球 / 团）、`trail` 下垂程度与断裂、`emission.drips` 重力系数与飞溅二次行为随元素显著变化；`ground.landing` 仅色板。

---

### A03 追踪投射物 `projectile_homing`

坐标：`S-P · T-T · H-W→H-B`（终点是动目标）

**职责**：主体持续转向追踪一个运动目标，尾迹呈现转向历史，命中时交接给爆发。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 主体，通常小而亮，带方向性（前端尖 / 后端拖） | ★material（方向性 SDF）· mesh（锥 / 泪滴） | 必需 |
| `trail` | 长而柔的转向尾迹，是本原型的视觉主角 | ★mesh（Trail 网格，宽度曲线）· gpu_particles（尾流带） | 必需 |
| `emission.wisps` | 从尾迹外侧甩出的细碎粒子 | ★gpu_particles · cpu_particles | 可选 |
| `edge` | 核心的边缘光晕，强调速度感 | ★material | 可选 |
| `light` | 随体局部光 | ★local_light | 必需 |

**节拍**：起手 `launch` 0.05~0.3 s（可含 `hoverTime` 悬停）；飞行 `travel` 0.5~4 s（转向率 `turnRate` 限制；目标由 `setTarget(transform)` 或外部 `setTravelPose` 驱动）；命中 `impact` 交接；收尾 `end` 0.3~1 s（尾迹长于 A01 因此收尾更长）。

**2D/3D 投影差异**：3D `core` 网格沿速度方向 LookRotation，`trail` 面向相机（Trail 网格默认）；2D `core` quad 旋转到速度方向，`trail` 网格在 XY 平面；2D 尾迹宽度建议更大以补偿缺少深度。

**对外接口（特有参数）**：`turnRate` float 30~720 °/s；`hoverTime` float 0~1 s；`loseTargetMode` enum `continue/fizzle`；`trailLength` float 0.2~3 s；`wobbleAmplitude` float 0~0.5 m（飞行蛇摆）；`wobbleFrequency` float 0~10 Hz。事件：标准 + 入 `setTarget(Transform)` + 出 `onTargetLost`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 材:静 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `trail` | 网格 · 材:静 · 短 | 网格 · 材1 | 网格 · 材2 | 网格 · 材2 + G:300 | 网格 · 材2 + G:1500 | 网格 · 材3 + G:5000 |
| `emission.wisps` | 关 | C:15 | G:150 | G:300 | G:1000 | G:3000 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`trail` 的连续性（丝带 / 断续 / 锯齿）与 `emission.wisps` 的脱落方式随元素显著变化；`core` 方向性形态中等敏感。

---

### A04 光束 `beam`

坐标：`S-L · T-S 或 T-I · H-K→H-W`（源在挂点或世界点，终点由外部或长度决定）

**职责**：两端受控的拉伸柱体，从源到终点即时或持续存在，终点处持续给出接触反馈。瞬发（射线）与持续（引导束）是同一结构的时长参数差异。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `beam_column.core` | 亮核柱 | ★mesh（圆柱 / 双交叉 quad，程序化拉伸）+ material（沿 U 流动噪声、端部收窄） | 必需 |
| `beam_column.body` | 外围柔化柱（更宽更暗，噪声抖动） | ★mesh + material | 可选 |
| `flash.source` | 源端汇聚闪光 | ★material（径向 SDF）· local_light | 必需 |
| `flash.hit` | 终端接触闪光（持续束时循环脉冲） | ★material · local_light | 必需 |
| `emission.source` | 源端向内汇聚的粒子 | gpu_particles · ★cpu_particles | 可选 |
| `emission.hit` | 终端溅射粒子（沿法线反弹） | ★gpu_particles · cpu_particles | 可选 |
| `light.source` / `light.hit` | 两端局部光 | ★local_light | 必需（至少终端） |

**节拍**

| 阶段 | 事件 | 时长量级 | 说明 |
|---|---|---|---|
| 起手 | `launch` | 0.05 ~ 0.4 s | 源端汇聚；柱体从源向终点"生长"（`growTime`） |
| 持续 | `sustain` | 瞬发 0.05~0.2 s / 持续 直至 `end` | 柱体沿 U 流动，终端脉冲 |
| 峭点 | `impact` | 持续型每次 `hitPulseInterval` | 终端脉冲增强（出事件 `onHitPulse`） |
| 收尾 | `end` | 0.1 ~ 0.5 s | 柱体从源端向终端"抽离"或整体变细消失 |

**2D/3D 投影差异**：3D 柱体用圆柱网格（或两片交叉 quad 面向相机），端部用球形收口；2D 柱体是一条沿长度拉伸的 quad（9-slice 式 UV：两端固定、中段平铺），排序层置于角色上方；3D 局部光在两端各一盏 Point，2D 用 Light2D Freeform 沿柱体或两端 Point。

**对外接口（特有参数）**：`length` float 0.5~50 m（或由 `setEndPoint(position)` 驱动）；`width` float 0.02~2 m；`growTime` float 0~0.4 s；`mode` enum `hitscan/sustained`；`hitPulseInterval` float 0.1~1 s；`noiseAmplitude` float 0~0.5（柱体抖动）；`endpointFollowsSurface` bool（终端法线对齐）。事件：标准 + 入 `setEndPoint(position, normal)` + 出 `onHitPulse`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `beam_column.core` | 双 quad · 材1 | 双 quad · 材2 | 圆柱 · 材2 | 圆柱 · 材2 | 圆柱 · 材3 | 圆柱 · 材3 + 顶点位移 |
| `beam_column.body` | 关 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `flash.source/hit` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `emission.source` | 关 | 关 | C:30 | C:60 | G:500 | G:1500 |
| `emission.hit` | 关 | C:20 | G:200 | G:400 | G:1500 | G:4000 |
| `light.*` | 光:烘 | 光:1（终端） | 光:2 | 光:2 | 光:2 | 光:2 阴 |

**元素敏感度**：`beam_column.*` 柱体形态（平直 / 锯齿分叉 / 螺旋缠绕 / 波动）随元素显著变化；`emission.hit` 溅射行为随元素；`flash.*` 仅色板与闪烁频率。

---

### A05 近战挥击 `melee_sweep`

坐标：`S-A（扫掠面）· T-I · H-K`

**职责**：武器或肢体扫过的轨迹瞬间显形为一个刃锋面，尾随薄尾迹与火星，短促而锐利。弧形斩、直刺、上挑是扫掠路径参数差异。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core.blade` | 刃锋面：沿扫掠路径生成的带状网格，前缘亮后缘淡 | ★mesh（程序化扫掠带，`arcAngle/radius/thickness` 生成）+ material（沿 U 消散阈值） | 必需 |
| `edge` | 刃锋前缘的锐利高光带 | ★material（同一材质的边缘通道） | 可选 |
| `trail` | 刃尖拖出的细尾迹 | ★mesh（Trail 网格）· cpu_particles | 可选 |
| `emission.sparks` | 扫掠中甩出的火星 / 碎屑 | ★cpu_particles · gpu_particles | 可选 |
| `flash.tip` | 挥击最快点的瞬闪 | material · ★local_light | 可选 |
| `light` | 一帧到数帧的局部光脉冲 | ★local_light | 必需 |

**节拍**：起手 `launch` 0~0.08 s（可选预闪）；持续 `travel` 0.08~0.3 s（刃锋面按 `sweepProgress` 从 0 生长到 1，由内建时长或外部驱动）；峭点 `impact`（若命中）触发 `flash.tip`；收尾 `end` 0.1~0.4 s（面从后缘向前缘消散）。

**2D/3D 投影差异**：3D 刃锋面是绕挂点的三维扫掠带（可有厚度与双面），朝向由扫掠平面决定；2D 刃锋面是 XY 平面上的扇形 quad 网格（极坐标 UV），排序层在角色之上，可选"翻转"参数；3D 火星受重力有深度散布，2D 火星限制在平面并夸大尺寸。

**对外接口（特有参数）**：`sweepPath` enum `arc/line/uppercut/spin`；`arcAngle` float 30~360 °；`radius` float 0.3~4 m；`thickness` float 0.02~0.6 m；`sweepDuration` float 0.08~0.4 s；`sweepProgress` float 0~1（可外部驱动以对齐动画）；`edgeSharpness` float 0~1。事件：标准 + 入 `setSweepProgress(t)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core.blade` | 几:低(8 段) · 材:静 | 几:低 · 材1 | 几:中(24 段) · 材2 | 同上 | 几:高 · 材2 | 几:高 · 材3 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `trail` | 关 | 网格 · 材:静 | 网格 · 材1 | 同上 | 同上 | 网格 · 材2 |
| `emission.sparks` | C:8 | C:20 | C:60 | G:300 | G:1000 | G:3000 |
| `flash.tip` | 关 | 材:静 | 材1 + 光 | 同上 | 同上 | 同上 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`core.blade` 消散方式（平滑淡出 / 碎裂成片 / 滴落 / 锯齿闪烁）与 `emission.sparks` 的抛出方式随元素显著变化；`edge` 锐度随元素中等敏感。

---

### A06 命中爆发 `impact_burst`

坐标：`S-P · T-I · H-W`

**职责**：在一个点上把能量瞬间释放：核心闪光、向外扩张的波前、抛出的碎屑、短暂残留。它是所有"命中"的接收端，可独立播放也可被 A01–A03/A08 交接。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `flash` | 峰值闪光，1~3 帧最亮 | ★material（径向 SDF + 噪声扰边）· local_light | 必需 |
| `core` | 爆发主体（膨胀后消散的团） | ★material（体积噪声 + 阈值消散）· mesh（球 + 顶点位移） | 必需 |
| `shock` | 冲击环 / 波前，向外扩张变薄 | ★material（极坐标 SDF 环）· mesh（环面） | 可选 |
| `emission.debris` | 抛出的碎屑（火星 / 碎块 / 液滴），受表面法线约束 | ★gpu_particles · cpu_particles | 必需 |
| `emission.smoke` | 残留烟尘，缓慢上升消散 | ★cpu_particles（少量大粒子）· gpu_particles · material（体积噪声团） | 可选 |
| `decal` | 表面残迹（短寿命；长寿命请用 D06） | material | 可选 |
| `light` | 随 flash 峰值的局部光，指数衰减 | ★local_light | 必需 |

**节拍**：起手 `launch`→ 立即 `impact` 0 s（本原型 launch 与 impact 重合）；峭点 0.05~0.15 s（flash 峰、core 膨胀、shock 起步）；收尾 `end` 0.3~1.5 s（core 阈值消散、debris 落地、smoke 上升淡出、light 衰减）。

**2D/3D 投影差异**：3D `core` 用球 + 视线体积噪声，`shock` 用贴表面法线的环面或朝向法线的 quad，`emission.debris` 沿半球法线抛出并受重力落地；2D `core` 是一张 quad 的极坐标噪声爆发，`shock` 是同心极坐标环 quad，`emission.debris` 在 XY 平面抛出并按 Sorting Layer 分前后两组以造深度错觉。

**对外接口（特有参数）**：`burstRadius` float 0.2~6 m；`debrisCount` int 0~200（档位上限截断）；`shockEnabled` bool；`smokeDuration` float 0~3 s；`surfaceNormal` Vector3（约束抛出半球，默认 up）；`decalLifetime` float 0~5 s。事件：`impact(position, normal)` 入；出 `onPeak`、`onComplete`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `flash` | 材:静 | 材1 | 材1 + 光 | 同上 | 同上 | 同上 |
| `core` | 材1 | 材2 | 材2 | 材2 | 材3 | 材3 + 顶点位移 |
| `shock` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `emission.debris` | C:15 | C:40 | G:300 | G:600 | G:2500 | G:10000 |
| `emission.smoke` | 关 | C:6 | C:12 | C:16 | G:200 | G:600 |
| `decal` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：全部层高度敏感——`core` 膨胀形态（球形 / 尖刺放射 / 液态摊开 / 电弧网），`emission.debris` 的粒子形状与运动场，`emission.smoke` 是否存在，`decal` 类型（烧痕 / 冻痕 / 湿迹 / 裂纹）。

---

### A07 地面范围 `area_ground`

坐标：`S-A · T-Q · H-W`

**职责**：在地面上的一个形状区域内按"预警 → 爆发 → 残留"三段推进的覆盖效果。形状（圆 / 扇 / 矩形 / 环）与阶段时长是参数。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `ground.telegraph` | 预警：区域轮廓 + 填充进度（收缩 / 扫描 / 闪烁） | ★material（SDF 形状 + 进度阈值） | 必需（可通过参数缩短为 0 s） |
| `ground.fill` | 爆发 / 持续期的区域内主体覆盖（噪声流动、阈值翻腾） | ★material（平面噪声 + 阈值）· mesh（细分 quad 顶点位移） | 必需 |
| `column` | 区域内竖起的能量柱 / 光柱（爆发时） | mesh + material | 可选 |
| `emission.rise` | 区域内向上 / 向外的粒子 | ★gpu_particles · cpu_particles | 可选 |
| `shock` | 爆发瞬间的边缘扩散环 | material | 可选 |
| `decal.residue` | 残留期的表面痕迹（衰减） | material | 可选 |
| `light` | 区域中心（或多盏沿边）局部光 | ★local_light | 必需 |

**节拍**

| 阶段 | 事件 | 时长量级 | 说明 |
|---|---|---|---|
| 起手（预警） | `launch` | 0 ~ 2 s（`telegraphDuration`） | `ground.telegraph` 单独可见；出事件 `onTelegraphEnd` |
| 峭点（爆发） | `impact` | 0.1 ~ 0.4 s | `ground.fill` 从中心 / 边缘阈值展开，`column/shock/emission.rise` 触发 |
| 持续 | `sustain` | 0 ~ 10 s（`activeDuration`，0 = 一次性） | `ground.fill` 循环流动，`emission.rise` 持续 |
| 收尾 | `end` | 0.3 ~ 3 s | `ground.fill` 阈值收缩，`decal.residue` 接管后衰减 |

**2D/3D 投影差异**：3D 区域是贴地 quad / URP Decal Projector（可贴合起伏地面），`column` 是真实圆柱；2D 区域是椭圆压扁的 quad（透视暗示），Sorting Layer 置于角色下，`column` 是竖直 quad 排在角色前后两层之间。

**对外接口（特有参数）**：`shape` enum `circle/sector/rect/ring`；`radius` / `innerRadius` float；`sectorAngle` float 10~360 °；`rectSize` Vector2；`telegraphDuration` float 0~2 s；`telegraphStyle` enum `shrink/sweep/blink`；`activeDuration` float 0~10 s；`fillProgress` float 0~1（外部驱动）；`residueLifetime` float 0~10 s。事件：标准 + 出 `onTelegraphEnd`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `ground.telegraph` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `ground.fill` | 材1 | 材2 | 材2 | 材2 | 材3 | 材3 + 顶点位移 |
| `column` | 关 | 双 quad · 材1 | 圆柱 · 材2 | 同上 | 同上 | 圆柱 · 材3 |
| `emission.rise` | C:20 | C:50 | G:500 | G:1000 | G:4000 | G:15000 |
| `shock` | 关 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `decal.residue` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:2 | 光:2 | 光:3 阴 |

**元素敏感度**：`ground.fill` 流动形态（翻腾 / 结晶蔓延 / 液面波纹 / 电网跳跃）、`emission.rise` 运动方向与粒子形态、`column` 形态高度敏感；`ground.telegraph` 仅色板与闪烁节律。

---

### A08 坠落打击 `strike_descending`

坐标：`S-P→S-A · T-T · H-W`

**职责**：主体从高处坠落到地面点，落地前给出落点预示，落地后爆发并向外扩散。与 A02 的差别在于起点在上方、无水平弧线、落地爆发规模更大且属于本原型的一部分。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 坠落主体 | ★mesh（几何 + 顶点位移）· material | 必需 |
| `trail` | 竖直尾迹（拉长、被空气撕开） | ★gpu_particles · mesh · cpu_particles | 必需 |
| `ground.telegraph` | 落点预示圈 | ★material | 可选 |
| `flash` | 落地闪光 | ★material · local_light | 必需 |
| `shock` | 落地扩散环 | ★material · mesh | 必需 |
| `emission.debris` | 落地抛出碎屑（半球向上） | ★gpu_particles · cpu_particles | 必需 |
| `decal` | 落点残迹 | material | 可选 |
| `light` | 随体 + 落地峰值 | ★local_light | 必需 |

**节拍**：起手 `launch` 0~0.5 s（上方出现，`ground.telegraph` 出现）；行进 `travel` 0.2~1 s（`fallHeight` 与 `fallDuration`）；峭点 `impact` 0.05~0.2 s（flash/shock/debris）；收尾 `end` 0.5~2 s。

**2D/3D 投影差异**：3D 主体沿世界 -Y 坠落，`shock` 贴地环面；2D 主体沿屏幕 -Y 坠落，`ground.telegraph` 是椭圆 quad，`shock` 是压扁椭圆环，落地时主体切换 Sorting Layer 到地面层。

**对外接口（特有参数）**：`fallHeight` float 2~30 m；`fallDuration` float 0.2~1.5 s；`impactRadius` float 0.5~8 m；`telegraph` bool；`debrisCount` int；`easing` enum `linear/accelerate`。事件：标准 + 出 `onLand`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 几:低 · 材:静 | 几:低 · 材1 | 几:中 · 材2 | 同上 | 几:高 · 材2 | 几:高 · 材3 |
| `trail` | C:15 | C:40 | G:400 | G:800 | G:3000 | G:8000 |
| `ground.telegraph` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `shock` | 材:静 | 材1 | 材1 | 材2 | 材2 | 材2 |
| `emission.debris` | C:15 | C:50 | G:400 | G:800 | G:3000 | G:12000 |
| `decal` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:2 | 光:2 | 光:2 阴 |

**元素敏感度**：`core` 基础形、`trail` 撕裂方式、`emission.debris` 形态与运动场、`decal` 类型高度敏感。

---

### A09 扩散环 `wave_ring`

坐标：`S-A · T-I / T-D · H-W`

**职责**：从中心向外扩张的环状或面状波前，可在平面上（地面震荡）或竖直面上（冲击面），是"范围推开"的独立结构。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `shock.front` | 波前主环（外扩变薄，前缘锐后缘散） | ★material（极坐标 SDF 环 + 噪声扰边）· mesh（环面 + 顶点位移） | 必需 |
| `shock.after` | 后续次级环（可多道，间隔） | material | 可选 |
| `ground.wake` | 环经过后留下的短暂地面翻起 / 痕迹 | material | 可选 |
| `emission.lift` | 环经过时从地面卷起的粒子 | ★gpu_particles（环形发射体随半径移动）· cpu_particles | 可选 |
| `light` | 中心局部光随环扩张衰减 | ★local_light | 必需 |

**节拍**：起手 = 峭点 `impact` 0 s；扩张 0.2~1.5 s（`expandDuration`，半径从 0 到 `maxRadius`，`ringCount` 道环按 `ringInterval` 依次）；收尾 `end` 0.2~0.8 s。

**2D/3D 投影差异**：3D 环面网格贴地（或朝法线的 quad），顶点位移使地面"鼓起"；2D 环是极坐标 quad，若代表地面震荡则压成椭圆，若代表竖直冲击面则保持正圆并置于角色前层。

**对外接口（特有参数）**：`maxRadius` float 0.5~20 m；`expandDuration` float 0.1~2 s；`ringCount` int 1~5；`ringInterval` float 0.05~0.5 s；`thickness` float 0.02~1 m；`orientation` enum `ground/facing/normal`；`liftParticles` bool。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `shock.front` | 材:静 | 材1 | 材2 | 材2 | 材2 + 几:中 | 材3 + 几:高 顶点位移 |
| `shock.after` | 关 | 1 道 | 2 道 | 2 道 | 3 道 | 5 道 |
| `ground.wake` | 关 | 关 | 材1 | 材1 | 材1 | 材2 |
| `emission.lift` | 关 | C:30 | G:300 | G:600 | G:2500 | G:8000 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`shock.front` 边缘形态（平滑 / 锯齿 / 液态飞沫 / 电弧）与 `emission.lift` 粒子形态高度敏感。

---

### A10 连锁 `chain_link`

坐标：`S-L（多段）· T-Q · H-B×N`

**职责**：能量在多个目标之间按顺序跳跃，每段是一条短暂的连接线，每个节点有到达闪光；整体呈现"依次传递"的节律。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `link.segment` | 节点间的连线（弧 / 折线 / 丝带），逐段出现逐段消 | ★mesh（LineRenderer / 程序化折线带）+ material（沿 U 流动、抖动） | 必需 |
| `flash.node` | 每个节点到达时的闪光 | ★material · local_light | 必需 |
| `emission.node` | 节点处的短促粒子迸射 | cpu_particles · ★gpu_particles | 可选 |
| `edge` | 连线的外围柔光 | material | 可选 |
| `light.node` | 节点局部光（按跳跃依次点亮） | ★local_light | 必需（低档只留最新节点 1 盏） |

**节拍**：起手 `launch`（第一段出现）；每段 `hopInterval` 0.03~0.3 s；每次到达节点发出 `onHop(index, position)`；段寿命 `segmentLifetime` 0.1~0.5 s；`end` 在最后一跳后 0.2~0.6 s。

**2D/3D 投影差异**：3D 连线是空间折线（LineRenderer 面向相机）；2D 连线在 XY 平面，节点闪光 quad 置于角色前层；3D 每节点一盏 Point Light（档位上限），2D 用 Light2D Point。

**对外接口（特有参数）**：`hopCount` int 1~12；`hopInterval` float；`segmentLifetime` float；`jitter` float 0~1（折线抖动幅度）；`sag` float -1~1（弧线下垂 / 上拱）；`damping` float 0~1（每跳强度衰减）；`topology` enum `chain/fan/star`。事件：入 `setNodes(Vector3[])` / `addNode(position)`；出 `onHop`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `link.segment` | 4 顶点 · 材:静 | 8 顶点 · 材1 | 16 顶点 · 材2 | 同上 | 32 顶点 · 材2 | 64 顶点 · 材3 |
| `flash.node` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.node` | 关 | C:10 / 节点 | G:100 / 节点 | G:200 / 节点 | G:500 / 节点 | G:1500 / 节点 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `light.node` | 光:烘 | 光:1（最新节点） | 光:2 | 光:2 | 光:3 | 光:4 |

**元素敏感度**：`link.segment` 线形（锯齿分叉 / 平滑丝带 / 珠串 / 藤蔓）与 `emission.node` 迸射形态高度敏感。

---

### A11 位移 `displacement`

坐标：`S-L（起点→终点）· T-T / T-I · H-B`

**职责**：主体从起点到终点的位移可视化——连续冲刺留下残影与轨迹，或瞬移在起点消失、终点出现。两者是同一结构在 `mode` 上的差异（瞬移 = 轨迹长度 0 + 起终点各自的显 / 隐层）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `trail.afterimage` | 残影：沿路径以固定间隔留下主体轮廓拷贝并淡出 | ★mesh（复制主体网格 / Sprite 网格 + 阈值消散材质） | 冲刺必需 |
| `trail.streak` | 速度线 / 拉丝 | ★mesh（Trail 网格）· gpu_particles | 可选 |
| `flash.depart` | 起点消失闪 / 尘土 | material · cpu_particles · local_light | 瞬移必需 |
| `flash.arrive` | 终点出现闪 | material · local_light | 瞬移必需 |
| `emission.dust` | 起步 / 落地扬起的碎屑 | ★cpu_particles · gpu_particles | 可选 |
| `light` | 随主体或在起终点各一 | ★local_light | 必需 |

**节拍**：起手 `launch` 0~0.1 s（depart）；行进 `travel` 0.1~0.6 s（冲刺）/ 0 s（瞬移，起终点间隔 `blinkGap` 0~0.3 s）；峭点 `impact` = 到达（arrive）；收尾 `end` 0.2~0.8 s（残影淡出）。

**2D/3D 投影差异**：3D 残影是主体网格拷贝（需要用户提供 `subjectRenderer` 引用；无引用时退化为包围盒几何残影）；2D 残影是 Sprite 网格拷贝（同样需引用，退化为 quad）；3D 速度线沿运动方向面向相机，2D 在平面。

**对外接口（特有参数）**：`mode` enum `dash/blink`；`afterimageCount` int 0~12；`afterimageInterval` float 0.02~0.1 s；`afterimageLifetime` float 0.1~0.6 s；`blinkGap` float；`subjectRenderer` Renderer 引用（可空）；`groundDust` bool。事件：标准 + 入 `setTravelPose`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `trail.afterimage` | 2 个 · 材:静 | 3 个 · 材1 | 5 个 · 材1 | 6 个 · 材2 | 8 个 · 材2 | 12 个 · 材3 |
| `trail.streak` | 关 | 网格 · 材:静 | 网格 · 材1 | 同上 | 网格 + G:500 | 网格 + G:2000 |
| `flash.depart/arrive` | 材:静 | 材1 | 材1 + 光 | 同上 | 同上 | 同上 |
| `emission.dust` | C:8 | C:20 | C:40 | G:200 | G:600 | G:2000 |
| `light` | 光:烘 | 光:1 | 光:2 | 光:2 | 光:2 | 光:2 |

**元素敏感度**：`trail.afterimage` 消散方式、`trail.streak` 线形、`emission.dust` 粒子类型随元素显著变化；`flash.*` 仅色板。

---

### A12 牵引链接 `tether_link`

坐标：`S-L · T-S · H-2`

**职责**：两个运动主体之间持续存在的连接（链 / 绳 / 能量带），随两端距离变化张紧、松弛、断裂。与 A04 光束的差别：两端都是动主体、有物理感的下垂与张力、可断裂。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `link.body` | 连接主体：悬链线 / 直线带，张力决定下垂与粗细 | ★mesh（程序化悬链带，`segments` 个顶点）+ material（沿 U 流动、张力驱动亮度） | 必需 |
| `link.pulse` | 沿链传递的脉冲（方向：源→目标或往复） | material（U 方向脉冲阈值） | 可选 |
| `flash.anchor` | 两端锚点闪光 / 环 | ★material · local_light | 必需 |
| `emission.strain` | 张力超阈值时从链上迸出的粒子 | cpu_particles · ★gpu_particles | 可选 |
| `flash.break` | 断裂瞬闪 + 两半回弹 | material + mesh 动画 | 可选 |
| `light` | 两端锚点局部光 | ★local_light | 必需 |

**节拍**：起手 `launch` 0.05~0.3 s（链从源射向目标）；持续 `sustain`（张力 `tension` 由两端距离与 `restLength` 计算并出事件 `onTensionChanged`）；峭点 `impact` = `break`（距离 > `breakLength` 或外部调用）；收尾 `end` 0.2~0.6 s。

**2D/3D 投影差异**：3D 链是空间悬链（重力方向下垂），面向相机的带或圆柱链；2D 链在 XY 平面下垂，排序层在两个主体之间层。

**对外接口（特有参数）**：`restLength` float；`breakLength` float（0 = 不断裂）；`sag` float 0~1；`segments` int 4~64；`pulseSpeed` float；`pulseDirection` enum `forward/backward/pingpong`；`thickness` float。事件：入 `setEndpoints(a, b)` / `break()`；出 `onTensionChanged(t)`、`onBreak`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `link.body` | 4 段 · 材:静 | 8 段 · 材1 | 16 段 · 材2 | 同上 | 32 段 · 材2 | 64 段 · 材3 |
| `link.pulse` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `flash.anchor` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.strain` | 关 | 关 | C:30 | G:200 | G:600 | G:2000 |
| `flash.break` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:2 | 光:2 | 光:2 | 光:2 |

**元素敏感度**：`link.body` 形态（实链 / 液带 / 电弧 / 藤蔓 / 光带）与 `emission.strain` 形态高度敏感。

---

### A13 召唤显现 `summon_manifest`

坐标：`S-A→S-S · T-Q · H-W`

**职责**：在地面上先出现阵 / 裂口，再由地面向上升起一根光柱或裂隙中涌出体积，最终把"被召唤物"从无到有显现出来（显现本身作用于用户提供的目标渲染器，退化为几何壳）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `ground.circle` | 召唤阵 / 裂口：SDF 图形 + 旋转 + 进度点亮 | ★material（SDF 阵纹，程序化生成而非贴图；外部出图仅允许作遮罩辅助） | 必需 |
| `column` | 竖直光柱 / 涌出体积 | ★mesh（圆柱 / 双 quad）+ material | 必需 |
| `emission.rise` | 阵内向上的粒子 | ★gpu_particles · cpu_particles | 可选 |
| `mesh_shell.reveal` | 被召唤物的显现壳：阈值从下到上扫过（作用于目标渲染器材质或包围几何） | ★material（世界高度阈值消散反向） | 必需 |
| `flash` | 显现完成瞬闪 | material · local_light | 可选 |
| `light` | 阵中心 + 显现峰值 | ★local_light | 必需 |

**节拍**：起手 `launch` 0.3~1.5 s（阵点亮）；持续 `sustain` 0.3~1 s（光柱升起，粒子上涌）；峭点 `impact` = `reveal` 0.2~0.8 s（阈值扫过）；收尾 `end` 0.5~2 s（阵淡出、柱抽离）。

**2D/3D 投影差异**：3D 阵是贴地 quad / Decal，光柱真实圆柱，显现用世界 Y 阈值；2D 阵是椭圆 quad（Sorting Layer 在角色下），光柱是竖直 quad 排在角色后层，显现用 Sprite 局部 Y 阈值。

**对外接口（特有参数）**：`circleRadius` float；`circleSpin` float °/s；`columnHeight` float；`revealDirection` enum `bottomUp/topDown/center`；`revealDuration` float；`targetRenderer` Renderer 引用（可空）。事件：标准 + 出 `onRevealComplete`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `ground.circle` | 材:静 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `column` | 双 quad · 材1 | 双 quad · 材1 | 圆柱 · 材2 | 同上 | 同上 | 圆柱 · 材3 |
| `emission.rise` | 关 | C:30 | G:300 | G:600 | G:2000 | G:6000 |
| `mesh_shell.reveal` | 材:静（硬切） | 材1 | 材2 | 材2 | 材2 | 材3 |
| `flash` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:2 | 光:2 | 光:2 阴 |

**元素敏感度**：`ground.circle` 纹样类型、`column` 形态（光柱 / 涌出流体 / 裂隙火光 / 藤蔓）、`emission.rise` 与 `mesh_shell.reveal` 的边缘形态高度敏感。

---

## 5. B 状态持续（Sustained States）

本类共同特征：时间形态 T-S（循环直到外部 `end`），节拍统一为 `launch`（0.1~0.5 s 出现）→ `sustain`（循环，材质相位与粒子发射率稳定）→ `end`（0.2~0.8 s 消散）；各节只写差异。持续类对 overdraw 最敏感，因此降级表优先削 `body` 层。

### B01 地面光环 `aura_ground`

坐标：`S-A · T-S · H-B`

**职责**：跟随主体脚下的贴地环 / 阵，持续旋转流动，向上逸散少量粒子，是"处于某状态"的地面标识。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `ground.ring` | 贴地环 / 阵纹（SDF 环 + 旋转纹 + 呼吸） | ★material | 必需 |
| `ground.glow` | 环内柔光填充 | material | 可选 |
| `emission.rise` | 从环上缓慢上升的粒子 | ★cpu_particles · gpu_particles | 可选 |
| `light` | 脚下局部光（向上照主体） | ★local_light | 必需 |

**节拍**：标准持续型；`sustain` 中 `ground.ring` 以 `spinSpeed` 旋转、以 `pulseRate` 呼吸；可选 `impact` = `pulse`（外部触发一次强脉冲）。

**2D/3D 投影差异**：3D 贴地 quad / Decal Projector 贴合地形；2D 椭圆 quad，Sorting Layer 在主体下方。

**对外接口（特有参数）**：`ringRadius` float 0.3~3 m；`spinSpeed` float -180~180 °/s；`pulseRate` float 0~4 Hz；`patternIndex` int（程序化阵纹族选择）。事件：标准 + 入 `pulse()`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `ground.ring` | 材:静（只旋转） | 材1 | 材2 | 材2 | 材2 | 材3 |
| `ground.glow` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `emission.rise` | 关 | C:12 | C:30 | C:40 | G:300 | G:1000 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`ground.ring` 纹样与流动方式、`emission.rise` 粒子形态与上升方式（飘 / 跳 / 滴落反向）显著敏感。

---

### B02 体表光环 `aura_body`

坐标：`S-S · T-S · H-B`

**职责**：包裹主体表面的持续能量层——沿主体轮廓的边缘光、体表流动、周身逸散粒子。作用于用户提供的目标渲染器，无引用时退化为包围几何壳。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `edge` | 轮廓边缘光（菲涅尔 / Sprite 轮廓 SDF 外扩） | ★material（叠加到目标材质或复制渲染器外扩） | 必需 |
| `body` | 体表流动层（噪声沿表面流动，阈值闪烁） | ★material | 可选 |
| `emission.ambient` | 周身逸散粒子（从表面 / 包围体发射） | ★cpu_particles（网格 / Sprite 形状发射）· gpu_particles（SDF 表面发射） | 可选 |
| `light` | 主体中心局部光 | ★local_light | 必需 |

**节拍**：标准持续型；`sustain` 中可选 `impact` = `pulse`。

**2D/3D 投影差异**：3D `edge` 用菲涅尔 + 顶点法线外扩壳网格（复制目标网格）；2D `edge` 用 Sprite alpha 的 SDF 外扩（复制 SpriteRenderer 到外扩材质）；粒子 3D 从网格表面发射，2D 从 Sprite 形状发射（ParticleSystem Shape=SpriteRenderer）。

**对外接口（特有参数）**：`edgeWidth` float 0.005~0.2；`flowSpeed` float；`flowDirection` enum `up/down/radial/random`（增益向上、减益向下的语义靠此参数）；`targetRenderer` Renderer 引用（可空）。事件：标准 + 入 `pulse()`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `edge` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `body` | 关 | 关 | 材1 | 材1 | 材2 | 材3 |
| `emission.ambient` | C:10 | C:25 | C:60 | C:80 | G:800 | G:3000 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`body` 流动形态与 `emission.ambient` 粒子运动（上飘 / 下滴 / 跳跃 / 环绕）高度敏感；`edge` 锐度中等敏感。

---

### B03 环绕体 `orbitals`

坐标：`S-M · T-S · H-B`

**职责**：若干离散子体围绕主体做规则轨道运动（环 / 螺旋 / 随机游走），每个子体自带小核与短尾，是"携带某种力量"的空间化表现。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `orbit.core` | N 个轨道子体的核心 | ★gpu_particles（轨道场，网格 / quad 粒子）· cpu_particles（Orbital 速度模块）· mesh（脚本驱动 N 个几何） | 必需 |
| `orbit.trail` | 每个子体的短尾 | ★cpu_particles Trails 模块 · mesh（Trail 网格 × N） | 可选 |
| `link` | 子体之间或子体到主体的细连线 | mesh（LineRenderer） | 可选 |
| `light` | 主体中心一盏（随子体数脉冲） | ★local_light | 必需 |

**节拍**：标准持续型；`launch` 时子体从中心射出到轨道；`impact` = `consume(index)`（消耗一个子体射向外部，出事件 `onConsumed`，可与 A01 编排）。

**2D/3D 投影差异**：3D 轨道是倾斜圆 / 球面螺旋，子体网格有深度排序；2D 轨道是椭圆（透视暗示），子体过主体前 / 后时切换 Sorting Order 造前后错觉。

**对外接口（特有参数）**：`count` int 1~12；`orbitRadius` float；`orbitSpeed` float °/s；`orbitPattern` enum `ring/spiral/chaotic/dualRing`；`tilt` float 0~90 °；`bodyRadius` float。事件：标准 + 入 `consume(index)` / `setCount(n)`；出 `onConsumed(index)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `orbit.core` | C:4（quad） | C:8 | C:12 | 几 × 8 · 材1 | 几 × 12 · 材2 | 几 × 12 · 材3 |
| `orbit.trail` | 关 | C Trails 短 | C Trails | 网格 Trail × N | 同上 | 同上 + G:2000 |
| `link` | 关 | 关 | 关 | 4 顶点 | 8 顶点 | 16 顶点 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`orbit.core` 子体基础形（球 / 晶簇 / 电球 / 水滴 / 齿轮）与 `orbit.trail` 形态高度敏感；轨道模式中等（部分元素偏好 chaotic）。

---

### B04 护盾 `shield`

坐标：`S-S · T-S · H-B`

**职责**：包裹主体的半透明几何壳，持续存在，受击点局部涟漪，随耐久下降出现裂纹，破裂时碎片飞散。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `mesh_shell` | 壳体：球 / 椭球 / 六边形拼接壳 / 半球罩，菲涅尔边亮中透 | ★mesh（球 / 程序化多面体）+ material（菲涅尔 + 六边 / 噪声纹 + 边缘光） | 必需 |
| `edge` | 壳与地面 / 主体交接处的接触环 | material | 可选 |
| `flash.hit` | 受击点涟漪：以命中点为中心的环形波在壳表面扩散（材质接收 `hitPoint` 数组） | ★material | 必需 |
| `mesh_shell.crack` | 裂纹层：随 `integrity` 下降由 SDF / 噪声阈值显现 | material（同一材质的裂纹通道） | 可选 |
| `debris` | 破裂碎片：预破碎壳网格 + Rigidbody 或网格粒子 | ★mesh（预破碎 + Rigidbody）· gpu_particles（网格粒子） | 可选 |
| `flash.break` | 破裂闪 | material · local_light | 可选 |
| `light` | 壳内 / 中心局部光 | ★local_light | 必需 |

**节拍**：`launch` 0.1~0.4 s（壳从主体中心膨胀成形）；`sustain`（呼吸、流动）；`impact` = `hitAt(point)` 涟漪 0.2~0.5 s（可叠加 ≤ 4 个）；`break` 0.05~0.15 s 闪 + 碎片 0.5~1.5 s；`end` 0.2~0.5 s（正常撤销时壳收缩）。

**2D/3D 投影差异**：3D 真实球壳 / 多面体壳，菲涅尔与深度排序天然；2D 壳是圆 / 椭圆 quad 的 SDF 环 + 内部低透填充，涟漪用平面极坐标以命中点偏移；2D 碎片为 Sprite 网格切片 + Rigidbody2D。

**对外接口（特有参数）**：`shellShape` enum `sphere/ellipsoid/hemisphere/hexShell/flatDisc`；`shellRadius` float / `shellSize` Vector3；`integrity` float 0~1（外部驱动裂纹进度）；`hitRippleDuration` float；`maxSimultaneousHits` int 1~4；`fragmentCount` int；`breakForce` float。事件：入 `hitAt(point, strength)` / `setIntegrity(v)` / `break()`；出 `onHitRipple` / `onBreak`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `mesh_shell` | 几:低 · 材1 | 几:低 · 材2 | 几:中 · 材2 | 几:中 · 材2 | 几:高 · 材3 | 几:高 · 材3 + 顶点位移 |
| `edge` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `flash.hit` | 1 点 · 材1 | 2 点 | 4 点 | 4 点 | 4 点 | 4 点 |
| `mesh_shell.crack` | 关 | 材:静 | 材1 | 材1 | 材2 | 材2 |
| `debris` | 块:6 无刚体 | 块:12 | 块:24 | 块:32 | 块:64 | 块:128 |
| `flash.break` | 材:静 | 材1 | 材1 + 光 | 同上 | 同上 | 同上 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`mesh_shell` 表面纹理与透明度（晶体 / 液膜 / 电网 / 岩壳 / 光膜）、`debris` 碎片形态与运动（碎晶下坠 / 液滴溅 / 电弧散 / 光片飘）高度敏感。

---

### B05 蓄力汇聚 `charge_gather`

坐标：`S-M→S-P · T-Q · H-K`

**职责**：能量从周围向一个挂点汇聚并累积，核心逐级增大变亮，直到释放（交接给其他原型）或被打断（散逸）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `emission.inward` | 向内汇聚的粒子（从球面 / 环向中心，带吸引场） | ★gpu_particles（吸引力场）· cpu_particles（反向速度 / Attract 用 Force over Lifetime 近似） | 必需 |
| `core` | 累积核心，随 `chargeLevel` 缩放变亮 | ★material（SDF 核 + 噪声）· mesh | 必需 |
| `edge` | 核心边缘不稳定闪烁（高蓄力时溢出感） | material | 可选 |
| `link.inward` | 从周围向核心的细线（少量） | mesh（LineRenderer） | 可选 |
| `ground.ring` | 脚下 / 挂点下方的蓄力圈（可选，随等级点亮分段） | material | 可选 |
| `light` | 挂点局部光随等级增强 | ★local_light | 必需 |

**节拍**：`launch` 0.1~0.3 s（汇聚开始）；`sustain` = 蓄力（`chargeLevel` 0→1 由内建 `chargeDuration` 或外部驱动，分 `levelCount` 档，每过一档出 `onLevel(i)` 并短促脉冲）；`impact` = `release`（核心闪爆并交接，本原型只做闪与清空）或 `interrupt`（核心散逸成外抛粒子）；`end` 0.1~0.4 s。

**2D/3D 投影差异**：3D 粒子从球面向中心汇聚；2D 从圆周向中心，核心 quad 排序在角色前层；3D 局部光 Point，2D Light2D Point。

**对外接口（特有参数）**：`chargeDuration` float 0.3~5 s；`chargeLevel` float 0~1（外部驱动）；`levelCount` int 1~5；`gatherRadius` float；`overchargeShake` float 0~1；`interruptScatter` float。事件：入 `setChargeLevel(v)` / `release()` / `interrupt()`；出 `onLevel(i)` / `onRelease` / `onInterrupt`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `emission.inward` | C:20 | C:50 | G:400 | G:800 | G:3000 | G:10000 |
| `core` | 材1 | 材2 | 材2 | 材2 | 材3 | 材3 + 顶点位移 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材2 |
| `link.inward` | 关 | 关 | 2 条 | 4 条 | 6 条 | 8 条 |
| `ground.ring` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`emission.inward` 粒子形态与汇聚路径（直线 / 螺旋 / 跳跃）、`core` 累积形态（球 / 晶簇生长 / 电球 / 水团）高度敏感。

---

### B06 引导施法 `channel_sustain`

坐标：`S-P + S-A · T-S · H-K + H-B`

**职责**：持续施法的可视化——挂点上稳定的能量核心、脚下的引导阵、两者之间的联系，直到结束或被打断（打断时有明显的失稳散逸）。与 B05 的差别：不累积，稳态。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 挂点能量核心（稳态呼吸） | ★material · mesh | 必需 |
| `ground.ring` | 脚下引导阵（稳态旋转） | ★material | 可选 |
| `column` | 阵到核心之间的细光柱 / 上升流 | mesh + material · gpu_particles | 可选 |
| `emission.ambient` | 核心周围的稳态粒子 | ★cpu_particles · gpu_particles | 可选 |
| `light` | 挂点局部光 | ★local_light | 必需 |

**节拍**：标准持续型；`impact` = `interrupt`（核心破碎 + 粒子外散 0.2~0.4 s）；`end`（正常结束，核心收缩）。

**2D/3D 投影差异**：同 B01 + B05 的组合规则。

**对外接口（特有参数）**：`coreRadius` float；`ringRadius` float；`interruptScatter` float；`stability` float 0~1（低稳定度时核心抖动增强）。事件：入 `interrupt()`；出 `onInterrupt`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 材1 | 材2 | 材2 | 材2 | 材3 | 材3 |
| `ground.ring` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `column` | 关 | 关 | 双 quad · 材1 | 同上 | 同上 + G:500 | 圆柱 · 材2 + G:1500 |
| `emission.ambient` | C:10 | C:25 | C:50 | C:60 | G:600 | G:2000 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`core` 与 `emission.ambient` 形态高度敏感；`ground.ring` 纹样中等。

---

### B07 挂点标记 `attach_marker`

坐标：`S-P · T-S · H-K`

**职责**：附着在主体某个挂点（头顶 / 胸口 / 手）的持续小型标识体——一个小核 + 周围少量粒子 + 微光，用于表达"被标记 / 被锁定 / 有状态"，体积小、成本低、可多个并存。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 小型符号核（SDF 几何形：环 / 菱 / 箭 / 眼 等程序化形） | ★material（SDF 形状库）· mesh | 必需 |
| `emission.ambient` | 周围微粒 | ★cpu_particles | 可选 |
| `light` | 微弱局部光 | local_light | 可选（本原型唯一允许无光的发光类：常多实例并存） |

**节拍**：标准持续型；`sustain` 中 `core` 以 `bobAmplitude` 上下浮动、`spinSpeed` 自旋；`impact` = `pulse`。

**2D/3D 投影差异**：3D `core` 是面向相机的 quad 或小几何（billboard 由脚本），2D 是 quad 排序在角色前层。

**对外接口（特有参数）**：`symbol` enum（程序化 SDF 形状族：`ring/diamond/arrow/eye/cross/spiral/hex`）；`size` float 0.05~0.5 m；`bobAmplitude` float；`spinSpeed` float；`stackCount` int 1~5（叠层数，用于层数显示）。事件：标准 + 入 `pulse()` / `setStack(n)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `emission.ambient` | 关 | C:6 | C:12 | C:12 | C:20 | C:30 |
| `light` | 关 | 关 | 关 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`core` 仅色板与噪声抖动；`emission.ambient` 粒子形态随元素中等敏感。本原型元素敏感度最低。

---

### B08 武器附魔 `weapon_enchant`

坐标：`S-S（沿武器几何）· T-S · H-K`

**职责**：沿武器（或用户提供的任意长条几何）表面附着的持续能量层，随武器运动拖出短尾，挥动时尾迹增强（与 A05 编排时自然衔接）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `body` | 武器表面能量层（复制目标网格外扩 / Sprite 外扩，沿长轴流动） | ★material | 必需 |
| `edge` | 沿刃锋 / 长轴的亮边 | material | 可选 |
| `trail` | 随武器运动的短尾迹（速度门控：静止时几乎不见） | ★mesh（Trail 网格，尖端 / 根部两点）· cpu_particles | 必需 |
| `emission.shed` | 从表面脱落的微粒 | ★cpu_particles · gpu_particles（网格表面发射） | 可选 |
| `light` | 武器中点局部光 | ★local_light | 必需 |

**节拍**：标准持续型；`sustain` 中 `trail` 宽度按武器速度门控（`speedGate`）。

**2D/3D 投影差异**：3D 用目标网格外扩壳；2D 用 Sprite 外扩或沿 `weaponTip/weaponBase` 两点生成的长 quad；尾迹网格两个维度均用 Trail 网格（2D 在平面）。

**对外接口（特有参数）**：`weaponBase` / `weaponTip` Transform 引用（或 `targetRenderer`）；`flowSpeed` float；`speedGate` float（m/s，低于此尾迹隐藏）；`trailLifetime` float；`shedRate` float。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `body` | 材:静 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `trail` | 网格 · 材:静 | 网格 · 材1 | 网格 · 材2 | 同上 | 同上 | 网格 · 材3 |
| `emission.shed` | 关 | C:10 | C:30 | C:40 | G:400 | G:1500 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`body` 表面流动形态（火舌 / 霜结 / 电弧跳 / 水膜 / 毒滴）、`trail` 与 `emission.shed` 高度敏感。

---

### B09 侵蚀状态 `afflict_erode`

坐标：`S-S · T-S · H-B`

**职责**：表达主体正被某种力量持续侵蚀 / 覆盖：体表由局部向全身蔓延的覆盖层（阈值随 `coverage` 推进）、从体表滴落 / 飘散的粒子、暗淡的局部光。与 B02 的差别：有"蔓延进度"结构，且视觉重心向下 / 向内。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `body.coverage` | 覆盖层：噪声阈值随 `coverage` 从种子点蔓延（作用于目标渲染器材质或外扩壳） | ★material | 必需 |
| `edge.front` | 蔓延前沿亮边 | material（同一材质的阈值边缘通道） | 可选 |
| `emission.shed` | 从表面向下滴落 / 飘散的粒子 | ★cpu_particles · gpu_particles | 必需 |
| `ground.pool` | 脚下积累的残迹（随时间增长） | material | 可选 |
| `light` | 暗色局部光（负强度不可用，用低饱和低亮度色） | local_light | 可选 |

**节拍**：`launch` 0.2~0.6 s（从种子点出现）；`sustain`（`coverage` 由内建速率或外部驱动 0→1；每 `tickInterval` 出 `onTick` 并短促脉冲）；`end` 0.3~1 s（覆盖层反向收缩或整体碎裂脱落）。

**2D/3D 投影差异**：3D 覆盖层用目标网格外扩壳 + 世界空间噪声阈值；2D 用 Sprite 复制 + 局部 UV 噪声阈值；`ground.pool` 3D 贴地 quad，2D 椭圆 quad 在角色下。

**对外接口（特有参数）**：`coverage` float 0~1（外部驱动）；`spreadSpeed` float；`seedPoint` Vector3（局部）；`tickInterval` float 0~2 s（0 = 不 tick）；`shedRate` float；`targetRenderer` Renderer 引用（可空）。事件：入 `setCoverage(v)`；出 `onTick`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `body.coverage` | 材1（硬阈值） | 材1 | 材2 | 材2 | 材2 | 材3 |
| `edge.front` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.shed` | C:8 | C:20 | C:40 | C:60 | G:500 | G:2000 |
| `ground.pool` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：全部层高度敏感——覆盖层形态（结晶 / 燃烧碳化 / 腐蚀斑 / 电麻纹 / 湿透 / 阴影侵蚀）决定本原型的绝大部分外观。

---

### B10 束缚禁锢 `restrain_bind`

坐标：`S-S（笼 / 环）· T-S · H-B`

**职责**：包围主体的束缚几何——环、笼、锁链、藤蔓、冰棱等程序化几何从地面 / 四周合拢，持续存在并微动，解除时崩解。与 B04 的差别：是外部施加、几何为开放结构（环 / 条 / 棱），非连续壳。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `mesh_shell.cage` | 束缚几何：程序化环 / 条 / 棱柱 / 螺旋（`cageShape`），`launch` 时从四周合拢 | ★mesh（程序化生成）+ material | 必需 |
| `ground.ring` | 脚下束缚圈 | material | 可选 |
| `emission.ambient` | 束缚体周围微粒 | cpu_particles | 可选 |
| `debris` | 解除时崩解碎片 | mesh · cpu_particles | 可选 |
| `light` | 局部光 | ★local_light | 必需 |

**节拍**：`launch` 0.1~0.4 s（合拢，可有 `snapShake`）；`sustain`（微动、呼吸）；`impact` = `struggle`（外部触发一次挣扎抖动）；`end` 0.2~0.6 s（崩解 / 消散）。

**2D/3D 投影差异**：3D 真实几何环 / 棱围绕主体（前后有深度遮挡）；2D 分前后两组 quad / Sprite 网格，前组 Sorting Order 在主体前、后组在主体后。

**对外接口（特有参数）**：`cageShape` enum `rings/bars/spikes/spiral/chains`；`elementCount` int 3~16；`cageRadius` float；`snapShake` float；`breakOnEnd` bool。事件：入 `struggle()`；出 `onSnap`（合拢完成）。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `mesh_shell.cage` | 几:低 × 3 · 材:静 | 几:低 × 6 · 材1 | 几:中 × 8 · 材2 | 同上 | 几:高 × 12 · 材2 | 几:高 × 16 · 材3 |
| `ground.ring` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `emission.ambient` | 关 | C:8 | C:16 | C:20 | C:40 | G:600 |
| `debris` | 关 | C:10 网格 | 块:12 | 块:16 | 块:32 | 块:64 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`mesh_shell.cage` 基础几何族（冰棱 / 藤蔓 / 锁链 / 电环 / 光环 / 岩柱）高度敏感——本原型是"几何族预设"最重要的消费者。

---

## 6. C 反馈事件（Feedback Events）

本类共同特征：附着于主体（H-B），时间形态多为瞬发（T-I）或短衰减（T-D），总时长 0.2~1.5 s，节拍统一为 `launch`＝`impact`（触发即峰值）→ `end`。它们对"节拍精确度"（峰值帧、衰减曲线）最敏感，对粒子数最不敏感。

### C01 受击反应 `hit_reaction`

坐标：`S-P + S-S · T-I · H-B`

**职责**：主体被命中瞬间的反馈：命中点小闪、体表整体瞬白 / 瞬色（作用于目标渲染器）、少量碎屑，极短。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `flash.point` | 命中点小闪（方向性 SDF：星 / 裂 / 溅） | ★material · local_light | 必需 |
| `body.tint` | 体表整体闪色（目标材质叠加参数或外扩壳一帧） | ★material | 可选 |
| `emission.chips` | 命中点迸出少量碎屑 | ★cpu_particles | 可选 |
| `light` | 一帧局部光 | ★local_light | 必需 |

**节拍**：`impact` 0 s 峰值；`end` 0.1~0.3 s。

**2D/3D 投影差异**：3D `flash.point` billboard quad 朝相机 + 法线偏移；2D quad 在角色前层；`body.tint` 3D 走材质参数（需 `targetRenderer`），2D 走 SpriteRenderer 材质参数。

**对外接口（特有参数）**：`hitPoint` Vector3；`hitNormal` Vector3；`flashShape` enum `star/crack/splash/ring`；`tintStrength` float 0~1；`tintDuration` float 0.03~0.15 s；`targetRenderer`（可空）。事件：入 `impact(point, normal)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `flash.point` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `body.tint` | 材:静 | 材:静 | 材:静 | 材:静 | 材1 | 材1 |
| `emission.chips` | 关 | C:6 | C:12 | C:16 | C:24 | G:300 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`flash.point` 形状与 `emission.chips` 形态中等敏感；`body.tint` 仅色板。

---

### C02 暴击强调 `critical_emphasis`

坐标：`S-P · T-I · H-B`

**职责**：比 C01 强一档的命中反馈：更大的方向性闪光、一道快速扩散环、放射状速度线、更长的局部光衰减。与 C01 是不同原型而非参数差异，因为多了 `shock` 与 `emission.rays` 两层结构。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `flash` | 大闪（多角星 / 裂形 SDF） | ★material · local_light | 必需 |
| `shock` | 快速扩散薄环 | ★material | 必需 |
| `emission.rays` | 放射状速度线（拉长粒子或程序化线束网格） | ★mesh（线束）· cpu_particles（Stretched Billboard） | 可选 |
| `emission.chips` | 碎屑 | cpu_particles · gpu_particles | 可选 |
| `light` | 局部光峰值 + 较长衰减 | ★local_light | 必需 |

**节拍**：`impact` 0 s；峰 0.05~0.1 s；`end` 0.3~0.6 s。

**2D/3D 投影差异**：3D `shock` 朝相机或朝法线，`emission.rays` 三维放射；2D 全部在平面，`emission.rays` 用 8~16 段程序化线束 quad。

**对外接口（特有参数）**：`flashScale` float；`rayCount` int 4~24；`rayLength` float；`shockRadius` float；`freezeFrames` int 0~3（**仅**本预制体内部各层保持峰值帧数，不碰时间缩放）。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `shock` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.rays` | 关 | 线束 6 | 线束 12 | 线束 12 | 线束 16 | 线束 24 |
| `emission.chips` | 关 | C:10 | C:20 | C:30 | G:400 | G:1000 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`flash` 形状、`emission.chips` 形态中等敏感；`emission.rays` 与 `shock` 低敏感（结构性强调，元素只改色）。

---

### C03 治疗恢复 `heal_restore`

坐标：`S-S + S-M · T-D · H-B`

**职责**：主体周身缓慢上升的柔和粒子 + 体表由下向上的柔光扫过 + 脚下微弱光圈，整体节奏舒缓、向上、渐亮后渐隐。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `emission.rise` | 从主体周围 / 脚下上升的柔粒子（缓慢、轻微左右摆） | ★cpu_particles · gpu_particles | 必需 |
| `body.sweep` | 体表由下向上的柔光带（目标渲染器高度阈值） | ★material | 可选 |
| `ground.glow` | 脚下光圈（呼吸一次） | material | 可选 |
| `edge` | 轮廓柔光（短暂） | material | 可选 |
| `light` | 柔和局部光（慢起慢落） | ★local_light | 必需 |

**节拍**：`launch`＝`impact` 0.1~0.3 s 起亮；持续 0.5~1.5 s；`end` 0.3~0.8 s。可选 `sustain` 循环模式（持续治疗）。

**2D/3D 投影差异**：3D 粒子从包围体底部环发射，`body.sweep` 世界 Y 阈值；2D 粒子从 Sprite 底部横线发射，`body.sweep` Sprite 局部 Y。

**对外接口（特有参数）**：`riseHeight` float；`riseDuration` float；`sweepEnabled` bool；`loop` bool；`softness` float 0~1（粒子与光边缘柔度）。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `emission.rise` | C:12 | C:30 | C:60 | C:80 | G:800 | G:3000 |
| `body.sweep` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `ground.glow` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `edge` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`emission.rise` 粒子形态（光点 / 叶片 / 水珠 / 符文碎片）中等敏感；节奏结构不随元素改变。

---

### C04 强化获得 `empower_rise`

坐标：`S-S + S-A · T-I / T-D · H-B`

**职责**：主体获得增强的瞬间：脚下光柱猛然升起并穿过主体、体表边缘光瞬亮后维持短暂高亮、周身粒子向外爆开后上升。比 C03 更锐利、更快、有"爆发点"。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `column` | 脚下升起的光柱（快升慢消） | ★mesh（圆柱 / 双 quad）+ material | 必需 |
| `edge` | 轮廓边缘光爆亮 | ★material | 必需 |
| `emission.burst` | 爆开后上升的粒子 | ★cpu_particles · gpu_particles | 可选 |
| `ground.ring` | 脚下一道扩散环 | material | 可选 |
| `light` | 局部光爆亮后维持 | ★local_light | 必需 |

**节拍**：`impact` 0 s；峰 0.1~0.2 s；`end` 0.4~1 s。

**2D/3D 投影差异**：3D 光柱真实圆柱穿过主体；2D 光柱是竖直 quad 排在角色后层 + 一片窄 quad 在前层（造穿过错觉）。

**对外接口（特有参数）**：`columnHeight` float；`columnRadius` float；`edgeHoldDuration` float；`burstCount` int。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `column` | 双 quad · 材:静 | 双 quad · 材1 | 圆柱 · 材2 | 同上 | 同上 | 圆柱 · 材3 |
| `edge` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `emission.burst` | C:10 | C:25 | C:50 | C:60 | G:600 | G:2000 |
| `ground.ring` | 关 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`column` 形态与 `emission.burst` 形态中等敏感。

---

### C05 拾取吸收 `pickup_absorb`

坐标：`S-M→S-P · T-T · H-W→H-B`

**职责**：一个或多个小体从世界点被吸向主体（弧线 / 螺旋），到达时主体处小闪。是世界空间的"获得"，与 F08（UI 内收集飞行）结构相同但维度与附着不同。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `orbit.body` | 被吸的小体（N 个，可有起始悬浮抖动） | ★gpu_particles（目标吸引场）· cpu_particles · mesh | 必需 |
| `trail` | 小体尾迹 | cpu_particles Trails · mesh | 可选 |
| `flash.absorb` | 到达主体的小闪（每个到达一次） | ★material · local_light | 必需 |
| `light` | 主体处局部光随到达脉冲 | local_light | 可选 |

**节拍**：`launch` 0~0.3 s（悬浮 / 抖动）；`travel` 0.2~0.8 s（`pathStyle` 直线 / 弧 / 螺旋，有加速）；`impact` = 每个到达（出 `onAbsorb(index)`）；`end` 0.1~0.3 s。

**2D/3D 投影差异**：3D 空间弧线；2D 平面弧线，小体 Sorting Order 在角色前层。

**对外接口（特有参数）**：`count` int 1~50；`pathStyle` enum `straight/arc/spiral`；`hoverTime` float；`travelDuration` float；`stagger` float；`targetTransform` Transform 引用。事件：入 `setTarget(transform)`；出 `onAbsorb(index)`、`onAllAbsorbed`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `orbit.body` | C:6 | C:15 | C:30 | G:100 | G:300 | G:1000 |
| `trail` | 关 | C Trails 短 | C Trails | 同上 | 同上 + 材1 | 同上 + 材2 |
| `flash.absorb` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`orbit.body` 基础形与 `trail` 形态中等敏感。

---

### C06 消散退场 `dissolve_out`

坐标：`S-S · T-D · H-B`

**职责**：主体从有到无：作用于目标渲染器的阈值消散（方向 / 噪声形态可选）、消散前沿的亮边、从消散边缘脱落的粒子、随之减弱的局部光。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `mesh_shell.dissolve` | 目标渲染器的阈值消散（无引用时退化为几何壳） | ★material（噪声 / 方向阈值 + 前沿边） | 必需 |
| `edge.front` | 消散前沿亮边 | ★material（同材质通道） | 必需 |
| `emission.shed` | 从前沿脱落的粒子（形态与运动随元素） | ★gpu_particles（从阈值边缘发射 / SDF 表面）· cpu_particles（网格 / Sprite 形状发射 + 阈值遮罩近似） | 必需 |
| `ground.residue` | 脚下残迹（短寿命） | material | 可选 |
| `light` | 局部光随进度减弱 | ★local_light | 必需 |

**节拍**：`launch`＝`impact` 0 s；`travel` = 消散进度 0→1（`dissolveDuration` 0.3~2 s，或外部驱动 `progress`）；`end` 0.2~0.6 s（粒子落尽）。

**2D/3D 投影差异**：3D 世界空间噪声阈值 + 方向（上 / 下 / 中心）；2D Sprite 局部 UV 噪声 + 方向；粒子 3D 从网格表面发射受重力 / 浮力，2D 从 Sprite 形状发射。

**对外接口（特有参数）**：`progress` float 0~1（外部驱动）；`dissolveDuration` float；`direction` enum `bottomUp/topDown/center/noise`；`edgeWidth` float；`shedDensity` float；`targetRenderer`（可空）。事件：入 `setProgress(v)`；出 `onDissolved`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `mesh_shell.dissolve` | 材1（硬阈值） | 材1 | 材2 | 材2 | 材2 | 材3 |
| `edge.front` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.shed` | C:15 | C:40 | G:400 | G:800 | G:3000 | G:10000 |
| `ground.residue` | 关 | 关 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：全部层高度敏感——消散形态（燃尽 / 碎晶 / 化水 / 电离 / 风散 / 沙化 / 溶解 / 像素化归风格轴）是元素表现力的核心场景。

---

### C07 登场显现 `entrance_in`

坐标：`S-S · T-Q · H-B`

**职责**：主体从无到有（C06 的时间反演，但结构不同：有预示阶段、有落地 / 定形冲击）。预示（脚下光 / 空间扭动）→ 显现（反向阈值）→ 定形冲击（环 + 光峰）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `ground.telegraph` | 预示：脚下光圈 / 阵 | ★material | 可选 |
| `mesh_shell.reveal` | 目标渲染器反向阈值显现 | ★material | 必需 |
| `edge.front` | 显现前沿亮边 | material | 可选 |
| `emission.gather` | 向主体汇聚 / 从主体爆开的粒子（`gatherMode`） | ★cpu_particles · gpu_particles | 可选 |
| `shock` | 定形瞬间脚下 / 周身一道环 | material | 可选 |
| `light` | 峰值局部光 | ★local_light | 必需 |

**节拍**：`launch` 0.1~0.8 s（预示）；`travel` = 显现 0.2~0.8 s；`impact` = 定形；`end` 0.2~0.5 s。

**2D/3D 投影差异**：同 C06 反向；`shock` 3D 贴地环 / 朝相机，2D 椭圆或正圆 quad。

**对外接口（特有参数）**：`telegraphDuration` float；`revealDuration` float；`direction` enum；`gatherMode` enum `inward/outward/none`；`landingShock` bool；`targetRenderer`（可空）。事件：标准 + 出 `onRevealComplete`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `ground.telegraph` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `mesh_shell.reveal` | 材1 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `edge.front` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.gather` | C:10 | C:30 | C:60 | G:500 | G:2000 | G:6000 |
| `shock` | 关 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`mesh_shell.reveal` 与 `emission.gather` 形态高度敏感。

---

### C08 格挡弹反 `parry_deflect`

坐标：`S-A（小面）· T-I · H-K`

**职责**：在格挡点出现一个短促的方向性面（扇 / 弧 / 盘）承接来袭，伴随沿入射反向的火星与一道锐利闪光。与 C01 的差别：有"面"层与方向性反射，与 B04 的差别：瞬发、无壳体持续。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `shock.plate` | 格挡面：扇 / 弧 / 盘的 SDF 面，向入射方向外扩后消 | ★material · mesh | 必需 |
| `flash` | 接触点锐闪 | ★material · local_light | 必需 |
| `emission.sparks` | 沿反射方向的火星 | ★cpu_particles · gpu_particles | 必需 |
| `light` | 一帧局部光 | ★local_light | 必需 |

**节拍**：`impact` 0 s；`end` 0.15~0.4 s。

**2D/3D 投影差异**：3D 面朝入射方向；2D 面在平面，朝入射方向旋转，Sorting Order 在角色前层。

**对外接口（特有参数）**：`incomingDirection` Vector3；`plateShape` enum `sector/arc/disc`；`plateRadius` float；`sparkSpread` float。事件：入 `impact(point, incomingDir)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `shock.plate` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.sparks` | C:8 | C:20 | C:40 | G:300 | G:800 | G:2500 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`shock.plate` 面纹理与 `emission.sparks` 形态中等敏感。

---

### C09 形态切换 `transform_shift`

坐标：`S-S · T-Q · H-B`

**职责**：主体在两个形态之间切换的遮盖与过渡：先由能量 / 粒子包裹遮住主体，峰值时发出闪光（用户在此帧交换目标渲染器），随后包裹散开显出新形态。本原型不负责交换模型本身，只负责"遮 → 闪 → 散"的结构并在峰值发出 `onSwap` 事件。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `body.wrap` | 包裹体：从下向上或从中心向外的体积包裹（噪声壳 / 稠密粒子） | ★material（外扩壳阈值）· gpu_particles | 必需 |
| `emission.orbit` | 环绕包裹体旋转的粒子 | ★cpu_particles（Orbital）· gpu_particles | 可选 |
| `flash` | 峰值全遮闪光 | ★material · local_light | 必需 |
| `shock` | 散开时一道环 | material | 可选 |
| `light` | 局部光爬升到峰值再衰减 | ★local_light | 必需 |

**节拍**：`launch` 0.2~0.6 s（包裹）；`impact` = 峰值 `onSwap`（一帧到 0.1 s）；`end` 0.3~0.8 s（散开）。

**2D/3D 投影差异**：3D 包裹壳为外扩网格或包围球；2D 包裹为 Sprite 外扩或包围 quad，粒子在平面上环绕（前后 Sorting 切换）。

**对外接口（特有参数）**：`wrapDuration` float；`wrapDirection` enum；`holdDuration` float 0~0.3 s；`unwrapDuration` float；`targetRenderer`（可空）。事件：出 `onSwap`（用户在此交换模型）。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `body.wrap` | 材1 | 材2 | 材2 | 材2 | 材2 + G:1000 | 材3 + G:4000 |
| `emission.orbit` | 关 | C:20 | C:40 | C:60 | G:800 | G:2500 |
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `shock` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`body.wrap` 包裹形态与 `emission.orbit` 形态高度敏感。

---

## 7. D 物理破坏（Physical Destruction）

本类共同特征：以网格几何族为主角（预破碎 + Rigidbody、Cloth、顶点位移、程序化网格），材质族负责断面与残迹，粒子族负责尘埃与飞溅。元素在本类里多数表现为"无元素 / 纯物理"（见 `ELEMENT_CATALOG_v1.md` §4），元素只在"由元素引起的破坏"时注入（如断面发光、碎块结晶化）。

### D01 刚体破碎 `fracture_burst`

坐标：`S-M · T-I → T-D · H-B`

**职责**：一个目标（用户提供的网格，或本原型内置的基础几何体）瞬间碎裂为多块，碎块在冲量与重力下飞散、碰撞、静止、最终淡出，伴随尘埃与断面高亮。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `debris` | 碎块：预破碎网格（编辑期 Voronoi 切分基础几何 / 用户网格）+ Rigidbody | ★mesh（预破碎 + Rigidbody）· gpu_particles（网格粒子 + 简化物理）· cpu_particles（Mesh 渲染 + 碰撞模块） | 必需 |
| `edge.fracture` | 断面材质：内表面高亮 / 发光 / 不同色 | ★material（碎块材质第二通道：按面法线 / 顶点色区分断面） | 可选 |
| `emission.dust` | 破碎瞬间的尘埃云 | ★cpu_particles（少量大粒子体积噪声）· gpu_particles | 必需 |
| `emission.chips` | 细小碎屑（比 debris 小一级） | ★gpu_particles · cpu_particles | 可选 |
| `flash` | 破碎瞬闪（元素引起时才明显） | material · local_light | 可选 |
| `light` | 局部光（元素引起时） | local_light | 可选 |

**节拍**：`impact` 0 s（切换：隐藏完整体，激活碎块并施加冲量）；`travel` 0.5~3 s（物理模拟，`settleTime` 后碎块 sleep）；`end` 0.5~2 s（碎块阈值消散 / 下沉 / 缩小）。

**2D/3D 投影差异**：3D Voronoi 三维切分 + Rigidbody（Convex MeshCollider 或 Box 近似）；2D 对 Sprite 网格 / quad 做二维 Voronoi 切分 + Rigidbody2D + PolygonCollider2D，碎块可绕 Z 旋转、按 Sorting Order 分前后。

**对外接口（特有参数）**：`sourceMesh` Mesh / Sprite 引用（可空 → 内置 `primitive` enum `cube/sphere/cylinder/slab`）；`fragmentCount` int 4~200；`impulse` float；`impulseOrigin` Vector3；`upwardBias` float；`settleTime` float；`fadeMode` enum `dissolve/shrink/sink`；`fractureGlow` float 0~1。事件：入 `impact(origin, impulse)`；出 `onSettled`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `debris` | 块:8 · 无刚体（脚本抛物） | 块:16 · Box 碰撞 | 块:32 | 块:48 | 块:96 · Convex | 块:200 · Convex |
| `edge.fracture` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `emission.dust` | C:6 | C:12 | C:20 | C:24 | C:40 | G:800 |
| `emission.chips` | 关 | C:20 | G:300 | G:600 | G:2000 | G:6000 |
| `flash` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `light` | 关 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：无元素时纯物理（`debris` 材质为源材质，`emission.dust` 为尘）；有元素时 `edge.fracture`（断面发光 / 结晶 / 焦黑）、`emission.chips` 形态、`flash/light` 是否出现高度敏感。

---

### D02 结构崩塌 `collapse_sequence`

坐标：`S-M · T-Q · H-W`

**职责**：多个部件按空间顺序（自上而下 / 由一侧向另一侧 / 由支点向外）依次失稳、脱落、坠落，形成有节律的连续崩塌，落地处扬尘。与 D01 的差别：有"传播顺序"结构，部件粒度大，时间长。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `debris.parts` | 部件：N 个基础几何 / 用户网格分组，按 `propagation` 顺序激活 Rigidbody | ★mesh + Rigidbody | 必需 |
| `debris.secondary` | 部件落地二次碎裂（简化：小块粒子） | cpu_particles（Mesh）· gpu_particles | 可选 |
| `emission.dust.fall` | 每个部件脱落时的粉尘 | ★cpu_particles | 必需 |
| `emission.dust.ground` | 落地扬尘（贴地扩散） | ★cpu_particles · material（贴地噪声 quad） | 必需 |
| `edge.crack` | 崩塌前部件上的裂纹预示 | material | 可选 |
| `light` | 元素引起时 | local_light | 可选 |

**节拍**：`launch` 0.2~1 s（裂纹预示、微震）；`travel` = 传播 0.5~4 s（`propagation` + `interval` 决定各部件激活时刻，每激活出 `onPartRelease(i)`）；`impact` = 每个落地（出 `onPartLand(i)`）；`end` 1~3 s（尘埃沉降，部件淡出或保留）。

**2D/3D 投影差异**：3D 真实三维部件与 Rigidbody；2D 部件为 Sprite 网格 / quad 切片 + Rigidbody2D，落地扬尘在角色层下方。

**对外接口（特有参数）**：`parts` Mesh[] / 内置 `primitiveGrid` (Vector3Int)；`propagation` enum `topDown/leftRight/fromPoint/random`；`interval` float 0.02~0.5 s；`preShake` float；`keepDebris` bool。事件：出 `onPartRelease(i)` / `onPartLand(i)` / `onCollapsed`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `debris.parts` | 块:8 无刚体 | 块:16 | 块:32 | 块:48 | 块:96 | 块:200 |
| `debris.secondary` | 关 | 关 | C:20 | C:40 | G:800 | G:3000 |
| `emission.dust.fall` | C:6 | C:15 | C:30 | C:40 | C:80 | G:1500 |
| `emission.dust.ground` | C:6 | C:12 | C:24 | C:30 | C:60 | G:1000 |
| `edge.crack` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：同 D01。

---

### D03 布料动态 `cloth_dynamic`

坐标：`S-A（可动面）· T-S · H-K`

**职责**：一片受力可动的布（旗 / 披风 / 帷幕 / 幡），在风场与自身运动下持续摆动，可撕裂或烧蚀（元素注入时）。**Cloth 组件与顶点位移是同一原型的两种族实现**，档位决定选谁。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `cloth` | 布面：细分平面 + Cloth 组件（高档）或顶点位移波（低档） | ★mesh（Cloth）· mesh + material（顶点位移正弦叠噪声） | 必需 |
| `edge.fray` | 边缘磨损 / 撕口（材质 alpha 阈值） | material | 可选 |
| `mesh_shell.erode` | 元素侵蚀（燃烧 / 冻结 / 腐蚀阈值从一角蔓延） | material | 可选 |
| `emission.shed` | 从布面 / 侵蚀前沿脱落的粒子 | cpu_particles · gpu_particles | 可选 |
| `light` | 元素侵蚀时 | local_light | 可选 |

**节拍**：标准持续型；`impact` = `tear(point)`（撕裂：Cloth 约束断开或阈值局部挖空）或 `erode()`（侵蚀进度启动）；`end` = 侵蚀完 / 布收起。

**2D/3D 投影差异**：3D Cloth 组件 + 固定顶点约束 + 风场；2D 无 Cloth → 一律细分 quad 顶点位移（沿 X 传播的正弦 + 噪声，固定边）+ 可选 Sprite 纹理；2D 只能"摆动"不能"折叠"。

**对外接口（特有参数）**：`size` Vector2；`subdivision` int；`pinEdge` enum `top/left/topLeftCorner/twoPoints`；`windStrength` / `windDirection` / `windTurbulence`；`stiffness` float；`erodeProgress` float 0~1；`tearEnabled` bool。事件：入 `tear(point)` / `setErode(v)`；出 `onTorn` / `onEroded`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `cloth` | 顶点位移 · 8×8 | 顶点位移 · 16×16 | Cloth 低 · 16×16 | Cloth 低 · 24×24 | Cloth 中 · 32×32 | Cloth 高 · 64×64 |
| `edge.fray` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `mesh_shell.erode` | 材1 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `emission.shed` | 关 | C:10 | C:30 | C:40 | G:500 | G:2000 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：无元素时纯物理；有元素时 `mesh_shell.erode` 与 `emission.shed` 高度敏感。

---

### D04 液体飞溅 `liquid_splash`

坐标：`S-M · T-I → T-D · H-W`

**职责**：液体在一点撞击后向外飞溅：中心冠状隆起、放射状液柱、离散液滴、落地后的湿迹与涟漪。液体"是什么"由元素决定（水 / 血 / 毒 / 熔岩 / 泥）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core.crown` | 冠状隆起 + 放射液柱（程序化网格：环 + N 柱，顶点位移生长再回落） | ★mesh + material（表面高光 / 透明）· material（quad 上 SDF 冠形，2D / 低档） | 必需 |
| `emission.drops` | 离散液滴（拉伸 billboard 或球网格粒子，受重力，落地消） | ★gpu_particles（SDF 碰撞）· cpu_particles（Collision 模块） | 必需 |
| `emission.mist` | 细雾 | cpu_particles · gpu_particles | 可选 |
| `decal.wet` | 落地湿迹（扩散、寿命衰减） | ★material | 可选 |
| `shock.ripple` | 落点涟漪（若落在液面上） | material | 可选 |
| `light` | 元素发光时（熔岩 / 毒） | local_light | 可选 |

**节拍**：`impact` 0 s；`travel` 0.3~1 s（冠升落、液滴飞）；`end` 0.5~3 s（湿迹衰减）。

**2D/3D 投影差异**：3D 冠为程序化环 + 柱网格，液滴三维飞散 + 碰撞；2D 冠为极坐标 SDF quad（上半冠形），液滴在平面飞散、落到"地面线"消失并生成湿迹椭圆。

**对外接口（特有参数）**：`volume` float 0.1~5（液量，驱动冠高、滴数、湿迹大小）；`impulseDirection` Vector3；`viscosity` float 0~1（低：细碎快；高：粗大慢粘）；`wetDecalLifetime` float；`onLiquidSurface` bool。事件：入 `impact(point, dir)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core.crown` | quad SDF · 材:静 | quad SDF · 材1 | 网格 8 柱 · 材1 | 网格 12 柱 · 材2 | 网格 16 柱 · 材2 | 网格 24 柱 · 材3 顶点位移 |
| `emission.drops` | C:15 | C:40 | G:400 | G:800 | G:3000 | G:12000 |
| `emission.mist` | 关 | 关 | C:10 | C:15 | G:300 | G:1000 |
| `decal.wet` | 材:静 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `shock.ripple` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：全部层高度敏感（液体元素画像：粘度、透明度、发光、颜色、是否冒烟 / 结晶）。

---

### D05 液体流 `liquid_stream`

坐标：`S-L · T-S · H-K→H-W`

**职责**：从源点持续流出的液柱 / 液带，沿抛物线落到接触点，接触点持续飞溅并积累液面 / 湿迹。与 A04 的差别：受重力弯曲、有体积与粘度、终点是"积累"而非"命中"。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `link.stream` | 液柱：沿抛物线的程序化管 / 带网格（截面随粘度与流速变化），材质沿 U 流动 | ★mesh + material · gpu_particles（稠密粒子流，高档叠加） | 必需 |
| `emission.splash` | 接触点持续飞溅 | ★gpu_particles · cpu_particles | 必需 |
| `ground.pool` | 接触点积液（增长到上限，`end` 后衰减） | ★material（SDF 圆 + 边缘噪声 + 顶点位移波） | 可选 |
| `emission.mist` | 细雾 | cpu_particles | 可选 |
| `light` | 元素发光时 | local_light | 可选 |

**节拍**：`launch` 0.1~0.4 s（液柱前端从源到接触点）；`sustain`（流动、飞溅、积液增长）；`end` 0.2~0.8 s（源停、液柱尾端追到接触点、积液衰减）。

**2D/3D 投影差异**：3D 管网格 + 三维飞溅；2D 带状 quad 沿抛物线（宽度 = 直径），飞溅在平面，积液为地面线上的椭圆。

**对外接口（特有参数）**：`flowRate` float；`viscosity` float；`source` / `target` Transform 或 `targetPoint`；`arcGravity` float；`poolMaxRadius` float。事件：入 `setTarget(point)`；出 `onReachTarget`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `link.stream` | 带 8 段 · 材1 | 带 16 段 · 材1 | 管 16 段 · 材2 | 管 24 段 · 材2 | 管 32 段 · 材2 + G:1000 | 管 64 段 · 材3 + G:5000 |
| `emission.splash` | C:15 | C:40 | G:300 | G:600 | G:2000 | G:8000 |
| `ground.pool` | 材:静 | 材1 | 材1 | 材2 | 材2 | 材3 顶点位移 |
| `emission.mist` | 关 | 关 | C:8 | C:12 | G:200 | G:800 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：全部层高度敏感（同 D04）。

---

### D06 表面残迹 `surface_decal`

坐标：`S-A · T-D · H-W`

**职责**：贴附在表面上的长寿命残迹（燃痕 / 冻痕 / 湿迹 / 裂纹 / 腐蚀斑 / 焦黑），出现时有短暂"生成"动态（扩散 / 结晶生长），随后缓慢衰减。它是其他原型收尾层的长寿命版本，独立存在以便单独控制寿命与叠加。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `decal` | 残迹主体：SDF 形状 + 噪声边缘 + 进度阈值（生成 → 稳定 → 衰减） | ★material（URP Decal Projector 或贴表面 quad） | 必需 |
| `edge.front` | 生成期前沿（发光边 / 结晶边） | material（同材质通道） | 可选 |
| `emission.linger` | 残迹上方缓慢逸出的粒子（烟 / 冷气 / 蒸汽 / 毒雾） | ★cpu_particles | 可选 |
| `light` | 残迹发光（元素） | local_light | 可选 |

**节拍**：`impact` 0 s；`travel` = 生成 0.1~0.8 s；`sustain` = 稳定 0~∞（`lifetime`）；`end` = 衰减 0.5~5 s。

**2D/3D 投影差异**：3D URP Decal Projector 贴合任意表面（PM+）或贴地 quad（低档）；2D 椭圆 quad 在角色层下方（或墙面时在背景层上方）。

**对外接口（特有参数）**：`shape` enum `blob/ring/splat/crack/radial`；`size` Vector2；`growDuration` float；`lifetime` float（0 = 永久，直到 `end`）；`fadeDuration` float；`surfaceNormal` Vector3；`lingerRate` float。事件：标准。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `decal` | quad · 材:静 | quad · 材1 | quad · 材2 | quad · 材2 | Decal Projector · 材2 | Decal Projector · 材3 |
| `edge.front` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.linger` | 关 | C:4 | C:8 | C:10 | C:16 | C:30 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：全部层高度敏感（残迹类型即元素画像的直接投影）。

---

### D07 弹性形变 `soft_deform`

坐标：`S-S · T-I · H-B`

**职责**：主体（用户网格或内置几何）受力后的挤压 / 拉伸 / 果冻回弹（顶点位移弹簧），可伴随表面波纹。是"软体打击感"的结构，不破碎。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `mesh_shell.deform` | 目标网格的顶点位移（沿冲击方向挤压 + 弹簧回弹 + 表面正弦波） | ★material（顶点位移 shader，接收 `hitPoint/hitDir/time`）· mesh（脚本修改顶点，低档 / 2D） | 必需 |
| `flash.point` | 接触点闪 | material | 可选 |
| `emission.chips` | 少量碎屑 | cpu_particles | 可选 |
| `light` | 可选 | local_light | 可选 |

**节拍**：`impact` 0 s；`travel` 0.2~0.8 s（阻尼振荡）；`end` 0.1 s。

**2D/3D 投影差异**：3D 顶点位移沿冲击方向 + 法线；2D Sprite 网格顶点位移（需 Sprite 网格细分，Full Rect + 细分 或 quad 网格）——挤压 / 拉伸在 XY，回弹为缩放 + 剪切。

**对外接口（特有参数）**：`hitPoint` / `hitDirection`；`amplitude` float 0~0.5；`frequency` float；`damping` float；`rippleEnabled` bool；`targetRenderer`（可空）。事件：入 `impact(point, dir, strength)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `mesh_shell.deform` | 整体缩放（无顶点位移） | 顶点位移 · 低细分 | 顶点位移 · 中 | 同上 | 顶点位移 · 高 + 波纹 | 同上 |
| `flash.point` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `emission.chips` | 关 | C:6 | C:12 | C:12 | C:20 | C:30 |
| `light` | 关 | 关 | 关 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：低；`emission.chips` 与 `flash.point` 仅色板。本原型主要属于"无元素 / 纯物理"类。

---

### D08 地面裂陷 `ground_rupture`

坐标：`S-A · T-Q · H-W`

**职责**：地面自一点向外开裂：裂纹沿路径生长（SDF 路径阈值）、裂缝内发光 / 涌出、裂缝两侧地块隆起或下陷（顶点位移 / 块几何）、扬尘。是"大地类"打击与范围的地面结构。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `decal.crack` | 裂纹：程序化分叉路径 SDF，阈值沿路径生长 | ★material（贴地 quad / Decal Projector） | 必需 |
| `edge.glow` | 裂缝内发光 / 涌出（元素） | material（同材质通道） | 可选 |
| `debris.slabs` | 裂缝两侧隆起 / 下陷的地块（程序化块几何，顶点位移或刚体） | ★mesh + material | 可选 |
| `emission.dust` | 沿裂纹扬起的尘 | ★cpu_particles · gpu_particles | 必需 |
| `emission.vent` | 裂缝内涌出的粒子（元素） | gpu_particles · cpu_particles | 可选 |
| `light` | 裂缝发光（元素） | local_light | 可选 |

**节拍**：`impact` 0 s（起裂点）；`travel` 0.2~1 s（裂纹生长，`crackLength/branches`）；`sustain` 0~5 s（裂缝内持续发光 / 涌出）；`end` 0.5~3 s（裂纹愈合 / 淡出，地块回落）。

**2D/3D 投影差异**：3D 贴地 Decal + 真实地块几何隆起；2D 裂纹为地面线附近的压扁 quad，地块为 Sprite 网格切片上下位移。

**对外接口（特有参数）**：`crackLength` float；`branches` int 0~8；`crackWidth` float；`slabCount` int；`slabHeight` float（正隆起负下陷）；`ventEnabled` bool；`healOnEnd` bool。事件：出 `onCrackComplete`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `decal.crack` | quad · 材1 | quad · 材1 | quad · 材2 | quad · 材2 | Decal · 材2 | Decal · 材3 |
| `edge.glow` | 关 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `debris.slabs` | 关 | 块:4 位移 | 块:8 | 块:12 | 块:24 刚体 | 块:48 刚体 |
| `emission.dust` | C:8 | C:20 | C:40 | C:60 | G:1000 | G:4000 |
| `emission.vent` | 关 | 关 | C:20 | G:300 | G:1000 | G:4000 |
| `light` | 关 | 光:1 | 光:1 | 光:1 | 光:2 | 光:3 |

**元素敏感度**：`edge.glow`、`emission.vent`、`light` 完全由元素决定（无元素时全关，纯物理裂陷）；`decal.crack` 分叉风格中等敏感。

---

## 8. E 环境氛围（Environment & Atmosphere）

本类共同特征：世界坐标附着（H-W），时间形态持续（T-S），多为大范围、低对比、长寿命；对 overdraw 与粒子数最敏感，因此降级表最陡。节拍统一 `launch`（0.5~3 s 渐入）→ `sustain`（循环）→ `end`（0.5~3 s 渐出）；各节只写差异。

### E01 降落天气粒子 `weather_precip`

坐标：`S-V · T-S · H-W`

**职责**：在一个盒 / 柱状体积内持续降落的粒子（细长条 / 片状 / 灰屑 / 发光屑等形态由元素决定），落地后有接触反馈（溅 / 积），跟随一个中心（通常是相机或主体）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `emission.fall` | 降落主体（拉伸 billboard / quad / 小网格），受风场 | ★gpu_particles · cpu_particles | 必需 |
| `emission.fall.near` | 近景大颗粒（少量，增强景深） | cpu_particles | 可选 |
| `emission.splash` | 落地接触（小溅 / 小环 / 停留） | ★cpu_particles · gpu_particles（地面高度 / SDF 碰撞） | 可选 |
| `veil.far` | 远景雨幕 / 雪幕（平面 UV 滚动噪声） | material（大 quad） | 可选 |
| `light` | 无（天气本身不发光；发光元素的降落屑可有低强度 1 盏） | local_light | 可选 |

**节拍**：标准持续型；`impact` = `gust`（一阵风：短时改变风向与密度）。

**2D/3D 投影差异**：3D 盒体积随中心移动，粒子有深度，`veil.far` 可选；2D 粒子在多层 Sorting（前 / 中 / 后）+ 尺寸 / 速度分层模拟视差，`emission.splash` 在"地面线"。

**对外接口（特有参数）**：`volumeSize` Vector3；`density` float 0~1；`fallSpeed` float；`windDirection` / `windStrength`；`particleShape` enum `streak/flake/ember/petal/ash`（也可由元素预设覆盖）；`groundHeight` float；`splashEnabled` bool；`followTarget` Transform（可空）。事件：入 `gust(strength, duration)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `emission.fall` | C:40 | C:100 | G:1500 | G:3000 | G:15000 | G:60000 |
| `emission.fall.near` | 关 | 关 | C:20 | C:30 | C:60 | C:100 |
| `emission.splash` | 关 | C:20 | C:60 | G:500 | G:2000 | G:8000 |
| `veil.far` | 关 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `light` | 关 | 关 | 关 | 光:1 | 光:1 | 光:1 |

**元素敏感度**：`emission.fall` 粒子形态与运动（直落 / 飘落 / 上飘反向 / 斜飞）与 `emission.splash` 高度敏感。

---

### E02 空气粒子流 `ambient_airflow`

坐标：`S-V · T-S · H-W`

**职责**：体积内缓慢漂浮 / 流动的悬浮粒子（尘埃 / 孢子 / 萤火 / 灰烬 / 气泡 / 雪雾），带噪声湍流，无明显重力，营造空气感。与 E01 的差别：无降落主方向、无落地。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `emission.drift` | 漂浮粒子（Curl 噪声场） | ★gpu_particles（湍流场）· cpu_particles（Noise 模块） | 必需 |
| `emission.drift.glow` | 少量会发光 / 闪烁的粒子（萤火类） | cpu_particles | 可选 |
| `veil.rays` | 光柱 / 体积光线束（斜向平面 quad 组 + 噪声） | material（quad 组） | 可选 |
| `light` | 无或极弱 | local_light | 可选 |

**节拍**：标准持续型；`impact` = `disturb(point, radius)`（局部扰动：粒子被推开）。

**2D/3D 投影差异**：3D 三维 Curl 场；2D 二维 Curl 场 + 多 Sorting 层视差；`veil.rays` 3D 为斜向平面组，2D 为斜向 quad 排在背景之上角色之下。

**对外接口（特有参数）**：`volumeSize`；`density`；`driftSpeed`；`turbulence`；`glowFraction` float 0~1；`raysEnabled` bool；`followTarget`。事件：入 `disturb(point, radius, strength)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `emission.drift` | C:30 | C:80 | G:1000 | G:2000 | G:10000 | G:40000 |
| `emission.drift.glow` | 关 | C:10 | C:20 | C:30 | C:60 | C:100 |
| `veil.rays` | 关 | 关 | 材1 · 2 片 | 材1 · 4 片 | 材2 · 6 片 | 材2 · 8 片 |
| `light` | 关 | 关 | 关 | 关 | 光:1 | 光:1 |

**元素敏感度**：`emission.drift` 粒子形态与运动倾向高度敏感。

---

### E03 雾烟云体 `fog_volume`

坐标：`S-V · T-S · H-W`

**职责**：占据一片空间的半透明体积（地面雾 / 烟团 / 云 / 毒气 / 寒气），边缘柔和、内部有慢速噪声翻腾，可被局部扰动。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `body.volume` | 体积主体：多张大 quad 的层叠体积噪声（低档）/ 球壳视线方向 ray-march 近似（高档）/ 大粒子云 | ★material（层叠 quad 体积噪声 + 深度淡出）· cpu_particles（少量大粒子）· gpu_particles | 必需 |
| `body.detail` | 细节翻腾层（小尺度噪声，速度不同） | material | 可选 |
| `edge.wisps` | 边缘拉丝（少量粒子沿边缘游走） | cpu_particles | 可选 |
| `light` | 体积内部光（有光源元素时） | local_light | 可选 |

**节拍**：标准持续型；`impact` = `disturb(point, radius)`（局部推开）。

**2D/3D 投影差异**：3D 层叠 quad 面向相机 + 软深度（依赖 URP 深度纹理时降为不依赖的球壳法线淡出）；2D 多张大 quad 平面噪声，按 Sorting 分前后层，角色位于中间层。

**对外接口（特有参数）**：`volumeSize`；`density` float；`noiseScale`；`flowDirection` / `flowSpeed`；`edgeSoftness`；`groundHugging` bool（贴地雾：高度衰减）。事件：入 `disturb(...)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `body.volume` | 2 quad · 材1 | 3 quad · 材2 | 4 quad · 材2 | 4 quad · 材2 | 6 quad · 材3 或 C:24 大粒子 | 8 quad · 材3 + G:2000 |
| `body.detail` | 关 | 关 | 材1 | 材1 | 材1 | 材2 |
| `edge.wisps` | 关 | 关 | C:8 | C:12 | C:24 | C:40 |
| `light` | 关 | 关 | 关 | 光:1 | 光:1 | 光:2 |

**元素敏感度**：`body.volume` 噪声形态（翻腾 / 沉降 / 结晶雾 / 电离雾）与流动方向高度敏感。

---

### E04 持续表面场 `surface_field`

坐标：`S-A（大面）· T-S · H-W`

**职责**：大面积可动表面（水面 / 熔岩面 / 毒沼 / 能量池 / 沙面），顶点位移波 + 表面材质流动 + 边缘接触反馈 + 表面逸出粒子。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `surface` | 表面：细分平面网格 + 顶点位移波（Gerstner / 噪声）+ 材质（流动、反射近似、发光） | ★mesh + material | 必需 |
| `edge.shore` | 边缘接触（岸线泡沫 / 结壳 / 亮边）——按世界高度或 SDF 边缘 | material（同材质通道） | 可选 |
| `emission.surface` | 表面逸出粒子（气泡 / 火星 / 蒸汽 / 毒雾） | ★cpu_particles · gpu_particles | 可选 |
| `shock.ripple` | 外部触发的涟漪（材质接收 ≤ 8 个涟漪源） | material | 可选 |
| `light` | 面下 / 面上局部光（发光表面） | local_light | 可选 |

**节拍**：标准持续型；`impact` = `ripple(point, strength)`。

**2D/3D 投影差异**：3D 细分平面 + 顶点位移 + 法线重建；2D 为一条"表面带" quad（横向）+ 顶点位移上下起伏 + 平面流动噪声，涟漪为沿 X 的一维波。

**对外接口（特有参数）**：`size` Vector2；`subdivision` int；`waveAmplitude` / `waveFrequency` / `waveSpeed`；`flowDirection`；`viscosity`（波形尖锐度）；`emissive` float。事件：入 `ripple(point, strength)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `surface` | 16×16 · 材1（无顶点位移，UV 流动） | 32×32 · 材2 | 64×64 · 材2 顶点位移 | 同上 | 128×128 · 材3 | 256×256 · 材3 |
| `edge.shore` | 关 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.surface` | 关 | C:15 | C:40 | C:60 | G:1000 | G:4000 |
| `shock.ripple` | 关 | 2 源 | 4 源 | 4 源 | 8 源 | 8 源 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:2 | 光:3 |

**元素敏感度**：全部层高度敏感（表面元素画像即本原型的全部外观）。

---

### E05 喷发源 `vent_emitter`

坐标：`S-P→S-L · T-S · H-W`

**职责**：一个固定点持续或间歇地向一个方向喷出物质（气态 / 液态 / 能量态由元素决定），带喷口本体与喷出流。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `emission.jet` | 喷出流（方向性锥形发射，速度衰减，膨胀） | ★gpu_particles · cpu_particles | 必需 |
| `core.nozzle` | 喷口本体发光 / 热变形 | material · mesh | 可选 |
| `body.plume` | 喷出流的连续体积近似（材质柱体，与粒子叠加） | mesh + material | 可选 |
| `light` | 喷口局部光随喷发脉冲 | ★local_light | 必需（有光元素时） |

**节拍**：标准持续型；`sustain` 中按 `mode` 连续或间歇（`interval/burstDuration`），每次喷发出 `onBurst`。

**2D/3D 投影差异**：3D 三维锥发射；2D 平面扇形发射，`body.plume` 为竖直 / 定向 quad。

**对外接口（特有参数）**：`direction` Vector3；`coneAngle` float；`jetSpeed` float；`jetLength` float；`mode` enum `continuous/intermittent`；`interval` / `burstDuration` float。事件：出 `onBurst`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `emission.jet` | C:20 | C:60 | G:600 | G:1200 | G:5000 | G:20000 |
| `core.nozzle` | 关 | 材:静 | 材1 | 材1 | 材2 | 材2 |
| `body.plume` | 材1 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 | 光:1 阴 |

**元素敏感度**：`emission.jet` 与 `body.plume` 形态高度敏感。

---

### E06 传送门 `portal_gate`

坐标：`S-A（竖直面）+ S-S（环）· T-S · H-W`

**职责**：一个竖直的可穿越面：外环几何（旋转 / 呼吸）+ 内部面（旋涡 / 深度错觉 / 流动）+ 环缘逸出粒子 + 前方地面投光。开 / 关有明确节拍，穿越时有脉冲。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `mesh_shell.rim` | 外环：环面网格（或程序化多段环）+ 材质流动 | ★mesh + material | 必需 |
| `core.surface` | 内部面：极坐标旋涡 + 视差深度错觉（多层 UV 偏移） | ★material（quad） | 必需 |
| `emission.rim` | 环缘逸出 / 被吸入的粒子 | ★cpu_particles · gpu_particles | 可选 |
| `ground.cast` | 门前地面投光 / 阵 | material | 可选 |
| `light` | 门中心局部光（Spot 朝前 + Point） | ★local_light | 必需 |

**节拍**：`launch` 0.3~1.5 s（从一点撕开到满径，`openStyle`）；`sustain`（旋转、呼吸）；`impact` = `traverse`（穿越脉冲：内部面亮 + 环缩放弹一下 + 粒子爆发）；`end` 0.3~1 s（收缩闭合）。

**2D/3D 投影差异**：3D 环面 + 竖直 quad，可从侧面看到是"薄面"（可选双面）；2D 椭圆环（透视暗示）或正圆，内部面正对相机。

**对外接口（特有参数）**：`radius` float；`openStyle` enum `irisOpen/tearVertical/growFromCenter`；`openProgress` float 0~1（外部驱动）；`swirlSpeed` float；`depthLayers` int 1~4；`rimSegments` int。事件：入 `traverse()` / `setOpen(v)`；出 `onOpened` / `onClosed` / `onTraverse`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `mesh_shell.rim` | 极坐标 quad 环 · 材1 | 同上 · 材2 | 环面 16 段 · 材2 | 环面 24 段 · 材2 | 环面 48 段 · 材3 | 环面 64 段 · 材3 顶点位移 |
| `core.surface` | 材1 · 1 层深度 | 材2 · 2 层 | 材2 · 2 层 | 材2 · 3 层 | 材3 · 3 层 | 材3 · 4 层 |
| `emission.rim` | 关 | C:20 | C:50 | C:60 | G:800 | G:3000 |
| `ground.cast` | 关 | 材:静 | 材1 | 材1 | 材1 | 材2 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:2 | 光:2 | 光:2 阴 |

**元素敏感度**：`core.surface` 旋涡形态、`mesh_shell.rim` 环几何族、`emission.rim` 形态高度敏感。

---

### E07 世界过渡幕 `sweep_curtain`

坐标：`S-A（大面）· T-T · H-W`

**职责**：世界空间中一道大面积的"幕"或"波"横扫过场景（能量墙推进、雾墙压来、冲击面掠过），前沿有细节，扫过后留下短暂痕迹。**这是世界空间几何，不是屏幕空间后处理**。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `veil` | 幕主体：大平面 / 大弧面网格 + 材质（前沿阈值 + 噪声 + 深度淡出） | ★mesh + material | 必需 |
| `edge.front` | 前沿亮边 / 波峰 | material（同材质通道） | 必需 |
| `emission.front` | 前沿卷起的粒子（发射体随幕移动） | ★gpu_particles · cpu_particles | 可选 |
| `decal.wake` | 扫过后的地面痕迹（短寿命） | material | 可选 |
| `light` | 前沿局部光随幕移动 | local_light | 可选 |

**节拍**：`launch` 0.1~0.5 s（幕出现）；`travel` 0.5~5 s（`sweepDistance/sweepDuration`）；`end` 0.3~1 s（幕淡出）。

**2D/3D 投影差异**：3D 大竖直面 / 弧面沿方向推进；2D 一条竖直 quad 横扫屏幕世界区域（Sorting 在角色前或后可选）。

**对外接口（特有参数）**：`curtainSize` Vector2；`curvature` float 0~1；`sweepDirection` Vector3；`sweepDistance` / `sweepDuration`；`frontThickness`；`wakeLifetime`。事件：标准 + 入 `setProgress(t)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `veil` | 平面 · 材1 | 平面 · 材2 | 弧面 16 段 · 材2 | 同上 | 弧面 32 段 · 材3 | 弧面 64 段 · 材3 顶点位移 |
| `edge.front` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `emission.front` | 关 | C:30 | G:400 | G:800 | G:3000 | G:10000 |
| `decal.wake` | 关 | 关 | 材1 | 材1 | 材1 | 材2 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:1 | 光:2 |

**元素敏感度**：`veil` 与 `edge.front` 形态高度敏感。

---

### E08 屏障墙 `barrier_wall`

坐标：`S-A（竖直面）· T-S · H-W`

**职责**：持续存在的竖直（或穹顶）屏障面：半透明流动材质、边缘接地环、受击点涟漪、可开裂 / 破碎。是 B04 护盾的"世界固定、大面积"版本，结构不同：不跟随主体，几何是墙 / 穹顶 / 圆柱段，有接地边。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `veil` | 屏障面：平面 / 弧面 / 圆柱段 / 穹顶网格 + 材质（六边 / 噪声 / 流动 + 菲涅尔近似） | ★mesh + material | 必需 |
| `ground.base` | 接地边：地面接触环 / 线 | ★material | 必需 |
| `flash.hit` | 受击涟漪（≤ 4 点） | material | 可选 |
| `mesh_shell.crack` | 裂纹（`integrity`） | material | 可选 |
| `debris` | 破碎（可选） | mesh · gpu_particles | 可选 |
| `light` | 沿墙 1~3 盏 | local_light | 可选 |

**节拍**：`launch` 0.2~1 s（从地面升起 / 从中心展开）；`sustain`；`impact` = `hitAt` / `break`；`end` 0.2~0.8 s（沉入地面 / 消散）。

**2D/3D 投影差异**：3D 真实弧面 / 穹顶；2D 一片竖直 quad（可有轻微透视梯形），接地边为地面线上的椭圆环。

**对外接口（特有参数）**：`wallShape` enum `plane/arc/cylinder/dome`；`wallSize` Vector3；`riseStyle` enum `fromGround/fromCenter`；`integrity` float；`maxSimultaneousHits` int。事件：同 B04。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `veil` | 平面 · 材1 | 平面 · 材2 | 弧面 · 材2 | 同上 | 穹顶 · 材3 | 穹顶 · 材3 顶点位移 |
| `ground.base` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `flash.hit` | 1 点 | 2 点 | 4 点 | 4 点 | 4 点 | 4 点 |
| `mesh_shell.crack` | 关 | 材:静 | 材1 | 材1 | 材2 | 材2 |
| `debris` | 关 | 块:8 | 块:16 | 块:24 | 块:48 | 块:96 |
| `light` | 关 | 关 | 光:1 | 光:1 | 光:2 | 光:3 |

**元素敏感度**：`veil` 表面纹理与 `debris` 形态高度敏感。

---

### E09 光源体 `light_body`

坐标：`S-P · T-S · H-W / H-K`

**职责**：一个自身就是光源的持续体（核心几何为球 / 柱 / 簇 / 舌形，由元素决定），核心形态 + 包裹 + 逸出粒子 + **以局部光为主角**（有闪烁 / 呼吸模式）。它是环境里"光从哪里来"的原型，也是局部光节拍器的主要试验场。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `core` | 发光核心（球 / 柱 / 晶 / 火舌形 SDF） | ★material · mesh | 必需 |
| `body` | 包裹（体积噪声 / 热浪 / 光晕） | material | 可选 |
| `emission.ambient` | 逸出粒子（火星 / 光尘 / 电弧） | ★cpu_particles · gpu_particles | 可选 |
| `ground.cast` | 地面投光近似（低档替代真实光） | material | 可选 |
| `light` | 局部光：闪烁 / 呼吸 / 稳定模式 + 色温 | ★local_light | 必需 |

**节拍**：标准持续型；`impact` = `flare`（短促增亮）；`light` 按 `flickerMode` 运行。

**2D/3D 投影差异**：3D Point Light（可带阴影 PM+）+ 网格核心；2D Light2D Point（可 Sprite Light 形状）+ quad 核心；2D 的 `ground.cast` 在角色层下方。

**对外接口（特有参数）**：`coreShape` enum；`coreRadius`；`lightRange`；`flickerMode` enum `steady/breathe/flicker/pulse/strobe`；`flickerRate` float；`flickerDepth` float 0~1；`castShadows` bool（档位截断）。事件：入 `flare(strength)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `core` | 材1 | 材2 | 材2 | 材2 | 材3 | 材3 |
| `body` | 关 | 材1 | 材1 | 材2 | 材2 | 材3 |
| `emission.ambient` | C:8 | C:20 | C:40 | C:60 | G:600 | G:2000 |
| `ground.cast` | 材:静（替代光） | 材1 | 关（有真实光） | 关 | 关 | 关 |
| `light` | 光:烘 | 光:1 | 光:1 | 光:1 | 光:1 阴 | 光:1 阴 |

**元素敏感度**：`core` 形态、`emission.ambient`、`light` 色温与闪烁模式高度敏感。

---

## 9. F UI 内特效（In-UGUI Effects）

**边界（ADR-010 §3）**：本类只提供 **UGUI 内的特效材质与粒子**，附着在用户给定的 RectTransform 上；**不改 UI 本身**——不做面板位移 / 缩放 / 透明度动画、不做布局、不做文字。实现载体：UGUI `Graphic` 自定义材质（Shader Graph UI 目标）、Canvas 空间 ParticleSystem（Canvas 为 Screen Space-Camera 或 World Space 时直接可用；Overlay 时由 T2b 决定 Canvas 渲染纹理还是限制为 Camera 模式——**未决**，见报告）。UI 内无局部光（Light2D 不影响 UGUI）——本类的 `light` 职责由材质发光承担。维度：UI 天然为 2D；"3D"列表示 World Space Canvas 下的差异。

### F01 按钮反馈 `ui_button_feedback`

坐标：`S-A（矩形）· T-I · H-U`

**职责**：按下 / 悬停时按钮矩形内的材质反馈：从触点扩散的圆形波、边缘流光一闪、少量粒子迸出。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `fill.ripple` | 从触点向外的圆形波（SDF 圆随时间扩张 + 淡出） | ★material（Graphic 材质，接收触点 UV） | 必需 |
| `frame` | 矩形 / 圆角矩形边框流光一闪（SDF 圆角矩形边） | ★material | 可选 |
| `emission.pop` | 触点迸出少量粒子 | cpu_particles（Canvas 空间） | 可选 |

**节拍**：`impact` 0 s；`end` 0.15~0.4 s。悬停模式 `sustain` 循环（边框缓慢流光）。

**投影差异（Overlay / Camera / World）**：World Space Canvas 下粒子有深度可穿过其他 UI；Overlay 下粒子需 Camera 模式（未决）。

**对外接口（特有参数）**：`touchPoint` Vector2（UV）；`rippleMaxRadius` float（UV 单位）；`cornerRadius` float；`mode` enum `press/hover`。事件：入 `impact(uv)`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `fill.ripple` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `frame` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |
| `emission.pop` | 关 | C:6 | C:10 | C:12 | C:16 | C:24 |

**元素敏感度**：低（色板 + 波边缘形态）。

---

### F02 持续流光 `ui_shine_loop`

坐标：`S-A · T-S · H-U`

**职责**：矩形 / 图标区域内周期性扫过的高光带、或沿边框循环流动的光，表示"可用 / 稀有 / 新"。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `fill.sweep` | 斜向高光带按周期扫过（可遮罩到图标 alpha） | ★material | 必需 |
| `frame.flow` | 边框流光（沿 SDF 边的 U 参数流动） | material | 可选 |
| `emission.sparkle` | 区域内偶发闪点 | cpu_particles · ★material（程序化闪点阈值噪声） | 可选 |

**节拍**：标准持续型；`sweepInterval` 决定扫光周期。

**对外接口（特有参数）**：`sweepInterval` float 0.5~5 s；`sweepAngle` float；`sweepWidth` float；`maskToAlpha` bool；`sparkleDensity` float。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `fill.sweep` | 材1 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `frame.flow` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.sparkle` | 关 | 材:噪 | 材:噪 | 材:噪 | 材:噪 + C:8 | 材:噪 + C:16 |

**元素敏感度**：低。

---

### F03 奖励爆发 `ui_reward_burst`

坐标：`S-P→S-M · T-I · H-U`

**职责**：在一个 UI 点上爆发：放射光芒、扩散环、迸出并下落的粒子（纸屑 / 光点 / 碎片），短暂残留光晕。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `flash` | 中心闪光（多角星 SDF） | ★material | 必需 |
| `emission.rays` | 放射光芒（程序化 N 条 quad 或极坐标材质光芒旋转） | ★material（极坐标光芒 + 旋转）· mesh | 可选 |
| `shock` | 扩散环 | material | 可选 |
| `emission.confetti` | 迸出下落粒子（受 Canvas 空间重力） | ★cpu_particles | 必需 |
| `body.glow` | 残留光晕（慢淡） | material | 可选 |

**节拍**：`impact` 0 s；峰 0.1 s；`end` 0.6~1.5 s（粒子落尽）。

**对外接口（特有参数）**：`rayCount` int；`confettiCount` int；`confettiShape` enum `square/strip/dot/star`；`gravity` float；`glowHold` float。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.rays` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `shock` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.confetti` | C:12 | C:30 | C:60 | C:80 | C:150 | C:300 |
| `body.glow` | 关 | 材:静 | 材1 | 材1 | 材1 | 材1 |

**元素敏感度**：中（粒子形态、光芒形态）。

---

### F04 揭示 `ui_reveal`

坐标：`S-A · T-Q · H-U`

**职责**：一个矩形内容（卡面 / 图标 / 结果）由遮盖到显露的过程：预示（边框呼吸 / 内部流动）→ 揭示（阈值扫过 / 翻转闪 / 碎裂）→ 定格（边框亮 + 光芒）。卡片翻转本身若涉及 RectTransform 旋转属 UI 布局——本原型只做材质层的"翻转闪"（透视 UV 扭曲在 shader 内），不动 RectTransform。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `fill.cover` | 遮盖层（流动噪声 / 云雾 / 能量），预示期呼吸 | ★material | 必需 |
| `fill.reveal` | 揭示阈值（方向扫 / 噪声溶 / 中心开 / shader 内透视翻转闪） | ★material | 必需 |
| `frame` | 边框：预示呼吸 → 定格亮 | ★material | 可选 |
| `emission.burst` | 揭示瞬间粒子 | cpu_particles | 可选 |
| `emission.rays` | 定格光芒（稀有度驱动） | material | 可选 |

**节拍**：`launch` 0.3~2 s（预示，可外部延长）；`impact` = 揭示 0.2~0.6 s；`end` 0.3~1 s（定格光芒淡出，边框保持由 `holdFrame`）。

**对外接口（特有参数）**：`revealStyle` enum `sweep/dissolve/irisOpen/flipFlash/shatter`；`revealProgress` float（外部驱动）；`rarityTier` int 0~5（驱动光芒 / 粒子 / 边框强度）；`holdFrame` bool。事件：入 `reveal()` / `setProgress(v)`；出 `onRevealed`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `fill.cover` | 材1 | 材2 | 材2 | 材2 | 材2 | 材3 |
| `fill.reveal` | 材1 | 材1 | 材2 | 材2 | 材2 | 材2 |
| `frame` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `emission.burst` | 关 | C:15 | C:30 | C:40 | C:80 | C:150 |
| `emission.rays` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |

**元素敏感度**：中（遮盖层与揭示阈值形态）。

---

### F05 合成融合 `ui_merge_fuse`

坐标：`S-M→S-P · T-Q · H-U`

**职责**：多个 UI 元素位置上的材质 / 粒子向一个中心汇聚、融合闪光、产出新体的过程。**元素本体的位移属 UI 布局，本原型只做每个源位置的"抽离流"（粒子 / 光带从源飞向中心）与中心融合层**。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `link.stream` | 源→中心的光带 / 粒子流（N 条） | ★cpu_particles（目标吸引）· material（Bezier 带 quad） | 必需 |
| `core.fuse` | 中心融合核：随到达增大、旋转、闪烁 | ★material | 必需 |
| `flash` | 融合峰值闪 | ★material | 必需 |
| `shock` | 峰值扩散环 | material | 可选 |
| `emission.burst` | 峰值粒子 | cpu_particles | 可选 |

**节拍**：`launch` 0.2~0.6 s（抽离流启动）；`travel` 0.3~1 s（汇聚）；`impact` = 融合峰值（出 `onFused`）；`end` 0.3~0.8 s。

**对外接口（特有参数）**：`sourcePoints` Vector2[]（Canvas 局部）；`targetPoint` Vector2；`streamDuration`；`stagger`；`fuseGrowth` float。事件：出 `onFused`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `link.stream` | C:6 / 源 | C:12 / 源 | C:24 / 源 | C:30 / 源 | C:50 / 源 + 材带 | C:80 / 源 + 材带 |
| `core.fuse` | 材1 | 材1 | 材2 | 材2 | 材2 | 材3 |
| `flash` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材2 |
| `shock` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.burst` | 关 | C:12 | C:24 | C:30 | C:60 | C:100 |

**元素敏感度**：中。

---

### F06 HUD 脉冲 `ui_hud_pulse`

坐标：`S-A（边框 / 屏幕边）· T-I / T-S · H-U`

**职责**：HUD 元素（图标 / 屏幕边缘矩形区域）的提示脉冲：边框呼吸 / 闪烁、区域内向内或向外的渐变脉冲、可循环（危险 / 低资源）或单次（提示）。屏幕边缘红晕若作为全屏后处理是资产外；作为**用户放置的全屏 RectTransform 上的 Graphic 材质**则属本原型（用户负责放置）。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `frame.pulse` | 边框脉冲（SDF 边宽与亮度随时间） | ★material | 必需 |
| `fill.gradient` | 区域内渐变脉冲（向内 vignette 形 / 向外） | material | 可选 |
| `emission.tick` | 脉冲峰值时的少量粒子 | cpu_particles | 可选 |

**节拍**：单次 `impact`→`end` 0.3~0.8 s；循环 `sustain`（`pulseRate`）。

**对外接口（特有参数）**：`mode` enum `once/loop`；`pulseRate` float；`edgeWidth` float；`gradientDirection` enum `inward/outward`；`urgency` float 0~1（驱动频率与饱和）。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `frame.pulse` | 材1 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `fill.gradient` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.tick` | 关 | 关 | C:8 | C:8 | C:12 | C:20 |

**元素敏感度**：低。

---

### F07 能量条 `ui_bar_energy`

坐标：`S-A（条形）· T-S · H-U`

**职责**：条形填充区域的材质动态：填充部分内部流动、填充前沿亮边与粒子、填充变化时的"追赶"残影带（在 shader 内用 `value/previousValue` 两值实现，不改 RectTransform）、满 / 空状态的特殊脉冲。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `fill.body` | 填充区域内流动材质（按 `value` 阈值裁切） | ★material | 必需 |
| `edge.front` | 填充前沿亮边 | ★material（同材质通道） | 必需 |
| `fill.lag` | 变化残影带（`previousValue`→`value` 之间的淡色带，按时间追赶） | material（同材质通道） | 可选 |
| `emission.front` | 前沿粒子（随 `value` 位置移动） | cpu_particles | 可选 |
| `frame.state` | 满 / 空 / 危险状态边框脉冲 | material | 可选 |

**节拍**：标准持续型；`impact` = `valueChanged`（残影带启动、前沿闪）。

**对外接口（特有参数）**：`value` float 0~1；`lagDuration` float；`direction` enum `leftRight/rightLeft/bottomUp/radial`；`flowSpeed`；`lowThreshold` float（低于此进入危险脉冲）。事件：入 `setValue(v)`；出 `onFull` / `onEmpty` / `onLow`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `fill.body` | 材:静 | 材1 | 材1 | 材1 | 材2 | 材2 |
| `edge.front` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `fill.lag` | 材:静 | 材:静 | 材:静 | 材:静 | 材:静 | 材:静 |
| `emission.front` | 关 | 关 | C:8 | C:8 | C:16 | C:24 |
| `frame.state` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |

**元素敏感度**：中（填充流动形态）。

---

### F08 收集飞行 `ui_collect_fly`

坐标：`S-M · T-T · H-U`

**职责**：N 个小体从 Canvas 上一处（或世界点投影到 Canvas）沿弧线飞向目标 UI 点，带尾迹，到达时目标处小闪并出计数事件。是 C05 的 UI 内版本。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `orbit.body` | 飞行小体（N 个） | ★cpu_particles（目标吸引 + 初始爆散）· material（N 个 Graphic 沿 Bezier——需 N 个 RectTransform，**属 UI 布局，禁止**；因此只用粒子） | 必需 |
| `trail` | 小体尾迹 | cpu_particles Trails | 可选 |
| `flash.arrive` | 目标处到达闪 | ★material | 必需 |

**节拍**：`launch` 0~0.3 s（爆散悬浮）；`travel` 0.3~1 s；`impact` = 每个到达（`onArrive(index)`）；`end` 0.1~0.3 s。

**对外接口（特有参数）**：`count`；`sourcePoint` / `targetPoint` Vector2；`scatterRadius`；`travelDuration`；`stagger`；`pathCurvature`。事件：出 `onArrive(index)` / `onAllArrived`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `orbit.body` | C:8 | C:20 | C:40 | C:50 | C:100 | C:200 |
| `trail` | 关 | C Trails 短 | C Trails | 同上 | 同上 | 同上 |
| `flash.arrive` | 材:静 | 材1 | 材1 | 材1 | 材1 | 材1 |

**元素敏感度**：中（小体形态与尾迹）。

---

### F09 面板入场 `ui_panel_enter`

坐标：`S-A（面板矩形）· T-I · H-U`

**职责**：面板出现 / 消失时**材质层**的入场效果：面板 Graphic 材质上的阈值扫入 / 溶入 / 扫描线揭示 + 边框流光一圈 + 少量边缘粒子。**面板自身的位移 / 缩放 / 透明度动画属 UI 布局，不做**；本原型只在用户已经放好的面板 Graphic 上做材质入场。

**层结构**

| 层 | 视觉职责 | 技术族（★首选） | 必需 |
|---|---|---|---|
| `fill.reveal` | 面板材质阈值入场（方向扫 / 噪声溶 / 扫描线 / 网格拼合） | ★material | 必需 |
| `frame.trace` | 边框沿周长流光一圈（SDF 边 U 参数） | ★material | 可选 |
| `emission.edge` | 前沿粒子 | cpu_particles | 可选 |

**节拍**：`launch`＝`impact` 0 s；`travel` 0.2~0.6 s（阈值推进）；`end` 0.1~0.3 s。退场 = 反向。

**对外接口（特有参数）**：`revealStyle` enum `sweep/dissolve/scanline/gridAssemble`；`direction` enum；`duration`；`reverse` bool（退场）。事件：入 `enter()` / `exit()`；出 `onEntered` / `onExited`。

**六档降级**

| 层 | ML | MM | MH | PL | PM | PH |
|---|---|---|---|---|---|---|
| `fill.reveal` | 材1 | 材1 | 材2 | 材2 | 材2 | 材2 |
| `frame.trace` | 关 | 材1 | 材1 | 材1 | 材1 | 材1 |
| `emission.edge` | 关 | 关 | C:12 | C:16 | C:30 | C:50 |

**元素敏感度**：低~中（阈值形态）。

---

## 10. G 编排级（Orchestration）

编排级不是一个原型，也不逐个枚举组合；它是**一套把多个原型实例按时间线组合成一个自包含预制体**的规则。ADR-010 §3 将其列为第七大类。

### 10.1 定义

一个编排（orchestration）= 一组**子原型实例**（每个是本目录中的一个原型 + 元素 + 风格 + 维度，共享档位）+ 一条**时间线**（何时对哪个子实例发什么入事件）+ **事件接线**（某个子实例的出事件触发另一个子实例的入事件）+ **对外聚合接口**（编排自身暴露的事件与参数，映射到子实例）。编排的产物仍是一个自包含预制体。

### 10.2 组合规则

1. **子实例数上限**：v1 ≤ 8 个子实例（成本模型按档位累加，任一档位超预算即编译失败）。
2. **同维度同风格同档位**：一个编排内所有子实例的 `dimension` / `style` / `performanceTier` 必须一致；`element` 可以不同（允许多元素组合）。
3. **时间线条目**：`{ t, target: <子实例 id>, event: <入事件名>, payload? }`，`t` 相对编排 `launch`。允许 `t` 相同（并发）。
4. **事件接线**：`{ from: <子实例 id>.<出事件>, to: <子实例 id>.<入事件>, delay?, payloadMap? }`。典型接线：投射物类 `onImpact(position, normal)` → 命中爆发类 `impact(position, normal)`；蓄力类 `onRelease` → 投射物类 `launch`；消散类 `onDissolved` → 登场类 `launch`。**接线只在预制体内部**，不触碰任何外部对象。
5. **聚合接口**：编排必须显式声明它对外暴露哪些事件与参数；未声明的子实例接口不暴露。标准参数（`palette/intensity/scale/speed/seed`）默认广播到全部子实例，可按子实例覆写。
6. **嵌套**：编排不可嵌套编排（v1 限制，避免成本模型递归）。
7. **循环与门控**：允许 `wait_for: <子实例>.<出事件>` 门控（时间线条目在事件发生后才计时），不允许无限循环（持续类子实例的 `sustain` 由编排 `end` 统一结束）。
8. **空间关系**：每个子实例声明 `anchor`：`root`（编排根）/ `follow:<子实例 id>`（跟随另一个子实例的当前位置，用于命中承接）/ `offset(Vector3)`。
9. **档位降级**：编排不定义自己的降级表；各子实例按自身降级表降级，编排层只做"总预算超限时按 `priority` 字段从低到高禁用可选子实例"。
10. **命名**：编排 recipe 的 `archetype` 字段取 `orchestration`，子实例在 `children[]` 中各自带 `archetype`。

### 10.3 典型组合骨架（结构描述，不含具体特效名）

- **蓄力 → 投射 → 命中 → 残迹**：B05 → A01/A02/A03 → A06 → D06，接线 `onRelease→launch`、`onImpact→impact`、`onComplete→launch`。
- **预示 → 坠落 → 范围 → 裂陷**：A07(telegraph) ∥ A08 → A07(fill) → D08。
- **登场链**：A13 → C07 → B02（召唤 → 显现 → 持续光环）。
- **破坏链**：D07 → D01 → D06（形变 → 破碎 → 残迹）。
- **UI 链**：F04 → F03 → F08（揭示 → 奖励爆发 → 收集飞行）。

### 10.4 编排级与"内嵌子层"的边界

有些原型（A01–A03、A08）的节拍里写"命中交接给 A06"。两种实现路径：
- **编排路径**（推荐）：投射物 + 命中爆发作为两个子实例接线。好处：命中爆发可独立换元素 / 换档位 / 复用。
- **内嵌路径**：投射物 recipe 直接包含 `impact` 阶段的层（`flash/shock/emission.debris`）。允许，但这些层必须使用与 A06 相同的层角色名，以便元素预设注入一致。

Recipe v2 中两者的表达见 `RECIPE_V2_SCHEMA_DRAFT.md` §6。

---

## 11. 审计清单（供 T2b 与主 agent 复核）

- [ ] 58 个原型全部有六段结构（职责 / 层 / 节拍 / 投影 / 接口 / 降级）+ 元素敏感度。
- [ ] 所有层名取自 §2.1 词表（含 `.后缀`）。
- [ ] 所有技术族 ∈ {material, gpu_particles, cpu_particles, mesh, local_light}。
- [ ] 无原型 / 层涉及后处理、相机、UI 布局、全局光、时间缩放；无序列帧。
- [ ] 每个有发光语义的原型 `light` 层为必需（例外：B07 挂点标记，因多实例并存；F 类，因 UGUI 不受 Light2D 影响，由材质发光承担——两处例外均已在节内注明）。
- [ ] 正文无具体特效名（"元素名 + 形体名"式命名一律不出现）；元素名仅作为"元素敏感度"举例中的形态词（如"锯齿分叉"）。
- [ ] 分类总览表数量与正文节数一致（A13 + B10 + C9 + D8 + E9 + F9 = 58）。

