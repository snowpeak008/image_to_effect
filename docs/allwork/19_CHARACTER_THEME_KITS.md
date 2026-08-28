# W18 角色主题套装（4 套 × 全技能链，综合内容）

> 开发状态（2026-08-25）：**源码、严格构建与机器门禁完成；用户已通过后续特效类批量签署拒绝当前视觉候选。** 已交付 8 个专属 Runtime Entry、百鬼夜行组件、4 个 kit_showcase Composite 和 `VFXPREVIEW_HeroKits.unity`；未授权重做。详见 `../stage-notes/W13_W18_COMPOSITE_HERO_KITS_REPORT.md`。

> 目标：最高层的综合交付——以"一个角色"为单位，把多个 Archetype 的特效统一在一种主题风格下成套产出，验证整条生产链能交付**风格一致的完整角色特效包**（这是实际项目里最常见的需求形态）。每套 6–8 个组件，组件优先引用/变体化前序批次产物，专属新做控制在每套 ≤3 个。
> 排在最后开发（依赖元素批次、风格包、新 Archetype、Composite 协议全部就绪）。
> 批次预览场景：`VFXPREVIEW_HeroKits.unity`（假人+自动技能循环驱动器，按套切换）。

## 1. 套装清单

### Kit A：炎刃武士（stylized 火系 / 3D）
| 组件 | 来源 |
|---|---|
| 普攻拖刀光 ×3 段（红橙渐强） | katana_trail_weapon_3d 变体 ×3 |
| 冲刺斩 | flame_slash_2d 的 3D 适配 + gale 流线 |
| 蓄力居合（大招） | blade_tempest_ultimate_3d 火焰化 overrides |
| 受击/格挡 | hit_flash + parry_spark 红金变体 |
| 登场/阵亡 | hero_entrance / death_dissolve（燃边红）|
| 专属新做：**刀鞘余焰 idle 状态** | 新（Aura，粒子 ≤16 极轻待机） |

### Kit B：冰月法师（crystal+frost 混合 / 3D）
组件：冰锥连发（ice_shard 3D 适配）、冰霜新星（fire_nova 结构复用换 frost 层）、水晶护盾（crystal_shield`.crystal`）、冰封大招（frozen_domain overrides）、登场（twin_portal 冷色）+ 专属新做 ×2：**月轮回旋镖投射物**（Projectile，往返路径协议扩展）、**法杖聚能 idle**。

### Kit C：机械猎手（steampunk+holo 混合 / 3D）
组件：枪口焰三连（muzzle_flash 变体）、蒸汽冲刺（steam_vent + 拖尾）、全息瞄准区域（holo_scan 窄扇变体）、EMP 手雷（emp_nova 3D 适配）、机甲登场（glitch_blink 逆放）+ 专属新做 ×2：**锁链钩爪**（Beam 双端点+收缩回卷）、**过热排汽 idle**。

### Kit D：幽咒巫女（ghost+inkwash 混合 / 2D）
组件：墨符飞弹（arcane_missile 墨化变体）、鬼手缠缚（shadow_grasp 变体）、幽魂领域（phantom_wail 扩大档）、诅咒印记（curse_mark 直用）、大招·百鬼夜行（新 Composite：ink_dragon_trail ×3 + 鬼影队列）+ 专属新做 ×1：**灵符结界八卦阵**（Shield，符纸八枚环布）。

## 2. 套装级规格（每套开发时展开）

- **主题一致性表**：每套定版三色主色板 + 风格 token 组合 + 形状语言（Kit A 锐角新月 / Kit B 六棱与圆 / Kit C 齿轮与直线 / Kit D 飘带与符纸），套内所有组件的 style 块必须引用同一预设——机器可验（palette 引用一致性检查）。
- **技能循环编排**：每套一份 `kit_showcase` Composite（普攻→技能→大招→受击→死亡→登场循环），作为套装整体验收载体与宣传演示。
- **专属新做组件**按 00 号规格卡全字段展开；idle 类统一预算：粒子 ≤16 / PS ≤1 / Renderer ≤3（常驻必须极轻）。

## 3. 批次验收
通用 DoD + 附加：每套 kit_showcase 一镜到底录像；套内 palette 一致性机器检查全绿；变体组件不改原件 default 哈希；四套同屏切换演示（资源加载/卸载干净，无残留实例）。

机器状态：主题、palette、引用合法性、实例复用与 Reset/Release 清理均已通过；四套的视觉一致性仍等待用户最终签署。

---

## 全计划总规模（W1–W18）

- 特效族：91（元素/风格/环境/打击感/大招/屏幕） + 10（新 Archetype） + 6（风格包二打样） + 10（游戏 UI 交互） + 4 套角色包（专属新做约 8 个 + showcase Composite 4 个）≈ **129 个新特效族**
- 风格：8 + 6 = **14 种**；Archetype：14 + 6 = **20 类**
- UI：编辑器 VFX Studio（W2 五页签） + 战斗屏幕包（W14） + 游戏交互 UI 包（W17）三层齐备

## 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W18 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐套/逐帧视觉核对，不据此伪造四套角色包的主题一致性、技能链节奏或残留问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 后续独立 next candidate（W24 全项目授权）

后续用户已另行授权全项目继续开发；该授权仅建立独立的 W18 下一候选，不撤销、不改写上方旧候选拒绝。新源码使用 `W18CharacterThemeController` 与 `w18-theme-next-candidate/v1`，四套输出 id 全部带 `_next_candidate`，目标 Scene 为 `Assets/VFX/Preview/VFXPREVIEW_HeroKits_NextCandidate.unity`，旧 Prefab、旧 Scene 与旧证据均不覆盖。

四套候选分别以锐角新月/上扬刀线、六边形/月轮、齿轮/直线瞄准、墨带/八符纸为真实 Mesh/Line 拓扑；每套一份不可混用的 palette 引用，并按 Idle→普攻链→位移→技能→大招→受击→死亡→登场循环驱动。手、武器、胸口、脚底载体可重绑到外部角色 rig，Reset/Stop 恢复原父级。单套上限 Renderer 14、Material 3、ParticleSystem 1/容量 16；当前实现实际为 10–12 Renderer、1 Material、0 ParticleSystem。Preview 使用 `VFXComposer/NextCandidate/WorldCellClip` 片元裁剪，不靠标签或自报状态遮掩跨格。

当前仅完成源码、稳定 Recipe/.meta、batch-safe authoring 与 Roslyn 静态编译；尚未运行隔离 Unity 的 Build/Edit/Preview/Play 门禁，也未进行用户视觉验收。唯一合法状态为 `NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null`。
