# 运行时交互与组合能力九宫格生产报告

日期：2026-08-24  
状态：第三轮候选完成；用户于 2026-08-25 签署为**有条件通过**，不等同于无条件视觉通过或商用认可

本报告按 `docs/rules/70_ITERATION_EVIDENCE_AND_LEARNING.md` 记录每轮目标、修改、验证和人工结论。前两轮未形成最终候选；第三轮是本次用户签署的候选。

## 工程迭代 1

- 结果：Compile 失败，未生成候选。
- 错误：`InteractionGalleryVfxController.cs` 两处隐式类型多变量声明触发 `CS0819`。
- 根因：使用了本项目 Unity/C# 编译器不接受的 `var a,b,c` 写法。
- 修改：拆成单独、明确的局部变量声明。
- 经验：一次性压缩代码不能牺牲目标 Unity 编译器兼容性；首次真实 Compile 前不得宣称实现完成。

## 工程迭代 2

- 结果：Compile 再次失败，仍未生成候选。
- 错误：Visual Capture 测试残留一处隐式多变量声明；Compiler 缺少 `VFXComposer.Editor.Validation` 引用。
- 修改：测试变量逐项声明；补齐 `RecipeCanonicalizer` 的命名空间。
- 防回归：后续新增 Gallery 在首次编译前对 `var ...,...` 做静态扫描，并显式核对 Domain/Validation 引用。

## 视觉迭代 1

- 工程结果：Compile 通过；EditMode 3/3；Runtime 1/1；Graphics 1/1。
- 人工结果：拒绝。证据移动到 `evidence/rejected-run-20260824-visual-1/`。
- 失败：Homing 运动头/尾迹弱；Dash 位移 streak 不可读；Transform 三层挤在中心；Multi-stage 残留提前出现；Channel/Chain 端点未真正驱动线段。
- 修改：提高 Homing 头与 Trail；Dash 改快速区间并增加主 streak；Transform 拉开原形/新形；Multi-stage 严格阶段互斥；Channel/Chain 的线段改由动态端点驱动。

## 视觉迭代 2

- 工程结果：Graphics 1/1，但人工拒绝；证据移动到 `evidence/rejected-run-20260824-visual-2/`。
- 失败：Transform 与 Multi-stage 的后续阶段仍提前出现，Dash 阶段窗口也不准确。
- 根因：把 Unity C# `Mathf.SmoothStep(from,to,t)` 错当成 Shader `smoothstep(edge0,edge1,value)`。
- 修改：新增 `Smooth01`（`InverseLerp + SmoothStep(0,1,t)`）和 `SmoothWindow`，所有阶段阈值统一使用明确的 0–1 门控。
- 防回归：Shader 阈值逻辑移植到 C# 时禁止直接照抄三参数 SmoothStep；必须对阈值前、阈值中、阈值后各断言一次。

## 工程迭代 3

- 结果：最终回归执行流程失败，不归因于产品代码。
- 现象：Compile 与 PlayMode 被错误并行启动到同一个 Unity 项目；PlayMode 退出且无 XML，Compile 完成 quit 后进程未及时结束。
- 处理：验证精确 PID、项目路径和 batchmode 参数后，只终止卡住的 PID；不接受该轮结果。
- 修改：同一 Unity 项目的 Compile/EditMode/PlayMode/Build 必须串行执行，后续重新完整运行。

## 视觉迭代 3（当前候选）

- 证据：`evidence/current-run/`，保存 Camera、自然 Update、60 fps；关键帧为 0.3 / 1.5 / 3 / 4 / 5.5 秒。
- 人工审查：达到用户验收边界，但不代替用户结论。
- 时序：Dash 在 1.5 秒显示主 streak、3 秒抵达；Transform 按原形/碎片/新形接力；Multi-stage 按蓄力/飞行/命中/残留接力；Channel/Chain 线段跟随动态端点。
- 资源：九个正式输出目录合计 `132,660 B`；新增 PNG 为 `0 B`，复用已批准的共享 Shader/Material/Mesh。

## 当前验证

- Compile：通过（串行复跑）；
- EditMode `InteractionGalleryProductionTests`：`4/4`；
- Runtime PlayMode `InteractionGalleryRuntimeTests`：`1/1`；
- Graphics PlayMode `InteractionGalleryVisualCaptureTests`：`1/1`；
- 5.5 秒重播后九项全部重新存活；
- 当前无 Unity 进程和项目锁。

历史失败证据保留在 `evidence/rejected-run-20260824-visual-1/` 与 `evidence/rejected-run-20260824-visual-2/`。当前候选的用户动态结论已记录在下文；机器结果不替代该结论。

## 用户最终视觉结论

- 签署日期：2026-08-25。
- 签署人：用户（本任务签署）。
- Scene：`Assets/VFX/Preview/VFXPREVIEW_InteractionGallery_3x3.unity`。
- 结论：**有条件通过**。
- 用户原话：**“这个也看过了，内容可见，但不可商用”**。

用户未指定具体格子、时间阶段或解除限制的条件。本报告不推断“不可商用”的原因，不将该结论改写为无条件通过，也不据此授权重做或修改源码/资产。
