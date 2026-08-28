# W1 风格体系与共享资产底座

> 实施状态（2026-08-25）：**源码与机器门禁仍为完成；用户已对 `VFXPREVIEW_W1_StyleSamples.unity` 作出最终视觉结论：拒绝。** 当前候选为 `rejected`，未授权重做、修改源码/资产或生成下一候选。实现与证据见 `docs/stage-notes/W1_W2_STYLE_STUDIO_REPORT.md`。

> 目标：把"风格"从隐含的手调结果，升级为 Recipe 中的一等公民维度，让同一个 Archetype 特效可以在多种美术风格间切换与批量生产。这是后面所有内容批次的前置。
> 预计规模：Recipe 字段扩展 + 8 个风格定义 + 共享 Shader/遮罩库扩充 + 2 个打样特效验证。

---

## 1. 风格 Token 定义

风格是与 Archetype、元素正交的第三维度。首批定版 8 种：

| token | 名称 | 视觉特征 | 主要载体 | 适用 |
|---|---|---|---|---|
| `stylized` | 风格化（现状默认） | 手绘感渐变、明确色阶、干净剪影 | 程序 Mesh + 笔刷遮罩 | 2D/3D |
| `cartoon` | 卡通描边 | 大色块、2–3 级硬色阶、深色描边、Q 弹时间曲线 | 程序 Mesh + 色阶 Ramp | 2D/3D |
| `pixel` | 像素 | 低分辨率量化、有限调色板、逐帧跳变而非平滑插值 | 低分 RT 量化 Shader / 帧动画图集 | 2D |
| `inkwash` | 水墨 | 墨色浓淡、飞白、边缘晕染、低饱和 + 单点缀色 | 笔刷遮罩 + 晕染噪声 | 2D 为主 |
| `semireal` | 半写实 | 柔和辉光、丰富噪声细节、物理感烟尘 | 多层噪声 Shader + 软粒子 | 3D |
| `holo` | 全息科幻 | 扫描线、Fresnel 边缘光、故障闪断、加色蓝青 | Fresnel/扫描线 Shader | 3D |
| `dark` | 暗黑仪式 | 低明度、血红/紫黑、符文、烟雾质感 | 符文遮罩 + 烟雾噪声 | 2D/3D |
| `neon` | 霓虹赛博 | 高饱和双色、锐利辉光带、电子脉冲节奏 | 加色描边 Shader | 2D/3D |

规则：

- 新风格必须先在本表登记 token，再进 Recipe；未登记 token 校验必须报错（沿用"未知字段报错"原则）。
- 一个特效的 default Recipe 声明其基准风格；风格变体是同模板下的另一份 Recipe，**不允许**为风格复制模板本身，除非该风格改变了结构（如 pixel 用帧动画图集替代粒子，此时是新模板并在 Manifest 里声明）。

## 2. Recipe 扩展（v1.1，向后兼容）

在 Recipe 顶层新增可选 `style` 块；缺省视为 `stylized`（现有 Recipe 字节不变，不触发迁移）：

```json
"style": {
  "token": "cartoon",
  "palette": { "primary": "#FF6A00", "secondary": "#FFD84D", "accent": "#FFFFFF" },
  "outline": 0.12,
  "shading_steps": 3,
  "noise_scale": 1.0,
  "glow_strength": 0.6
}
```

- `token` 必须在登记表内；数值字段每个模板在 Manifest 中声明支持范围，不支持的字段出现即报错。
- Patch 语义扩展两条：`set_style_token`（切风格变体，仅限 Manifest 声明支持的 token）、`set_palette`（换配色，不换结构）。这是"文字改风格"的入口。
- Schema 文件：`docs/ai-workflow/recipe-v1.schema.json` 升级为 v1.1，附迁移说明进 `docs/release/UPGRADE_AND_MIGRATION.md`。

## 3. 共享资产底座扩充（Shared 库）

本轮一次性补齐后续批次高频复用的素材，全部进 `project/Assets/VFX/Shared/`：

### 3.1 Shader（`Shared/Shaders/`，URP Unlit 系）

| Shader | 用途 | 关键属性 |
|---|---|---|
| `VfxLayeredRamp.shader` | 通用三层色阶（cartoon/stylized 主力） | Ramp 3 色、描边宽度、Reveal、Breakup 噪声 |
| `VfxSoftNoise.shader` | 半写实烟/雾/火（semireal 主力） | 双层滚动噪声、软边、HDR 强度 |
| `VfxHoloFresnel.shader` | 全息（holo 主力） | Fresnel、扫描线密度、故障频率 |
| `VfxPixelQuantize.shader` | 像素量化（pixel 主力） | 目标像素密度、调色板 LUT、抖动开关 |
| `VfxInkBrush.shader` | 水墨（inkwash 主力） | 墨浓度、晕染半径、飞白阈值 |
| `VfxDissolveEdge.shader` | 通用溶解/燃边（消散层通用） | 溶解阈值、燃边颜色、边宽 |

### 3.2 遮罩与噪声图（`Shared/Textures/`，共享、只计一次）

- 笔刷遮罩 ×4（宽笔触、飞白、碎裂、涡旋）；噪声 ×4（Perlin、Voronoi 裂纹、丝状、颗粒）；形状遮罩 ×6（软圆、环、六边、星芒、符文环、扫描线条）。复用 S15 已要求的 BrushMask/BreakupNoise/SparkAtlas 规格与登记流程（SHA-256、导入设置、来源记录）。
- 像素风调色板 LUT ×3（暖火、寒冰、毒紫）。

### 3.3 Mesh（`Shared/Meshes/`）

- 弧形刀光带（复用 S15 中心线方案）、半球壳、圆柱束、地面环、锥形喷射、平面碎片组。全部程序生成脚本进正式 Authoring，带确定性种子。

## 4. 打样验证（本工作包的验收载体）

选两个已有特效做风格打样，证明协议闭环：

1. `fireball_2d`：新增 `fireball_2d.cartoon.json` 与 `fireball_2d.neon.json` 两份风格变体 Recipe，结构不变仅走 style 块。
2. `frost_impact_2d`：用 `set_style_token` Patch 从 stylized 切到 dark，证明 Patch 路线可用。

## 5. 验收

- Schema v1.1 校验：非法 token、越界数值、不支持字段均有可定位错误码（登记进 `docs/release/ERROR_CODES.md`）。
- 现有全部 Recipe 不加 style 块时构建输出哈希不变（向后兼容证明）。
- 打样 4 份变体在同一 Preview 场景同屏对比截图，人工确认风格差异一眼可辨。
- 共享库每个 Shader 有最小材质样例与静态预算记录。

### 5.1 用户视觉拒绝记录（2026-08-25）

用户结论：**拒绝**。用户原话：“拒绝；W1样例尺寸未统一，Stylized基准过大、Dark frost过弱，Holy新星与Holo射线跨格、裁切并遮挡标签；场景缺少格内约束和8种style token的完整视觉对比，整体未达到商用级视觉完成度。”

本次拒绝对应原计划视觉要求：同 Archetype 风格变体未在统一尺度下形成稳定可比的构图；大体量新星、射线与轨迹跨格并遮挡标签；七格 Scene 未提供全部 8 种首批 style token 的完整视觉对照。用户所述“未达到商用级”仅记录为视觉制作完成度评价，不作版权、许可或法律解释。

Preview 构建器当前直接使用各 Prefab 的原始尺寸和 `localScale=1`，没有 Bounds 归一、格内裁剪或独立 viewport；所有条目同时播放。这些结构足以复现用户截图中的尺寸失衡、跨格、裁切和标签遮挡。机器门禁只证明 Schema、构建、生命周期、依赖与确定性，不构成视觉通过。

根据用户指令，本次只记录拒绝、画面问题和已确认直接原因。下一候选的允许修改范围当前为“无”；须等待用户另行授权，不自动重做或扩大到 W2。

### 5.2 后续独立 next-candidate（2026-08-25）

在上述拒绝记录完成后，用户另行授权 W1 重做。本轮没有回写或替换旧 `VFXPREVIEW_W1_StyleSamples.unity`，而是新增 W1-only compiler `w1-style-next-candidate-1` 和独立目标 Scene `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity`；状态为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`。

新候选固定为 4 × 3：八个首批 style token 各占一格，fan+wave、charge+occlude、telegraph+nova 三份 trace-backed“能力 + 皮肤”各占一格，最后一格显示边界契约。11 个效果格使用统一 root scale 与 `1.56 × 1.08 × 0.50` local envelope，每格由独立 layer/Camera viewport 硬裁剪；标签改放在与全部效果 viewport 不相交的 Overlay safe band。真实 Material mode、Mesh 载体与 timing profile 共同形成八种 token 的可观测差异，Dark 另有更高能量下限；三份组合分别驱动 5 个 fan shard、9 点 occluded beam 和 telegraph 后的 12-ray batched nova。

隔离 Unity 已真实生成 11/11 Prefab、11/11 Manifest 与新 Preview Scene，并通过 W1 EditMode `3/3`、Preview `1/1`、PlayMode `4/4`；W-C1/W-C2/W-C3、旧 W1 与 W3+ 的定向共享回归也全部通过。上述机器结果不产生用户视觉签署，新候选仍为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`。精确修复、预算、测试过滤器、复现命令与共享回归清单见 `docs/stage-notes/W1_NEXT_CANDIDATE_REPORT.md`。
