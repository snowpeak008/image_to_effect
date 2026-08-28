# S7 阶段纪要：运行时控制器与预览

> 状态：完成，主 Agent 独立验收通过  
> 执行日期：2026-08-22  
> 范围：S7 Runtime Controller、S6 接线、固定 2D 预览与证据；未进入 S8 Patch。

## 运行时接口与状态机

`VFXComposer.Runtime` 内的 `GeneratedVfxController` 是纯 Player 组件（程序集、全部 Runtime 源码均不含 `UnityEditor`）。公开的阶段接口为：

- `PlayLaunch()`
- `StartTravel()`
- `SetTravelTransform(Vector3, Quaternion)`
- `PlayImpact(Vector3)`
- `StopEffect(bool immediate)`
- `ResetForPool()`

只读 `CurrentStage`（`None / Launch / Travel / Impact`）和 `StageChanged` 事件使顺序可观测。每次阶段切换立即停止并清空其余根节点，且只激活目标根。`ParticleSystem` 以 `Play(true)` 启动；立即停止使用 `StopEmittingAndClear` + `Clear`，所有 `TrailRenderer` 都 `Clear`。非立即停止只 `StopEmitting`，根保持活动直到系统自然不再 `IsAlive`、Trail 没有 position 后自动隐藏。

`SetTravelTransform` 和 `ResetForPool` 都严格遵循“先设置位置、后 Clear Trail”的池化顺序。`SetTravelTransform` 是正式的连续移动接口：首次定位、非 Travel 阶段定位或位移超过序列化 `teleportClearDistance`（默认 1.75 world units）时才 Clear；Travel 中小步调用会保留 Trail。Runtime-only `VfxPreviewSequenceDriver` 也逐帧调用这个正式接口来执行 Launch → Travel → Impact，不包含伤害、碰撞或目标选择逻辑。

每个 Controller 同时序列化三根 Stage 引用与 `launchEnabled` / `travelEnabled` / `impactEnabled`。Compiler 依据稳定 Recipe stage ID 写入 `stage.enabled`，即使所有根在初始状态都 inactive，也不会让外部播放调用重新启用 Recipe 已禁用的阶段；调用被禁用阶段后状态为 `None`。

## 编译器与生成物

S6 `VfxCompiler` 现在在临时 Prefab 上用 `SerializedObject` 写入 Controller 的私有 `launchRoot` / `travelRoot` / `impactRoot`，随后才保存/验证/原子提交。阶段根默认关闭，避免模板的 `playOnAwake` 在外部调用前泄露。构建后验证会拒绝空、错误父节点或错误名称的序列化阶段引用。已有 S6 深拷贝、材质与原子回滚边界未改动。

## 预览入口

- 固定场景：[S7_2D_FireballPreview.unity](../../project/Assets/VFX/Preview/S7_2D_FireballPreview.unity)：中性灰正交相机、1-unit 参考线、蓝色起点/橙色终点、已生成火球实例。入口先询问保存当前已修改场景；固定场景已存在时直接打开，只有缺失时才创建，避免丢失用户场景或无谓重建。
- `Tools/VFX Composer/Preview`：`Open Fixed Preview Scene` 后进入 Play mode，可操作 `Launch Only`、`Travel Loop`、`Impact Only`、`Full Sequence`、`Reset`。
- Compiler 最小窗口增加 `Preview` 按钮以创建/打开该场景。
- `Tools/VFX Composer/Preview/Capture S7 Evidence` 使用不带 `-nographics` 的 batchmode `Camera.Render()` 走独立受控场景并重建可复核的静态阶段帧；这是帧采样，不声称为视频录制。
- 所有手动阶段按钮在改 Controller 前都会取消正在运行的 `VfxPreviewSequenceDriver` coroutine；因此 Reset 后稳定保持 `None`，不会被下一帧 Full Sequence 覆盖。

## 自动化与视觉证据

- EditMode 的真实 Compiler 集成测试断言默认 Recipe Build 后 Controller 三个序列化引用分别指向 `Launch`、`Travel`、`Impact`；既有 Runtime 边界测试递归检查 Runtime `.cs/.asmdef` 与程序集引用没有 `UnityEditor`。
- PlayMode（只引用 Runtime）覆盖独立 Launch/Travel/Impact、状态顺序、Travel 内小步 `SetTravelTransform` 保留 Trail、跨阈值瞬移在定位后清 Trail、立即 Stop 后粒子/Trail 均为 0、非立即 Stop 停发射并自然衰减、Reset 后重复播放、禁用阶段不播放，以及取消 Sequence 后 Reset 稳定。Runtime-only Sequence Driver 断言 Launch → Travel → Impact 的顺序和终点。
- 程序集静态边界 + 上述 PlayMode 组成“断开工具代码后仍运行”的等价证据；没有物理移动/禁用 Editor 目录，因此不会破坏工程资产导入状态。
- [docs/s7-evidence](../s7-evidence/) 包含 `launch.png`、`travel_start.png`、`travel_mid.png`、`impact.png`、`reset_after_move.png` 与 `sequence-trace.json`。肉眼复核：中段帧可见核心向终点方向的左向 Trail 与火星，Impact 帧可见同心 Shockwave + 径向 Burst，Reset 帧只有参考物、无跨屏 Trail。

## 实测命令

| 命令 | 结果 |
|---|---|
| `cmd /c tools\compile-check.bat` | 退出码 0 |
| `cmd /c tools\run-tests.bat EditMode` | 37 total / 0 failed |
| `cmd /c tools\run-tests.bat PlayMode` | 4 total / 0 failed |

各 Compiler 测试使用独立 `fireball_2d_s7test` Recipe ID，`TearDown` 只删除该测试输出并检查不存在 `vfxs6tmp_` 等临时目录；因此不会删除或换 GUID 于固定预览引用的 `Assets/VFX/Generated/fireball_2d/`。后者仅用于固定预览场景实例和人工 Preview，非测试残留。

## 已知边界

`PFT_2D_Embers` 是 World simulation space。非立即 `StopEffect(false)` 停发射后，已经出生的火星会在世界位置存活至其自然 lifetime 结束；这是预期表现，控制器不能把它们重新绑定到移动后的根。阶段切换、立即 Stop 与 Reset 都清空它们，因此不会在对象池复用时遗留。没有把静态抓帧描述为真实时间视频。

## 主 Agent 独立验收（2026-08-22）

结论：**通过 S7 退出门禁，可以进入 S8。**

- 第一版曾因 `SetTravelTransform` 每次清 Trail、Full Sequence 绕过正式 API、Recipe 禁用阶段仍可重新播放而退回；整改后小步 Travel 保留 Trail，大跨度位移在设置位置后清理，Sequence Driver 逐帧调用正式接口，stage enabled 也被序列化进 Controller。
- Preview 的手动阶段与 Reset 会先停止 Full Sequence coroutine；交互式打开固定场景会先处理未保存场景，已有固定场景只打开而不重建。
- 逐张复核 `launch`、`travel_start`、`travel_mid`、`impact`、`reset_after_move`：阶段位置与 JSON trace 一致，Travel 中段可见左向 Trail/Embers，Impact 同心可读，Reset 无跨屏残留。证据为可控帧采样，不冒充视频。
- 固定预览场景引用的 Prefab GUID `edfdb8327c7bd234c94f0f4338c35816` 与保留生成 Prefab 的 `.meta` 一致；生成目录未发现 temp、pending、backup 残留。
- 主 Agent 独立重跑：`compile-check` 退出码 0；EditMode `37/37`；PlayMode `4/4`。测试后无 Unity 进程，`git diff --check` 通过。
