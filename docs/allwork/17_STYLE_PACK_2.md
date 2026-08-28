# W16 风格包第二批：低多边形 / 宝石琉璃 / 糖果 / 星空 / 蒸汽机械 / 幽魂（6 种风格 + 12 个打样）

> 实现状态（2026-08-25）：6 个新 token、6 个新特效、6 个既有内容变体、共享 Facet/Gear/Symbol/Nebula/Star 资源与正式 Preview 已完成；14 token 机器回归通过，用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：在 W1 的 8 种风格之上再登记 6 种，覆盖更多游戏品类（休闲、女性向、太空、蒸汽朋克、恐怖）。流程同 W1：登记 token → 扩共享库 → 打样验证；每种风格 2 个打样（1 个新特效 + 1 个既有特效的变体 Recipe），证明"新风格可独立成活、也可套用旧内容"。
> 批次预览场景：`VFXPREVIEW_StylePack2.unity`。

## 1. 新风格 token

| token | 名称 | 视觉特征 | 关键技术 | 适用 |
|---|---|---|---|---|
| `lowpoly` | 低多边形扁平 | 硬面三角碎片、纯色面、无渐变无贴图、几何化运动 | 平直着色 Mesh 粒子、面法线硬边 | 3D 为主 |
| `crystal` | 宝石琉璃 | 透亮折光、彩虹色散边、切面高光扫动 | Fresnel+色散渐变 Ramp、切面 Mesh 库 | 2D/3D |
| `candy` | 糖果 Q 版 | 高明度马卡龙色、圆润果冻形变、星星爱心符号粒子 | 强 squash&stretch 曲线库、符号图集 | 2D 为主 |
| `cosmic` | 星空幻彩 | 深空底+星云噪声+星尘、缓慢宏大 | 星云双层噪声、星点闪烁图集、视差流动 | 2D/3D |
| `steampunk` | 蒸汽机械 | 黄铜齿轮、蒸汽喷吐、铆钉面板、机械节奏（棘轮式步进运动） | 齿轮/面板 Mesh 库、蒸汽软粒子、步进动画曲线 | 3D 为主 |
| `ghost` | 幽魂冷光 | 低饱和青绿、半透明拖影、扭曲飘动、忽隐忽现 | 顶点扰动飘动、拖影多实例、呼吸透明度 | 2D/3D |

共享库扩充：切面宝石 Mesh ×3、齿轮 Mesh ×3、符号图集（星/心/泡）、星云噪声 ×2、星点图集 ×1——全部进 `Shared/`，登记来源。

## 2. 打样清单（12 项）

| id / 变体 | 风格 | 说明 |
|---|---|---|
| poly_burst_impact_3d | lowpoly | 三角碎片球状爆散，纯色面翻转 |
| boulder_projectile_3d`.lowpoly` | lowpoly | W7 巨石的低模扁平变体（验证跨批次套用） |
| gem_lance_projectile_3d | crystal | 水晶长矛：切面折光+色散拖尾 |
| crystal_shield_3d`.crystal` | crystal | W4 冰晶盾换宝石琉璃质感 |
| candy_pop_impact_2d | candy | 糖果爆开：果冻弹跳+星心飞散 |
| healing_bloom_aura_2d`.candy` | candy | W7 治疗花环的马卡龙变体 |
| nebula_orb_projectile_3d | cosmic | 星云法球：内部星空视差+星尘尾 |
| summoning_portal_2d`.cosmic` | cosmic | 既有召唤门的星空变体 |
| steam_vent_burst_impact_3d | steampunk | 机械蒸汽爆放：齿轮弹出+汽浪 |
| volt_shield_3d`.steampunk` | steampunk | W5 电盾改黄铜线圈+电火花质感 |
| phantom_wail_area_2d | ghost | 幽魂哀嚎区域：飘动鬼影+冷雾 |
| spectral_trail_3d`.ghost` | ghost | 既有幽轨的完全体幽魂变体 |

## 3. 规格要点

- 6 个新特效按 00 号规格卡全字段展开（开发时写入各自小节）；6 个变体只交付 Recipe + 同屏 A/B 对比帧。
- lowpoly 风格 Manifest 强制 `flat_shading=true`、禁用软粒子与贴图渐变；candy 强制形变曲线库引用；ghost 的"忽隐忽现"用受控透明度节拍（沿用电系受控闪烁的 Manifest 语义，登记为 ghost 专属）。
- 每种风格给出配色卡 3 套（进 style 块 palette 预设，供 UI 色板快捷选用）。

## 4. 批次验收
通用 DoD + 附加：14 种风格（8+6）全 token 校验回归；每风格 A/B 同屏帧人工确认风格差异一眼可辨；变体 Recipe 不改原特效 default 输出哈希。

## 5. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W16 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造六种新增风格的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 6. 后续独立 next-candidate（2026-08-25）

用户后续已授权全项目继续开发；旧候选、旧 Scene 与上述拒绝记录继续保持不变。本轮另建 W16 源码 `next_candidate`，固定为六组相邻 A/B：每组一份新内容与一份既有内容风格变体，共用 style token、使用不同真实载体组合，并序列化 `pairFamily / pairRole / sourceBaseId / CombinationSignature`，使“新风格成活”和“旧内容套用”不再只是标签。未来隔离构建目标为 `VFXPREVIEW_StylePack2_NextCandidate.unity`，当前只完成源码与静态门禁，状态为 `NEXT_CANDIDATE_VISUAL_PENDING`；没有新的用户视觉结论。详见 `docs/stage-notes/W9_W10_W16_NEXT_CANDIDATE_REPORT.md`。
