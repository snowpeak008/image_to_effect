# S5 阶段纪要：正式 2D 模板库

> 状态：完成，主 Agent 独立验收通过  
> 执行日期：2026-08-22  
> 范围：仅 S5；未创建 Recipe 编译器、Handler 或任何 `Assets/VFX/Generated/` 内容。

## 产出

- 用 Unity Editor API 正式导入 `Assets/VFX/Templates/2D/Textures/` 中的 6 张给定透明 PNG：Sprite、alpha transparency、Clamp、Bilinear、无 mipmap；Core/Impact/Shockwave maxSize 1024，其余 512。源 PNG 保持原文件、未覆盖或重生成。
- 正式模板、材质和 Manifest 均在 `Assets/VFX/Templates/2D/`：
  - `PFT_2D_FireCore` — SpriteRenderer 主轮廓。
  - `PFT_2D_Embers` — 连续低频 ParticleSystem。
  - `PFT_2D_FireImpact` — 单次径向 Burst ParticleSystem。
  - `PFT_2D_FireTrail` — 决策规定的 TrailRenderer。
  - `PFT_2D_LaunchFlash` — 单次 0.12 s 闪光。
  - `PFT_2D_Shockwave` — 单次扩张、淡出的环。
- 建立 URP 粒子透明/加法材质：一个 Sprite transparent 基础材质和按纹理分开的 5 个 additive 材质；所有模板依赖均在 Templates 根内。
- 六个 `.manifest.json` 含真实 Prefab GUID/路径、v1 S4 契约、参数 min/default/max、成本估算（峰值粒子/材质/Trail）。`Editor/Domain/VfxBindingKeys.cs` 是唯一的正式 binding 符号表；仅声明符号，没有反射或执行路径。
- 新增 `UnityAssetReferenceResolver`，用 `AssetDatabase.GUIDToAssetPath` 驱动真实 Catalog 检查。
- 保存 Gold Sample：`Assets/VFX/Preview/S5_2D_FireballGoldSample.unity`，固定正交相机、深灰背景、1-unit 尺寸参照，包含 Launch 的同心 Flash+Core、Travel 的同头部 Core/Trail/Embers，和同心 Impact Flash/Burst/Shockwave；阶段沿横向排成故事板。
- 视觉证据保存在 `docs/s5-evidence/`：`travel.png`、`launch.png`、`impact_early.png`、`impact_mid.png`、`impact.png`（late）和 `silhouette-check.png`（黑/灰/亮 × 正常/缩小六格）。截图由保留的最小一次性 authoring/capture 脚本 `Assets/VFX/Authoring/S5TemplateAuthoring.cs` 使用 `Camera.Render()` 生成；隐藏图形设备 batchmode 运行，未使用 `-nographics`，且没有 Bloom。

## 参数三点与静态预算

| 模板 | 开放参数（min / default / max） | 可解释成本 |
|---|---|---|
| FireCore | scale 0.6 / 1.2 / 2.4 | 0 particle, 1 material, 0 trail |
| Embers | rate 4 / 18 / 36；lifetime .25 / .55 / 1.1 | 40 particle 上界, 1 material |
| FireImpact | count 8 / 24 / 40；speed 1.5 / 3.5 / 6 | 40 particle 上界, 1 material |
| FireTrail | time .08 / .22 / .4；width .12 / .42 / .55 | 1 material, 1 trail |
| LaunchFlash | lifetime .06 / .12 / .22；size .45 / 1 / 1.8 | 1 particle, 1 material |
| Shockwave | lifetime .12 / .28 / .5；endSize 1.2 / 2.8 / 4 | 1 particle, 1 material |

范围是模板固定结构内的受控值：低点仍会产生可见模块，默认值对应 Gold Sample，最高点由 `maxParticles`、短寿命和限定 Trail time 约束；这是静态预算预检，不宣称真机性能认证。

## 验证记录

| 命令 | 结果 |
|---|---|
| `cmd /c tools\\compile-check.bat` | 退出码 0 |
| `cmd /c tools\\run-tests.bat EditMode` | 24 total / 0 failed |
| `cmd /c tools\\run-tests.bat PlayMode` | 1 total / 0 failed（既有 Runtime 回归） |

新增真实资产 EditMode 集成测试覆盖：六 Manifest 合法、GUID/path 与 AssetDatabase 一致、Prefab 存在、递归依赖不离开 Templates、正式参数契约由 S4 ManifestValidator 验证、PNG importer 设置、每个 Prefab 可实例化；所有 ParticleSystem 的 `Simulate` 后均断言 `particleCount > 0`，Trail 使用受控位置断言可见点。另有 6 个模板、每个公开参数的 Manifest min/default/max 三点测试：每一个点都单独实例化 Prefab，由测试侧的显式 allow-list switch 写入对应 Unity 属性并回读；Impact count 额外通过 `GetBursts` 回读 min/max count，每个独立实例随后在固定 Simulation 时间窗口断言 `particleCount > 0`；Shockwave endSize 真实改写并回读 size-over-lifetime 曲线终点。此 helper 仅在测试中存在、无反射，非 S6 Handler。模板本身没有 Runtime AI 或 Editor 组件。

## DESIGN_PLAN 评审表

评分说明：静态截图证明可见的轮廓、方向、层级和同心关系；节奏另由 early/mid/late 三帧、实际粒子 duration/curve 和 `ParticleSystem.Simulate` 共同支持，不把单张图片冒充动态证据。

| 维度 | 分数 | 证据与判定 |
|---|---:|---|
| 轮廓 | 2 | `silhouette-check.png` 直接覆盖黑/灰/亮背景与正常/缩小六格；Core 和左向 Trail 均保持可辨。 |
| 节奏 | 2 | `impact_early.png` 是 Flash+初始 Burst，`impact_mid.png` 为 Burst+出现的环，`impact.png` 为扩张环+衰退 Burst；三帧实际由不同 Simulate 时间产生。 |
| 层级 | 2 | `launch.png` 中小 Core 位于中央而较大的 Flash 在其后；`travel.png` 保持 Core 第一中心、Trail/Embers 为辅助；late Impact 则将扩张环置为外层。 |
| 方向 | 1 | `travel.png` 可辨识从左尾连到右端 Core 的平直暖色 Trail 与分离火星，但 Trail 相对 Core 仍短、细；保守不给 2，留待 S7 的真实运动预览复评。 |
| 命中 | 2 | 三张 Impact 帧的 Flash、径向 Burst 与 Shockwave 均在同一世界原点；late 帧可复核环围绕正在消退的中心。 |
| 风格 | 2 | 所有证据均为 clean、warm、compact 的 stylized 火焰；没有烟、动态灯光、屏幕扭曲或 Bloom。 |
| 遮挡 | 1 | `travel.png` 中 Trail 未遮掉 Core，且六格 silhouette 检查在亮背景仍可辨；未做多主体压力场景，保守不给 2。 |
| 收尾 | 1 | `impact.png` late 帧显示外扩环和缩小中心，模板也都是非循环、最长 .5s；没有完整动态视频，保守留待 S7 复评。 |

所有维度至少为 1，轮廓/节奏/命中均为 2；本轮增加的六格轮廓检查和 Impact 三帧序列均可独立复核。

## 范围与后续

- Authoring 脚本保留在明确的 `Assets/VFX/Authoring/`，供视觉复核或受控重生成使用；它不位于模板 Prefab 上，也不是运行时依赖。
- 本阶段未进入 S6：没有 Recipe Build、绑定执行器、反射、Generated 写入或编译器逻辑。

## 主 Agent 独立验收（2026-08-22）

结论：**通过 S5 退出门禁，可以进入 S6。**

- 逐张复核 `travel.png`、`launch.png`、Impact early/mid/late 与 `silhouette-check.png`；命中阶段清楚、Shockwave 可见，黑/灰/亮背景和正常/缩小尺寸均有直接证据。
- 第一版与第二版视觉证据曾因模块分离、Trail 折线、Embers/Shockwave 不可辨而退回；第三版已消除这些阻塞项。方向、遮挡、收尾仍按 1 分保守记录，留给 S7 动态预览复评。
- 代码复核曾发现 Impact count 只检查 Burst 组数、Shockwave endSize 未真实应用的假阳性；整改后测试会回读 Burst min/max count，并真实写入、回读 size-over-lifetime 曲线终点。
- 主 Agent 独立重跑：`compile-check` 退出码 0；EditMode `24/24`；PlayMode `1/1`。
- 6 个正式模板、Manifest、真实 GUID/path、模板内依赖、参数三点、Gold Sample 和视觉评审证据均满足 S5 范围；验收时未发现 `Assets/VFX/Generated/` 或 S6 编译器产物。
