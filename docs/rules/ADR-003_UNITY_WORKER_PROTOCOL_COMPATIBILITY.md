# ADR-003：Unity Worker 的共享协议兼容边界

状态：`Accepted for Phase 2 implementation`  
日期：2026-08-26  
影响范围：`src/VFXComposer.Protocol`、Phase 2 Broker/Worker IPC、后续 Worker commands  
不影响范围：冻结的 Unity 主界面、视觉签署、L3/L4、production registration issuer

## 1. 背景

ADR-002 已冻结“独立 Desktop + Broker + Unity Worker”的进程边界，并要求跨进程消息使用纯 C# DTO、strict JSON、exact schema、typed hash/self-hash 与稳定错误码。

当前 Desktop/Broker 侧 `VFXComposer.Protocol` 面向 .NET 8，唯一 wire ingress 使用 `System.Text.Json`。Unity 工程固定在 `2022.3.62f3c1`，现有 package 使用 Unity 自带的 `Newtonsoft.Json`，没有一份已经批准、锁定且通过 Unity 门禁的 `System.Text.Json` 依赖。直接把 net8 程序集复制进 Unity、临时联网取包、在 Worker 内重新发明另一套 DTO 语义，都会破坏 Phase 1 的依赖和协议门禁。

## 2. 决策

Phase 2 采用一个协议、两个运行时适配层：

1. `docs/schemas/desktop/*.schema.json`、稳定 token registry、typed-hash/self-hash 编码和跨运行时 golden vectors 是规范性 wire contract。
2. .NET 8 的 `VFXComposer.Protocol` DTO 与 `StrictWireCodec` 是 Desktop/Broker 的实现；它不作为 Unity 可直接加载的二进制作出承诺。
3. Unity Worker 只在新建的 `Editor/W24/S6/Worker/Protocol/` 子目录内实现窄的 protocol adapter，使用现有 `Newtonsoft.Json`，不得依赖或扩展冻结的 `VfxStudioWindow`/五标签页。
4. Unity adapter 只接受 Phase 2 Worker 所需的 exact message kinds。它必须在 DTO 构造前执行 strict UTF-8、BOM/重复 decoded key/unknown/missing/wrong-type/深度/节点/字节预算、token registry、typed hash/self-hash 和版本检查。
5. Unity adapter 的类型是 wire projection，不是 authority。解析成功、schema 成功、route string 或 UI 状态都不能产生 project lease、machine/visual verdict、用户签署、L3 或 L4。
6. 每个跨运行时 message kind 必须有同一组规范性 schema、.NET 正负例、Unity 正负例和 byte-for-byte golden vectors。任一侧新增字段、版本或规范化规则，必须先更新 schema/version 和双侧门禁；不得单边放宽。
7. Worker 只从已认证 Broker session 接受 process-local handle grant。Desktop-visible lease descriptor不得承载 native handle 值；handle grant 必须使用独立的 Worker-only message kind/schema，并绑定 Worker session ID/process epoch、Broker/registration/lease generations、project identity 和 handle-root identities。
8. Handle grant 必须显式协商 `worker.handle-lifecycle.v1`。状态从 `Prepared` 进入 `GrantPublished` 后有两条合法分支：正常分支经 `GrantAcknowledged` 再进入 `RevocationPending`；安全取消分支可在 grant ACK 到达前直接从 `GrantPublished` 进入 `RevocationPending`。若 Worker 仍存活，两条分支都必须经 `RevokePublished` 和 exact revoke ACK 进入 `Revoked`；进入撤销分支后的迟到 grant ACK 必须拒绝。唯一无 ACK 收口分支，是从 `RevocationPending` 或 `RevokePublished` 观察到同一 pinned process object 已终止后直接进入 `Revoked`，不得凭 PID、超时或 session 文本推断。Broker 仅为同一 live Worker session 保留有界、identity/self-hash 完全绑定的 revoke-ACK tombstone，以允许 exact ACK retry，任意变体必须失败。Worker ACK revoke 前必须已关闭 exact handles；Broker 对任何已发布的裸句柄号都不得再使用 `DUPLICATE_CLOSE_SOURCE`。

## 3. 不采用的方案

- 不把 net8 `VFXComposer.Protocol.dll` 直接放入 Unity。
- 不在本阶段把整个 Protocol 降级或多目标编译到 `netstandard2.1`；现有 immutable/frozen registry、strict codec 与经过审计的 .NET 8 行为不得为兼容性而降格。
- 不从网络临时安装 `System.Text.Json` 或其他 IPC/serialization 包。
- 不让 Unity adapter 接受 caller absolute path、drive letter、UNC、ADS、device namespace、环境变量、`EditorPrefs` 或 `Application.dataPath` 作为 registration trust root。
- 不复用旧 S6 MCP envelope、旧 LocalReadOnlyFilesystemAdapter 或 r35/r36 receipt 作为新 Worker/Broker 的直接运行证据。

## 4. 文件所有权

规范性与 .NET 侧：

```text
src/VFXComposer.Protocol/
docs/schemas/desktop/
eng/verify-phase2-schemas.py
```

Unity 侧只允许新增：

```text
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Protocol/
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerProtocolTests.cs
```

Broker 的 Worker-only transport/grant 位于：

```text
services/VFXComposer.Broker/Ipc/
services/VFXComposer.Broker/Registration/
services/VFXComposer.Broker/Queries/
```

## 5. Phase 2 admission gate

在 production connection 可开放前，至少必须同时满足：

- Worker-only handle grant/ACK/revoke/ACK 各有 exact schema，并与 Desktop-visible lease descriptor 类型隔离；
- .NET 与 Unity 对所有 admitted Phase 2 messages 使用相同 golden bytes、typed hashes 和 self-hashes；
- Unity exact-filter 在隔离 shadow 中运行，XML/log 绑定当前源码，且自然退出；
- malformed/oversized/duplicate/unknown/version/hash negatives 在任何 native handle 使用或项目内容读取前拒绝；
- stale Worker epoch、Broker generation、lease generation、cross-project identity 和 revoked handle 在读取前 fail closed；
- reordered/replayed/substituted lifecycle messages 与 live-process session loss 不会触发裸句柄号关闭或 lease 复活；
- Worker 的所有项目访问只从 Broker 交付的 pinned handle 相对打开，不回退 path；
- Desktop/Client 对 Unity 工程仍保持零直接读写；
- production Broker policy/ACL/issuer 另行通过独立安全审计。

当前 Broker `Program` 仍必须在创建 listener、解析请求、查询路径或打开项目之前返回 `W24FS001`。本 ADR 只允许继续实现和测试 Phase 2 foundation，不授予 production read、Worker command、写入、视觉或 authority。
