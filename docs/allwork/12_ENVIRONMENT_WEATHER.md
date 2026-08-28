# W11 环境与天气扩展（7 个特效）

> 实现状态（2026-08-25）：7/7 Recipe、strict Runtime Entry、语义 Patch、运行时强度/风向协议与批次 Preview 已完成；机器门禁通过，用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：在 snow_weather_volume 之外补全常用环境/天气体积。全部沿用 Environment Archetype 协议：相机跟随区域、近中远三层密度、sustained 循环、与技能特效不同的"打包为场景氛围"验收口径（覆盖九宫格 B 已定）。
> 批次预览场景：`VFXPREVIEW_Environment.unity`（含一个简单场景盒供氛围对照）。

## 1. 清单

| id | 类型 | 一句话 | 关键分层 |
|---|---|---|---|
| rain_weather_volume | 天气 | 直落/斜落雨，近景雨丝+地面溅环 | 近景雨丝层、中景雨幕层、地面溅花+涟漪、雾气层 |
| sandstorm_weather_volume | 天气 | 横扫沙暴，能见度压制 | 主沙幕横流、卷沙团滚动、地表跑沙层、飞砾偶现 |
| mist_fog_volume | 天气 | 贴地流雾/山岚 | 2–3 层异速雾带、边缘丝状撕开、明暗呼吸 |
| falling_leaves_volume | 氛围 | 飘落叶（速度快慢层+翻转） | 大叶近景翻滚、小叶中景飘摆、着地滑移消失 |
| fireflies_volume | 氛围 | 夜萤光点游曳 | 光点缓游+呼吸明灭、偶发双点缠绕、近景大光斑虚化感 |
| ambient_dust_volume | 氛围 | 室内光尘/浮埃 | 微尘漂浮、光带内密度增强（模拟体积光内尘） |
| waterfall_env_3d | 场景件 | 瀑布：水帘+底部水雾 | 水帘条带流动、帘面白丝、落点雾团翻涌、溅珠、下游泡沫带 |

## 2. 通用规格

- 参数（所有天气体积统一暴露）：`intensity 0–1`（总密度标量）、`wind`（方向+强度）、`area_size`、`camera_follow`（开关）；天气类另有 `near/mid/far` 三层密度独立系数。
- 预算：天气体积放宽档 —— 粒子 ≤160 / PS ≤5 / 材质 ≤4 / 透明 Renderer ≤6（Manifest 声明；这是环境类唯一放宽点，技能类不得引用此档）。
- 生命周期：淡入 ≤2s、稳定循环、淡出 ≤2s；`intensity` 运行期可插值（暴雨→小雨过渡验收项）。
- rain 附加：溅环必须贴地且密度与雨强联动；sandstorm 附加：能见度压制用颜色雾层而非全屏 UI；waterfall 附加：斜视角验证水帘曲面与雾团体积。

## 3. 批次验收
通用 DoD + 附加：每个天气体积录 30s 稳定循环证据（无密度漂移、无粒子池耗尽）；`intensity` 0→1→0 全程无跳变；同屏两体积共存测试（rain + mist_fog_volume）不超合并预算 1.5 倍。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W11 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造天气层次、循环或强度变化的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. 后续全项目授权下的独立 next candidate（2026-08-25）

上述拒绝原文与旧候选保持不变。用户其后另行授权继续全项目开发并把视觉验收统一延后到最后；据此新增 7 个 `w11nc_*`、独立 Recipe/Generated/Shared 根与 `VFXPREVIEW_W11_ENVIRONMENT_NEXT_CANDIDATE.unity`。新实现包含近中远天气层及其独立密度协议、连续 intensity、世界 wind、真实瀑布深度与有界清理；当前仅完成源码和 Roslyn 静态编译，状态严格为 `NEXT_CANDIDATE_VISUAL_PENDING`。隔离 Unity build/Test Runner 与最终用户视觉签署尚未执行，详见 `../stage-notes/W11_W13_NEXT_CANDIDATE_REPORT.md`。
