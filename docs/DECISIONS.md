# VFX Composer 决策定版

> 状态：已定版；W24 变更控制已追加（2026-08-26）  
> 依据：[S1 技术贯通 Spike 纪要](spike-notes/S1_SPIKE_REPORT.md)、[S2 AI 环节小样纪要](spike-notes/S2_AI_REPORT.md) 与 [执行计划 §3.1](EXECUTION_PLAN.md)。本文件关闭 `DEVELOPMENT_PLAN.md` §21 与 `DESIGN_PLAN.md` §21 的全部开放项；后续变更须经变更控制并更新本文件。

## 工程与资产决策

| 来源 | 开放项 | 结论 | 依据与边界 |
|---|---|---|---|
| 开发 §21.1 | Unity 版本 | **Unity 2022.3.62f3c1 + URP 14.x** | 锁定 Editor `E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`。不采用 Unity 6。 |
| 开发 §21.2 | 首个目标平台 | **PC Editor 预览** | 保留 Recipe 的 `mobile_medium` profile 字段作为静态预算档位；首版不做 Android 或其他真机验收。 |
| 开发 §21.3 | 生成物依赖 Runtime 包 | **接受** | 生成 Prefab 可依赖最小 `VFXComposer.Runtime`；Runtime 不得引用 `UnityEditor`。 |
| 开发 §21.4 / 设计 §21.6 | Managed 资产手改 | **允许查看、调试和 Inspector 手改；重建覆盖** | 首版明确显示 Managed 语义；不做自动保留手改。 |
| 开发 §21.5 / 设计 §21.6 | Detach/Bake | **P2，首版不做** | 需要手改永久保留时，待稳定化后单独设计。 |
| 开发 §21.6 / 设计 §21.4 | 2D 拖尾 | **TrailRenderer** | S1 未新增该项实测，按保守默认选一个实现；必须由模板层封装，Particle Trail 以后可增补。 |
| 开发 §21.7 | 自动截图 | **首版不做** | 只提供编辑器预览。 |
| 执行 §3.1 | 组装策略 | **深拷贝** | S1 证明 Nested Prefab 会在不重建时传播模板变化；深拷贝维持“显式 Build 才改变生成物”的边界。 |
| 执行 §3.1 | 重建与 GUID | **同路径就地覆盖，保留 `.meta`/GUID** | S1 证明覆盖 `SaveAsPrefabAsset` 可保持场景引用；禁止删除后新建受管 Prefab。 |
| 执行 §3.1 | 幂等判断 | **规范化输入语义 hash** | Recipe + 模板/编译器版本不变时跳过写盘；首次材质浮点序列化不以字节差异作为唯一依据。 |
| 执行 §3.1 | JSON Schema | **不引入通用 Schema 验证库** | C# 手写结构与语义验证；`.schema.json` 仅是 AI 可读文档。 |

## 视觉与产品决策

| 来源 | 开放项 | 结论 | 依据与边界 |
|---|---|---|---|
| 设计 §21.1 | 首版风格 | **轻度卡通（stylized）** | 与现有火球 Recipe 的 `style: stylized` 对齐；正式视觉金样在 S5 定版。 |
| 设计 §21.2 | 第一目标画面 | **通用正交预览** | PC Editor 的固定 2D 预览场景；不在首版绑定横版或俯视实际游戏镜头。 |
| 设计 §21.3 | HDR Bloom | **URP Bloom 默认关闭** | 首版视觉不得依赖 HDR/Bloom；后续可将其作为可选展示条件评估。 |
| 设计 §21.5 | 默认火球主体 | **Sprite + 粒子混合** | 单一视觉职责模板：Sprite Core 为主体，粒子用于火星/爆发等辅助层。 |
| 设计 §21.7 | 工具 UI | **最小按钮面板** | 仅导入、Validate、Dry Run、Build、Preview 与报告文本区；完整三栏窗口推迟。 |
| 设计 §21.8 | 视觉金样素材 | **正式可用的本地资产** | S5 模板与金样须可进入正式 2D 闭环；不以临时占位物作为验收成品。 |
| 设计 §21.9 | 3D 范围 | **MVP 最后阶段** | 仅在 S4–S9 的 2D 闭环全部通过后进入 S10；不提前为 3D 扩张 Recipe。 |

## 已关闭的执行默认项

- 首版无真机性能门禁；预算报告只标注为静态预检。
- 第一版采用文件工作流，MCP 后置。
- 2D 先行、3D 后置；VFX Graph、云端资产生成和其他引擎不进入当前范围。

## W24 授权变更（2026-08-25）

以下用户授权决策优先于上方 MVP 首版默认项，但不撤销与其无冲突的工程边界：

| 决策 | 结论 | 边界 |
|---|---|---|
| 自动截图与证据 | **W24 起改为强制自动 Continuous Capture** | 使用正式场景和序列化相机；Beauty、最小 Diagnostic Pass、Telemetry 与完整元数据/hash；禁止手工最佳单帧冒充证据。此项明确取代“自动截图首版不做”。 |
| 状态等级 | **采用 L0–L4 与 `VISUAL_PENDING` 工作状态** | 没有 Visual QA 看帧最多 L2；只有用户可签署 L4。`VISUAL_PENDING` 允许源码开发连续推进，但不是质量等级。 |
| 视觉验收时机 | **全部内容开发后集中由用户验收** | 中间阶段不等待用户看图；机器门禁完成后继续开发。未签署条目不得称为 production ready。 |
| 当前最高优先级 | **W24 S0a→S6** | 四条垂直基线、设计合同、视觉 QA 与证据链优先于继续批量扩类型。 |
| 外部依赖 | **S0/S1 不新增第三方包** | 使用现有 Unity 2022.3、URP、Python、NumPy/Pillow；MCP/VFX Graph 仅按 W24 后续隔离评估。 |

## W24 S6 独立桌面架构变更（2026-08-26）

以下决策由用户 STOP-THE-LINE 明确批准，优先于“完整 UI 推迟”及任何继续扩展 Unity Editor 主界面的旧默认项；详细规范见 `rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md`。

| 决策 | 结论 | 边界 |
|---|---|---|
| 用户主界面 | **独立 .NET 8 + Avalonia + MVVM Desktop** | Unity Editor UI 冻结为兼容/诊断基线；对等验收和用户确认前不删除。 |
| Unity 职责 | **Unity Package 作为受控 Worker** | Prefab/Scene/Material/ParticleSystem、导入、构建、播放、渲染、测试仍只在 Unity 内执行。 |
| Shared Protocol | **纯 C# DTO + strict JSON/schema/hash/self-hash** | 不依赖 UnityEngine/UnityEditor；UI 布尔值、route 字符串和 caller JSON 不产生 authority。 |
| 安全边界 | **Broker 对 disconnected/test 可选；Windows production-connected profile 必经** | Desktop↔Broker 与 Worker↔Broker 分别 authenticated named pipe；Broker 是唯一 registration issuer。无可信 issuer 时继续 W24FS001，禁止 Desktop→Worker fallback；替代 trusted host 必须新 ADR 与独立审计。 |
| 工程写入 | **Desktop 永不直接写 Unity 工程** | 所有写入走版本化命令、预检、用户确认、幂等、内容哈希和 Worker 事务。 |
| 禁止 transport | **无公网 HTTP、任意 TCP、production stdio MCP** | Broker/Worker 协议冻结并通过独立审计前不得为了可用性绕过。 |
| 证据绑定 | **Desktop/Broker/IPC/Worker 新证据独立取得** | r31/r32/r35/r36 仅为旧 Unity UI/兼容回归，不能冒充新架构证据。 |
