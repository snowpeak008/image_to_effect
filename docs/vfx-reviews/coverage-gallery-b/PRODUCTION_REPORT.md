# 空间与呈现覆盖九宫格生产报告

日期：2026-08-23  
状态：第三轮 Screen/UI 被用户拒绝；第四轮候选已重建；用户于 2026-08-25 对第 3 项签署为**有条件通过**，不等同于无条件视觉通过或商用认可

## 1. 覆盖范围

本面包含 `3D Impact / 3D Aura / 3D Area / 3D Beam / 3D Trail / 3D Shield / 3D Spawn / Environment-Weather / Screen-UI`。前七项补足已有 2D 类型的空间纵切；后两项补足规则中的剩余正式 Archetype。

## 2. 第一轮拒绝

第一轮虽然 Compile、结构与像素存在成立，但视觉仍有两类明确错误：

- 一个通用 Transform 动画同时旋转所有层，导致 Area/Spawn 地面环侧翻、Shield 读形不稳；
- Screen/UI 把全安全区矩形加入旋转数组，产生巨大粉色遮挡块。

第一轮不得作为验收证据。根因已递归为 `EXP-015 / EXP-016`。

## 3. 第二轮修正

- Impact 提前达到空间爆发峰值；
- Aura、Shield 使用自己的三轴轨道/壳运动；
- Area 与 Spawn 只绕地面法线旋转；
- Beam 增加主束宽度和端点；
- Trail 增加真实 TrailRenderer 的宽度、寿命和运动范围；
- Environment 提高雾层与天气体积；
- Screen/UI 改为固定安全区边框，仅局部警告标记动画。

七个持续或事件控制型候选跨九宫格周期保持运行；3D Impact 与 3D Spawn 是一次性并按周期重播。

## 3.1 第二轮用户拒绝

用户实机截图确认第二轮仍不合格：Environment 暴露矩形雾卡，Screen/UI 只是调试边框，Aura/Area/Shield/Spawn 的主轮廓仍然是同一套分段圆环。Impact 的预览空窗也过长。完整记录见 `VISUAL_REJECTION_2026-08-23.md`。此前 `evidence/current-run` 降级为 rejected，不得继续称为当前视觉证据。

## 3.2 第三轮候选

- Aura 改为垂直包体与两条能量流，圆环只保留为低亮地面锚点；
- Area 改为贴地不规则能量池，边界仅为辅助；
- Shield 改为完整球壳、内部晶格与局部命中面；
- Environment 移除所有雾 Quad，改用不规则 Cloud Mesh，消除矩形卡边；
- Screen/UI 改为四角软晕影、方向命中条和中心反馈，不再绘制连续调试边框；
- Preview Driver 只对 Impact 增加一次审查用重播，正式 Entry 仍为 `one_shot`。

第二轮证据保存在 `evidence/rejected-run-20260823-user-visual-2/`；第三轮 Screen/UI 失败证据保存在 `evidence/rejected-run-20260823-screen-ui-3/`；第四轮候选位于 `evidence/current-run/`。工程验证不代替用户动态视觉验收。

## 3.3 第三轮 Screen/UI 拒绝与第四轮修改

第三轮右下角仍被用户拒绝。失败证据已移动到 `evidence/rejected-run-20260823-screen-ui-3/`。根因不是单纯颜色或透明度，而是正式 Screen/UI Prefab 错误序列化了九宫格右下角锚点。

第四轮将正式 Prefab 恢复为全屏安全区；九宫格只对场景实例设置缩略锚点。新增独立权威预览 `Assets/VFX/Preview/VFXPREVIEW_DamageWarningUI_Fullscreen.unity`。视觉结构改为薄软边缘晕影、单一顶部方向提示和小型中心命中标记，并按事件短时淡入淡出。九宫格右下角现在只用于类型覆盖缩略检查，不能代替全屏验收。

## 4. 资源策略

- 九个 Effect 各自只有一个正式 Runtime Prefab；
- 共享一个 Shader、两份 Material、Quad/Ring/Burst 三个 Mesh；
- Burst、Motes、Weather 最初各自保存为一份重型 ParticleSystem Prefab，导致 Shared Source 达到约 `377 KB`，该候选已拒绝；最终只保留一个 `PF_CoverageB_Particles.prefab`，三种差异由 Runtime Profile 配置；
- 本轮新增 PNG 为 `0 B`；
- Screen/UI 的 `Packages/com.unity.ugui/` 依赖记录到 Manifest，但不授予删除权。

第三轮九个正式输出目录源码合计 `133,956 B`；Shared 目录源码合计 `149,829 B`。Shared 增加一份很小的程序化 Cloud Mesh，仍不新增 PNG。这些是 Source/YAML 大小，不冒充 Player Build 或 GPU 驻留。

## 5. WYSIWYG

世界/空间类型对比场景：`Assets/VFX/Preview/VFXPREVIEW_CoverageGalleryB_3x3.unity`。Screen/UI 全屏场景：`Assets/VFX/Preview/VFXPREVIEW_DamageWarningUI_Fullscreen.unity`。证据目录：`docs/vfx-reviews/coverage-gallery-b/evidence/current-run/`。两条链路都使用保存的 Camera 与自然 Update。

本轮工程候选只证明类型空间、生命周期和呈现边界可实施。用户动态视觉结论已记录在第 7 节；像素计数和机器门禁不替代该结论。

## 6. 最终工程验证

- Compile：通过；
- EditMode `CoverageGalleryBProductionTests`：`5/5`；
- PlayMode `CoverageGalleryBRuntimeTests`：`1/1`；
- Graphics PlayMode `CoverageGalleryBVisualCaptureTests`：`2/2`（九宫格 + Screen/UI 全屏）；
- 主帧九格全部有前景；3 秒帧为 7 个持续 Entry + 1 个审查用重播 Impact；下一周期重播态 `aliveEntries=9`；
- 当前无 Unity 进程和项目锁。

## 7. 用户最终视觉结论

- 签署日期：2026-08-25。
- 签署人：用户（本任务签署）。
- 验收对象：`VFXPREVIEW_CoverageGalleryB_3x3.unity` 与 `VFXPREVIEW_DamageWarningUI_Fullscreen.unity`。
- 结论：**有条件通过**。
- 用户原话：**“这个是通过的，但还是无法商用”**。

用户未指定具体 Scene、格子、时间阶段或解除限制的条件。本报告不推断“无法商用”的原因，不将该结论改写为无条件通过，也不据此授权重做或修改源码/资产。
