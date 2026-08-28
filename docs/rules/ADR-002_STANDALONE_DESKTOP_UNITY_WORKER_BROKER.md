# ADR-002：独立桌面主界面、Unity Worker 与可选安全 Broker

状态：`Accepted`  
日期：2026-08-26  
决策人：用户（STOP-THE-LINE 架构决策）  
影响阶段：W24 S6、后续 Desktop/Worker/Broker、安装与最终验收

## 1. 背景

W24 已在 Unity Editor 内建立 Library、Create、Preview、Patch、Review 五标签页及一组只读模型、Preview 防护和结构化外部操作协议。r31/r32/r35/r36 为这些既有能力留下了隔离运行证据；最新 r36 对当前 registration/read/model/callback 源码记录 `104/104`，零 failed、skipped、inconclusive，五个 Unity 进程均自然退出 `0`。

这些结果证明的是 Unity 内兼容/诊断面，并不证明独立桌面软件、Broker、IPC、生产 real-read、Player UI、像素渲染、视觉 QA、用户签署或 L3/L4。

继续把用户工作流、MCP 入口和 UI 复杂度堆进 Unity Editor 会把展示、信任、文件系统、Unity 执行权限与生命周期耦合在同一进程。用户因此下达 STOP-THE-LINE：停止 Unity Editor 主界面的新增功能，把最终产品方向改为独立桌面主界面，同时保留 Unity 作为受控执行器。

本决策是方向变更，不是回滚。

## 2. 决策

采用以下目标架构：

```text
VFXComposer.Desktop (.NET 8 + Avalonia + MVVM)
        |
        | authenticated named pipe; versioned local protocol
        v
VFXComposer.Broker (required for the Windows production-connected profile)
        ^
        | separately authenticated Worker session; pinned capabilities
        v
com.vfxcomposer.unity / Unity Worker
        |
        v
Unity AssetDatabase / Prefab / Scene / Material / ParticleSystem / render / tests
```

职责固定为：

- Desktop 是用户日常主界面，负责导航、编辑意图、状态展示、确认、作业观察和证据浏览。
- Shared Protocol 是纯 C# DTO、严格 JSON、schema、typed hash/self-hash、版本握手与稳定错误码；不得依赖 `UnityEngine` 或 `UnityEditor`。
- Client 封装连接、握手、请求关联、幂等和断连恢复；不拥有 Unity 工程写权限。
- Broker 对 disconnected Desktop、纯协议开发和测试夹具是可选部署；对 Windows production-connected profile 是必经的独立安全进程与唯一 registration issuer。任何替代 trusted host 都必须由新 ADR 和独立安全审计批准；禁止 Desktop 直连 Worker 的降级路径。Windows 第一阶段只允许 authenticated named pipe；不得开放公网 HTTP、任意 TCP 或 production stdio MCP。
- Unity Worker 留在 `project/Packages/com.vfxcomposer.unity`，独占 `AssetDatabase`、`EditorSceneManager`、Prefab/Scene/Material/ParticleSystem、渲染和 Unity Test Runner 能力。
- Desktop 不直接写 `Assets`、`Packages`、`ProjectSettings` 或正式证据目录。所有 Unity 改动必须经过版本化命令、预检、用户确认、幂等键、内容哈希和 Worker 事务。

## 3. 源码与目录所有权

Phase 0 审计通过后才可创建以下四个新仓库源码根：

```text
apps/VFXComposer.Desktop/
src/VFXComposer.Protocol/
src/VFXComposer.Client/
services/VFXComposer.Broker/
```

`project/Packages/com.vfxcomposer.unity/` 已经存在，不是新源码根。后续 Worker 只可在计划列明的 `Editor/W24/S6/Worker/` 等新子路径内增量实现；这不授权移动、重写或扩展冻结的 Unity UI。

所有权规则：

| 根 | 允许依赖 | 禁止依赖/能力 |
|---|---|---|
| `src/VFXComposer.Protocol` | .NET BCL、明确冻结的序列化/哈希实现 | Unity、Avalonia、文件系统执行、IPC listener |
| `src/VFXComposer.Client` | Protocol、受控 IPC client abstraction | Unity、项目绝对路径、工程写入 |
| `apps/VFXComposer.Desktop` | Protocol、Client、Avalonia/MVVM | Unity assemblies、直接工程写入、authority issuer |
| `services/VFXComposer.Broker` | Protocol、OS 安全/IPC primitives | UnityEditor、caller 任意路径、公共网络 listener |
| Unity Package | Protocol-compatible DTO/adapters、Unity APIs | Desktop UI ownership、外网 server、自动视觉签署 |

如仓库构建规范要求不同命名，必须先修订本 ADR 或记录后续 ADR；不得静默搬迁已有文件。

## 4. 旧 Unity UI 的冻结边界

立即生效：

- 不再扩展 `VfxStudioWindow`、五标签页、Player UI、美化或 Unity 窗口内的 MCP 操作入口。
- 只允许修复编译失败、数据损坏或安全回归。
- 每个获准的窄修必须单独记录原因，重新冻结受影响 SHA，取得新的兼容回归收据，并把旧字节与旧收据保留为 predecessor；不得把新收据反向绑定到旧字节，也不得借“KEEP”跳过安全修复。
- 既有源码、测试和证据保留为兼容/诊断基线；Desktop 达到功能对等并通过门禁前不得删除。
- Desktop 对等后，旧 UI 只能在用户确认后降为 hidden diagnostic fallback；不立即删除。
- 旧 r31/r32/r35/r36 证据不得绑定或冒充 Desktop、Client、Broker、IPC 或新 Worker API 的证据。

冻结基线：

| 项 | SHA-256 |
|---|---|
| `VfxStudioWindow.cs` | `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587` |
| `VfxStudioModels.cs` | `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68` |
| `W24S6EditorIntegrationTests.cs` | `d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6` |
| `W24S6StudioModelsTests.cs` | `28188ffcbd31471876c137be3023aa668f93327a506dd4862c06d355e372d33d` |

运行与完整哈希清单以 `docs/stage-notes/W24_S6_UI_REPORT.md` 的 r36 段为准。

## 5. 协议不变量

所有跨进程请求与结果必须满足：

- 通用 envelope 显式包含 `protocolVersion`、`messageKind`、`requestId`；握手前消息不得伪造尚未建立的项目身份。
- project-bound 查询或命令另外必须包含由有效 lease 绑定的 `projectIdentityHash`；有副作用、可重放或作业型消息另含 `idempotencyKey`。这些 caller 字段只作关联与重放约束，不是信任根。
- exact schema；unknown、missing、wrong-type、duplicate decoded key、非法数值和孤立 surrogate 一律拒绝。
- 请求与结果分别有 typed canonical hash；需要持久化的对象另有 self-hash 和物理文件 hash。
- capability negotiation 是 allow-list 交集，不得把未知能力当作向后兼容。
- 稳定错误码与不回显秘密/绝对路径的诊断；异常文本不构成协议。
- route/verdict 字符串、caller 布尔值或 UI 勾选永不产生 authority。
- 任何 Machine、Visual、用户签署、L3、L4 状态必须保持类型与来源分离。

Phase 1 的 Shared Protocol 只定义不产生副作用的握手、状态和查询 DTO；命令 authority 留到后续门禁。

## 6. 项目注册与信任根

当前 production registration 继续 `REGISTRATION_ISSUER_PENDING / W24FS001`，并在 request parse、路径/盘符查询和文件打开之前失败关闭。

禁止作为信任根：

- caller 路径、caller `projectIdentityHash` 或注册 JSON；
- `EditorPrefs`、环境变量、当前目录、`Application.dataPath`；
- 仓库或 `ProjectSettings` 内可由同一用户改写的文件；
- `X:`、`GetDriveTypeW` 或 `QueryDosDeviceW` 的瞬时结果；
- 同一 Unity 进程的普通 static token。

未来 production issuer 必须由独立 Broker principal 持有。Broker 从 global/native volume namespace 建立根身份，钉住 volume GUID/serial 与 `FILE_ID_128`，但不枚举、解析或读取 Unity 项目内容。它只向已经通过独立 Worker peer authentication 的精确 PID/process epoch 交付 non-inheritable、只读、无 write/delete/`WRITE_DAC`/`WRITE_OWNER` 的 handle capability。Worker 独占项目内容读取、写入与 Unity API；reader 只允许从已钉住 root handle 逐段相对打开，不得回退盘符绝对路径。

## 7. IPC 与进程边界

Windows 第一阶段：

- 使用本机 authenticated named pipe。
- Desktop↔Broker 与 Worker↔Broker 是两个独立的 authenticated named-pipe session；不存在 Desktop↔Worker 直连或 Broker 缺失时的 fallback。
- pipe ACL 只允许预期用户/service SID；两端验证 peer SID、PID、批准的进程映像身份、process epoch、Broker generation 和协议版本。
- Broker 维护 Worker session registry；handle duplication 只能指向已认证 session 的精确 PID，并与 project lease、volume/file identity、Worker epoch 和 Broker generation 一起绑定。PID/epoch/image/generation 任一漂移即撤销 lease 和在途请求。
- 不监听 TCP；不自动穿透防火墙；不联网安装依赖；不提供 public HTTP。
- 连接建立不等于项目注册；无有效项目 lease 时所有工程请求 fail closed。
- Broker/Worker 崩溃、重启、generation 变化或 lease 撤销使在途请求失效；Desktop 必须显示 disconnected/retryable 状态，不伪造成功。

## 8. 写入与事务

Desktop 永不直接写 Unity 工程。未来 Worker 写命令必须具备：

1. exact versioned command；
2. frozen project/candidate/contract/build identities；
3. preflight 与完整写集；
4. 明确用户确认策略；
5. request ID + idempotency key；
6. staging、二次 replay、单点 commit；
7. crash recovery 与 invocation-owned rollback/quarantine；
8. 结构化进度、日志、产物哈希和稳定错误码；
9. 失败无 partial promotion；
10. Desktop 不获得 `AssetDatabase` capability。

## 9. 信息架构

Desktop 主导航冻结为：

- Dashboard / Project connection
- Library
- Create / Recipe editor
- Preview / Playback
- Patch / Diff
- Review / Evidence
- Jobs / Logs
- Settings / Diagnostics

旧五标签页到新导航的逐项迁移见 `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md`。

## 10. 失败关闭顺序

通用顺序：

1. 无可信 Broker/Worker/registration lease：返回 disconnected/pending；零工程 I/O。
2. 握手、版本、capability、peer identity 任一不符：关闭连接；零工程 I/O。
3. strict parse 与 exact schema。
4. 校验 project identity、request ID、幂等键与 typed hash。
5. 校验命令 allow-list、所需 capability 与用户确认状态。
6. Worker 内 preflight、所有权、依赖、预算、状态与写集检查。
7. 事务执行；输出仅在二次验证后 commit。
8. 结果由独立 verifier 重放；UI 只显示已验证来源的状态。

任何中间失败不得退回 Desktop 直写、caller path、EditorPrefs、环境变量、盘符重开、公共网络或自动 authority。

## 11. 威胁模型

必须防御：

- 任意绝对路径、UNC、ADS、device namespace、DOS-device remap、junction/symlink/reparse、hardlink 与 TOCTOU；
- 同用户进程篡改注册文件、named-pipe 抢占/降级、旧进程/旧 generation 重放；
- duplicate request、跨项目 request、旧 build/capture/evidence 身份重放；
- malformed/oversized JSON、重复键、深度/节点/数字资源耗尽；
- Worker/Broker/Desktop 任一崩溃导致 partial write 或幽灵成功；
- 日志泄漏秘密、绝对路径、令牌或 answer-bearing calibration 数据；
- UI 把 machine pass、Visual QA、用户 verdict、L3/L4 混为同一布尔状态。

当前不宣称防御已完成；Phase 2–5 分别建立运行证据。

## 12. 非目标

- 不自研 Unity-free renderer。
- 不让 Desktop 直接改 Unity 工程。
- 不在当前阶段开放外网 server、production stdio/HTTP MCP 或任意 TCP。
- 不自动判定视觉通过、用户 verdict、L3 或 L4。
- 不用旧 Unity UI 收据冒充新架构证据。
- 不在缺少独立 Broker issuer 时开放 production real-read。
- 不因方向变更删除既有 Unity UI 或证据。

## 13. 分阶段门禁

- Phase 0：ADR、文件级计划、迁移矩阵、威胁模型完成；独立审计 P0/P1=0；没有旧证据误绑定。
- Phase 1：Desktop 可在 Unity 未安装/未运行时启动并明确显示 disconnected；无网络监听、无工程写入。
- Phase 2：可信项目注册和只读连接通过 junction/UNC/ADS/DOS-remap/TOCTOU/handle lifecycle 门禁。
- Phase 3：Worker 命令幂等、事务、取消与崩溃恢复通过；失败无 partial promotion。
- Phase 4：Preview/Review 绑定精确产物身份；无像素证据不得显示视觉通过。
- Phase 5：安装、连接、构建、预览、审查、断连恢复端到端通过；用户再决定旧 UI 是否隐藏。

2026-08-27 用户调度例外：在 Phase 2 production read gate 尚未关闭时，可以提前冻结未来 Phase 3 的纯 C# command/job DTO、exact schema、strict codec 与 golden vectors，且只能由唯一 Protocol writer 在独立工作包内完成。这不启动 Phase 3 runtime，不允许 Client/Broker/Unity Worker transport、handler、project read/write、transaction、UI action 或 authority；Unity Worker command implementation 必须等待该 Protocol 包冻结并通过独立审计，production command transport 仍必须等待 Phase 2 gate。

## 14. 后果

正面：

- 用户体验与 Unity Editor 生命周期解耦。
- Unity 权限集中在 Worker，Desktop 保持低权限。
- Broker 可形成真正的独立安全边界。
- Protocol 可独立测试并保持跨平台潜力。

代价：

- 需要新的 solution、IPC、安装、升级、Broker 生命周期和端到端测试。
- 旧 UI 与 Desktop 在迁移期并存，需要明确功能矩阵和双重回归。
- 可用 Desktop + Worker 预计 2–4 周；production Broker、安全门禁、安装和完整验收预计 3–5 周；视觉返修另计。
- 架构切换后的总体完成度按用户重估为约 65%–70%，不得沿用旧 UI 完成度。

## 15. 回滚与修订

本 ADR 不允许“回滚”为继续扩展 Unity 主界面。若 Desktop 技术基线或 Broker 形态变化，必须新建 ADR 并保留本记录。任何临时开发便利不得解除 W24FS001、视觉签署、L3/L4 或项目写入边界。
