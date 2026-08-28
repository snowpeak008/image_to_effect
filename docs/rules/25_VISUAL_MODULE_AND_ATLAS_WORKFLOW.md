# 25 视觉模块与 Atlas 生产流程

## 1. 适用范围

本规则适用于所有 Archetype、Dimension 和渲染后端中使用位图/Sprite/Particle Texture 的视觉模块，包括 Projectile、Impact、Slash、Aura、Area、Beam、Trail、Shield、Spawn/Transform、Environment、Screen/UI，以及未来新增类型。

本规则不要求所有特效必须使用位图；纯 Mesh、Trail、程序化 Shader 或 VFX Graph 可以不创建颜色 Atlas，但其 Mask/Noise/Lookup Texture 仍受本规则约束。

## 2. 核心原则

```text
完整概念图（评审，不进 Player）
        ↓ 视觉拆解
独立、透明、无污染的模块源文件
        ↓ 裁边 / 定尺 / 变体 / 色彩与 Alpha 清理
按视觉 Family + 加载组打包的 Runtime Atlas
        ↓ 共享 Shader / Material + 参数化
Runtime Entry 内的 Particle / Mesh / Trail / UI 模块
```

- 完整概念图 MUST 只用于视觉方向、模块拆解和最终对比，MUST NOT 整张作为 Runtime 特效贴图。
- “文件少”不等于“资源标准”。目标是少量、紧凑、可复用、压缩正确的模块，而不是用粗糙占位图替代视觉层。
- Atlas 是经过清理的独立模块集合，不是完整效果截图，也不是把任意源文件简单拼在一起。
- Shared 不能成为隐藏内存成本的标签；每个 Runtime Entry 仍 MUST 报告完整依赖驻留 Texture。

## 3. 完整图片能否直接裁剪

### 3.1 允许

满足以下任一条件时 MAY 从源文件导出模块：

- PSD/Krita/原生工程具有独立图层，模块之间无烘焙交叉污染；
- 生成任务预先要求“互不重叠、透明背景、固定单元格、独立模块”，并通过 Alpha/边缘检查；
- 扁平图中的目标区域完整可见、不被其他模块遮挡，且裁剪后不存在背景、光晕、其他元素残留。

### 3.2 禁止直接裁剪

以下情况 MUST NOT 直接作为 Runtime 模块：

- 冰环/冲击波被核心、碎片、角色或背景遮挡；
- 模块已经烘焙其他模块的光、雾、阴影或颜色；
- 背景不是干净 Alpha；
- 需要靠修补大面积缺失结构才能恢复完整轮廓；
- 从完整概念图裁下后仍携带黑边、色边、矩形底板或不可解释的残影。

遇到上述情况，应以概念图为 Reference，重新绘制、重新生成或通过受控编辑获得独立模块；不得把“可以裁出来”误写为“可交付”。

## 4. 模块拆解标准

Gate 0 的 Brief MUST 给出 Module Decomposition Table。按实际效果选择，不强制全部存在：

| 角色 | 典型内容 | 常见复用范围 |
|---|---|---|
| Core/Flash | 核心光、星芒、起爆点 | 多种元素 Impact/Spawn |
| Main Shape | 弧、刃、球体、盾面、区域主体 | 同 Family/Archetype |
| Ring/Wave | 冲击环、范围环、波纹 | Impact、Aura、Area |
| Trail/Streak | 拖尾、速度线、残影 | Projectile、Slash、Beam |
| Debris/Shard | 冰晶、石块、火花、叶片 | 同元素 Family |
| Mote/Spark | 雪粒、尘粒、小光点 | 跨 Archetype |
| Mist/Cloud | 烟、雾、能量云 | Impact、Aura、Environment |
| Mask/Noise | 溶解、扰动、流动、破碎 | 跨项目 Shader Family |
| Distortion/Normal | 扭曲或法线数据 | 目标平台支持时 |

每个模块 MUST 记录：语义 ID、来源、是否可复用、变体数、目标屏幕像素尺寸、Alpha/Blend 语义、Pivot/方向、Atlas 单元和消费者列表。

### 4.1 最大合理拆解（Decompose-to-Reuse）

可被独立复用、旋转拼接、参数化或程序化重建的视觉资源，MUST 在保持视觉质量和运行效率的前提下拆解；禁止因为完整图制作方便，就把大量重复像素、透明空白或可复用结构长期烘焙在一张 Effect-local 大图中。

出现以下任一情况时 MUST 提交拆解方案：

- 同一视觉单元在单个效果中重复 `4` 次及以上；
- 完整资源具有明显旋转、镜像、平移、平铺或环形重复结构；
- 两个及以上效果可共享 Core、Shard、Segment、Mote、Mist、Mask、Noise 或基础 Mesh；
- 完整贴图包含可由 Mesh、Trail、粒子参数或 Shader 生成的大面积规则结构；
- 单个完整 Sprite 因透明中心/空白边界造成显著 Atlas 或 GPU 驻留浪费；
- 拆解后可在不增加 Material/Draw Call 的情况下明显降低 Source、Build 或 GPU 成本。

推荐的拆解终点是“最小稳定语义模块”，不是最小像素碎片。每个拆分结果必须具有稳定 ID、清晰 Pivot/方向、独立 Alpha/Blend 语义，并能由一个确定性组合规则复原目标视觉。

冰环标准示例：破碎冰环 SHOULD 使用 `8–16` 个环段，由一个 Particle System 或一个合并 Mesh、一个共享 Material 和 `3–4` 个轮廓变体组成；允许少量缺口、半径/角度/尺寸扰动。不得创建 `8–16` 份 Material、GameObject 或相同纹理副本。环段应使用紧凑弧形 Mesh/UV，不得把完整圆心透明区重复存入每个 Sprite。

拆分是资源与组合方式，不是必须暴露在画面中的造型。Brief 要求连续的 Ring、Aura、Area 边界、循环 Trail 或光带时，逻辑上即使由多段组成，视觉上仍 MUST 连续；只有 Brief 明确要求破碎、断裂或节奏缺口时，才允许显示分段。

所有环形、循环和平铺模块 MUST 同时满足“双重闭合”：

- 几何闭合：相邻段共享同一边界计算；闭环末端必须复用起点的内外顶点、法线及其他连续属性，能精确复用时不得只依赖近似容差。
- 着色闭合：UV、Noise、法线、溶解和颜色函数必须周期连续；优先使用圆周 `cos/sin` 坐标或已验证可平铺纹理，禁止在线性 UV 首尾制造暗线。
- 验收闭合：在峰值与衰减帧放大检查 `0°/90°/180°/270°` 和 UV seam；整体缩略图无明显问题不能替代局部检查。

拆解或压缩版本必须与上一可审候选在相同 Preview Scene、Camera、时间线和关键帧下 A/B。若环体退化为单线、齿轮、重复图章、接缝或亮度断层，即使 Source/Build/GPU 成本下降也判定失败。

以下情况达到停止拆解条件，MAY 保留较完整模块，但必须在 Brief/Manifest 中记录理由：

- 拆分会产生明显接缝、破坏不可分离的笔触/光照/流体连续性；
- 新增 Draw Call、Material、Overdraw、粒子更新或管理成本高于内存收益；
- 模块只有一个消费者且不存在可验证的重复像素/透明浪费；
- 目标平台压缩与实际 Build Report 证明继续拆分没有净收益；
- 视觉 A/B 显示拆解版本明显劣化。

Gate 0 必须记录“保留完整 / 拆分 / 程序化”的决策；Gate 6 必须报告拆解前后 Source Bytes、Build Disk Bytes、GPU Resident Bytes、Renderer/Particle/Material/Draw Call 与视觉差异。只减少文件数量或只减少 PNG 大小均不能单独证明方案更优。

## 5. ArtSource 与 Runtime 资产边界

推荐仓库结构：

```text
ArtSource/VFX/<Family>/
├─ Concepts/                  # 完整参考图；Unity 不导入
├─ Modules/                   # 高分辨率、分层、可编辑源文件
└─ AtlasLayout/               # Atlas 排布源与导出配置

project/Assets/VFX/Shared/<Family>/
├─ Textures/                  # 仅 Runtime Atlas / Mask / Noise
├─ Materials/
└─ Shaders/
```

- AI 生图、外包原图、PSD、无损大图均属于 ArtSource；未经处理 MUST NOT 直接进入 Runtime 依赖。
- AI 输出 MUST 记录 prompt/用途、生成方式、生成时间、源文件 SHA-256 和许可状态。
- Runtime 导出必须是确定性、可复跑过程；源文件变化时 Atlas/Manifest dependency hash 必须变化。
- 目标/reference 图片可以保留在 `docs/` 或 `ArtSource/`，但 MUST 通过 Player 依赖审计证明不进入构建。

## 6. 裁边、定尺与透明通道

- 模块先按语义边界 Tight Crop，再添加 `4–8px` 或相当比例的安全 Padding；禁止保留与内容无关的大面积透明方形画布。
- 纹理尺寸由目标相机中的最大投影像素决定，不由 AI 原图分辨率决定；默认源单元不超过实际最大投影的 `1–2x`。
- Ring 等天然需要方形包围盒的形状可保留透明中心，但必须登记原因，不能用该例外容忍普通 Shard/Trail 的空白浪费。
- Alpha 边缘必须检查黑边、白边、色溢、Premultiply 语义和压缩后破损。
- Pivot/方向是模块契约。具有运动方向的 Shard/Streak 必须声明 `+X` 或 `+Y` 为前向，并用运行时测试确认尖端朝运动方向。

## 7. Atlas 分组与复用

- Atlas MUST 按 `视觉 Family + 生命周期加载组 + Blend/采样兼容性` 组织；禁止一个全项目 Mega Atlas。
- 推荐命名：`T_<Family>_<Role>Atlas_<Variant>_vN`，例如 `T_Frost_CommonAtlas_A_v1`。
- 同一 Family 的 Impact、Aura、Area、Projectile MAY 复用 Core、Ring、Shard、Mist、Mote 与 Noise。
- Shared 资产默认需要至少两个已登记消费者，或在 Manifest/Brief 中列出已批准的 Family 复用计划；仅改目录名不得宣称已复用。
- 一个 Atlas 中的稳定单元必须有 ID、Rect、Pivot、方向和用途清单；代码不得依赖偶然排列。
- Atlas 单元矩形占 Atlas 面积的利用率 SHOULD `>= 65%`；无法满足时拆 Atlas 或记录 waiver。
- 变体只为降低视觉重复而存在，不作为逐帧动画误报。一个画面同时出现超过 `4` 个显著同类粒子时，SHOULD 提供至少 `3` 个轮廓/纹理变体，或提供等价的 Shader/几何随机化。

Particle System 可通过 Texture Sheet Animation 的 Grid/Sprite 模式和随机 `startFrame` 选择变体；只有 `frameOverTime` 随生命周期变化时才属于 Flipbook/序列帧动画。

## 8. 默认 Atlas 档位

以下是项目默认起点，不是所有效果强制使用一张图：

| 档位 | 建议 Atlas | 典型用途 | 说明 |
|---|---|---|---|
| Small | `256×256` | 少量 Core/Mote/Mask | 简单特效或全局小图集 |
| Standard | `512×512` | Ring + 3–6 Shard + Core/Mote | 默认 2D Family Atlas |
| Complex | `1024×1024` | 多层笔触/高质量 3D Billboard | 需要 Gate 6 报告与理由 |
| >1024 | waiver | 特写、环境或高端平台 | 必须给投影像素和平台证据 |

Frost Family 的推荐 Standard 示例：

```text
T_Frost_CommonAtlas_A_v1 (512×512 RGBA)
├─ Core flash x1–2
├─ Broken ring x1–2
├─ Mist ring x1
├─ Shard x4–6
└─ Snow/Spark x4–8

T_VFX_NoiseMask_A_v1 (128–256, Linear/Single Channel)
└─ dissolve / breakup / flow noise
```

该布局是 Family 共享资源，不要求每个 Effect 独占一张 Atlas；也不得为了“只用一张 Atlas”把不同时加载的视觉 Family 强绑在一起。

## 9. 导入与平台压缩

- Runtime Texture 默认 MUST 使用平台支持的压缩；`Uncompressed` 只允许调试、像素精确 UI 或经 waiver 的特殊数据纹理。
- PC/桌面 RGBA 默认评估 DXT5/BC7；移动端评估 ASTC/ETC2；最终由目标平台 Profile 决定。
- Crunch 只影响磁盘/下载大小，不替代 GPU 驻留内存报告。
- Color Atlas 通常使用 sRGB；Mask/Noise 默认 Linear；Read/Write 默认关闭。
- 固定正交 2D 且不存在显著缩放/远近变化时 MAY 关闭 Mipmap；3D、多距离或缩放场景必须单独评估。
- Importer Max Size 必须与 Runtime Atlas 档位一致，禁止用 `1254×1254` 等 AI 源尺寸直接决定运行时规格。

### 9.1 可选择的导出规格

资源导出尺寸与游戏世界中的视觉尺寸是两个独立概念：

- `visualScale/referenceWorldSize` 决定特效在相机中看起来多大；
- `exportProfile/textureScale/maxAtlasSize` 决定 Runtime 纹理分辨率、压缩和内存成本。

生成正式资产前 MUST 显式选择导出规格；未选择时使用项目的 `balanced` 默认值，禁止直接继承 AI 原图分辨率。首版统一支持以下语义档位：

| Profile | 用途 | 相对基础分辨率 | 正式交付 |
|---|---|---:|---|
| `preview` | 快速设计预览 | `0.5x` | 否，只能生成临时候选 |
| `compact` | 小屏/大量并发/移动优先 | `0.5–0.75x` | 是，需视觉 A/B 通过 |
| `balanced` | 默认 PC/通用运行档 | `1.0x` | 是 |
| `high` | 近景、Boss、宣传镜头 | `1.5–2.0x` | 是，需预算允许 |
| `custom` | 明确的项目特殊需求 | 显式配置 | 是，需记录 waiver/理由 |

Recipe/Build 请求的目标字段设计为：

```json
{
  "export": {
    "profile": "balanced",
    "textureScale": 1.0,
    "maxAtlasSize": 512,
    "platformCompression": {
      "standalone": "bc7",
      "android": "astc_6x6"
    },
    "buildDiskBudgetBytes": 262144,
    "gpuResidentBudgetBytes": 196608
  }
}
```

固定 Profile 可隐藏高级字段；`custom` 才允许显式覆盖。文件最终压缩大小受图像内容和平台编码器影响，因此 `buildDiskBudgetBytes` 是门禁上限，不是保证生成恰好相同字节数。

Compiler MUST 将“请求值”和“实际解析值”同时写入 Build Manifest，至少记录：Profile、Texture Scale、每张 Atlas 的源/导入尺寸、平台格式、Source Bytes、Build Disk Bytes（可测时）、GPU Resident Bytes 和是否超预算。超出硬预算时禁止提交正式输出；视觉 A/B 失败时不得以体积更小为理由替换已通过版本。

## 10. Material 与组合方式

- 优先让同一 Family Atlas 被少量共享 Material 使用，例如 Additive 与 Alpha Blend；不得为每个模块复制 Material。
- Blend MUST 按模块职责选择：短促能量核心/火花可用受控 Additive，持续轮廓、雾、烟和大面积面片默认使用 Alpha/Premultiplied；禁止为了省配置让全部模块无条件共用 Additive。
- 组合前 MUST 定义视觉能量预算，至少区分核心、主体、从属轮廓、空间层和高频细节。不同职责不得默认共用同一条 Color/Alpha over Lifetime 曲线。
- 参考图中的复杂 Ring/Wave SHOULD 拆成主环、雾环、碎片/火花和 Noise 驱动的溶解层，而不是用一个规则圆圈占位后声称完成。
- 多层视觉可以共享同一 Atlas 和 Shader，通过 UV Rect、Tint、Dissolve、Rotation、Custom Data 区分。
- Atlas 复用不代表画面必须相同；Recipe/Shader 参数、变体选择、速度、尺寸、时间线和局部组合应形成 Effect 差异。

## 11. 流程门禁

### Gate 0

- 冻结概念图用途和不进入 Runtime 的边界；
- 输出 Module Decomposition Table；
- 预登记 Family、消费者、Atlas 档位和预计驻留内存。

### Gate 1

- 先验证独立模块和最小组合；
- 同时提供 Atlas Layout/Pack Preview，不接受只看完整效果截图；
- 检查方向、Pivot、Alpha、重复感和目标距离可读性。

### Gate 3

- Runtime Entry 只能依赖 Runtime Atlas/Mask，不能依赖 Concepts/ArtSource；
- Manifest 记录 Atlas GUID/hash、单元契约版本和完整消费者依赖；
- 同一源模块不得在 Local/Shared 中出现无理由副本。

### Gate 5

- 正式 Runtime 画面与概念图逐模块对比；
- 检查 Ring/Trail/Mist 等多层结构是否被错误压缩成单层占位；
- 检查同一 Sprite 的重复、朝向和后期衰减。
- 至少同时审查峰值帧与衰减帧，验证中心不过曝、主体中后段可读、从属层不反客为主、各层颜色与亮度连续；结构测试或文件规格通过不得替代视觉签署。

### Gate 6

- 同时报告 Source 文件大小、Build 磁盘大小和 GPU Resident Memory；三者不得混用；
- 验证 Recipe/Build 请求的导出 Profile 已被解析，Manifest 中的请求值与实际 Atlas/Importer/平台格式一致；
- 报告 Atlas 尺寸、格式、Mipmap、利用率、消费者和并发实例；
- 记录单效果完整依赖驻留成本，Shared 不豁免；
- 对比压缩前后视觉，避免以明显 Alpha/裂纹损坏换取门禁通过。

## 12. 官方依据

- Unity 2022.3 Texture Sheet Animation：`https://docs.unity3d.com/cn/2022.3/Manual/PartSysTexSheetAnimModule.html`
- Unity 2022.3 Sprite Atlas Workflow：`https://docs.unity3d.com/kr/2022.3/Manual/SpriteAtlasWorkflow.html`
- Unity 2022.3 Sprite Atlas Properties：`https://docs.unity3d.com/ja/2022.3/Manual/class-SpriteAtlas.html`
- Unity 2022.3 Texture Formats：`https://docs.unity3d.com/cn/2022.3/Manual/texture-compression-formats.html`
- Unity 2022.3 Platform Texture Formats：`https://docs.unity3d.com/ja/2022.3/Manual/class-TextureImporterOverride.html`
