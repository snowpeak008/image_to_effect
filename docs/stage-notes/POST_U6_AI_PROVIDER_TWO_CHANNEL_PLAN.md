# POST-U6 AI Provider 双通道实施计划

> **Historical — superseded by U6/A6 closeout（2026-08-29）**：本文件是 A0 时点的计划输入，下文"A1 `ACTIVE`"、"A2–A6 `NOT STARTED`"等状态已全部过时——A0–A6 现已全部关闭（A6 `CLOSED — FINAL ACCEPTED/GO — P0/P1/P2=0/0/0`，ADR-006 以 `100/100` 验收），无活动 AI 工作包。现行规范与验收状态以 `docs/rules/ADR-006_AI_PROVIDER_TWO_CHANNEL_ROUTING.md` 与 `docs/coordination/W24_EVIDENCE_INDEX.md` 为准；后续开发的唯一顺序依据是 `docs/plans/OPTIMIZATION_MASTER_PLAN.md`（P0/R/O/F 系列任务卡），日常任务验收按 `docs/plans/CODING_STANDARDS.md`，不再要求逐任务 receipt。

> **状态：A0 已关闭；A1 `ACTIVE`，但必须按 2026-08-29 用户产品决定重构后重新验收。** 本文件是只改文档的计划重基线；不实现 AI、网络、设置 UI、Broker、Worker、Unity、项目写入或 runner 改动。
>
> **计划输入：** 两个独立 API 通道、用户可自由填写和保存的 Provider endpoint，以及固定版本 [`snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`](https://github.com/snowpeak008/Tom_doc/tree/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf) 的非敏感导入设计参考。

## 1. 位置、DAG 与当前状态

本计划是 ADR-005 USER_MODE 路线关闭后的独立产品链，不能反向成为 U0–U6 的依赖、验收证据或生产 GO 理由。精确依赖为：

`A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`

*以下为 A0 时点的状态快照（historical — superseded by U6/A6 closeout）：A1–A6 现已全部关闭，最终状态见顶部标注与 `docs/coordination/W24_EVIDENCE_INDEX.md`。*

| 节点 | 当前状态 | 边界 |
|---|---|---|
| A0 `AI_PROVIDER_TWO_CHANNEL_ROUTING` | `CLOSED — DOCS ONLY` | ADR-006 与七份控制文档的产品决定重基线；不重跑项目 gate。 |
| A1 `AI_PROVIDER_FOUNDATION` | `ACTIVE` | `OpaqueEndpoint` 合同、保存/解析、脱敏与 Gateway 基础；没有真实 Chat/Image HTTP。 |
| A2 Chat adapter | `NOT STARTED` | 对已显式绑定的 Chat route 做调用时 best-effort endpoint 解释。 |
| A3 Image adapter | `NOT STARTED` | 对已显式绑定的 Image route 做调用时 best-effort endpoint 解释。 |
| A4 Desktop wiring | `NOT STARTED` | 仅通过 Gateway 与受限设置管理接口。 |
| A5 mock E2E | `NOT STARTED` | 受控 mock 证据。 |
| A6 independent audit | `NOT STARTED` | 只读审计 A0–A5 冻结字节。 |

此前 A1 独立审阅的 `NO-GO — P0/P1/P2=0/0/1` 被新的用户需求 **supersede**：它不是 GO，也不删除历史审阅记录。A1 仍为唯一 `ACTIVE` 包，但旧的 endpoint admission 实现不得作为关闭证据；必须改成 `OpaqueEndpoint` 加 A2/A3 调用时 adapter parsing，并重新验证。

## 2. 双通道是路由边界，不是 endpoint 限制

| 通道 | 唯一用途 | 强制绑定 |
|---|---|---|
| `ChatLlm` | 所有对话、提示词辅助、LLM 文本与结构化文本请求。 | 一个显式 `ChatLlm` binding。 |
| `ImageGeneration` | 所有生图请求与后续受控下载/缓存。 | 一个显式 `ImageGeneration` binding。 |

每个 binding 精确指向一个 `ProviderProfile`、一个 capability、一个 model 和一个显式 `ProtocolId`。同一 profile 可以同时声明 chat 和 image capability，但用户必须分别选择、确认和展示两个 binding。`Official`、`Relay`、`Friend`、`Subscription`、`Custom` 只是 Origin 元数据，不能自动决定协议、adapter、认证、模型、endpoint 或第二选择。

功能代码只能调用 `IAiGateway` 的 channel-specific request。它不能持有或传入 endpoint、profile、adapter、header、credential、model override 或 fallback candidate。禁止按名称、Origin、模型、环境、PATH、导入字段、上次成功结果或 endpoint 文本推断路由；也禁止 unknown protocol 回落到任意兼容 adapter。

失败不得转去另一个 profile、model、endpoint、adapter、protocol 或 channel。以后若有有限重试，也只能重试已经解析出的同一路由和同一请求，并保持可取消。

## 3. 用户 endpoint 合同：`OpaqueEndpoint`

`OpaqueEndpoint.Value` 是用户拥有的 opaque configuration string。用户可以自由填写、编辑和保存官方、中转、朋友提供、订阅相关或完全自定义的 API 地址。值可以含 scheme、host、port、user-info、path、query、fragment，也可以根本不是可解释的 URI；这些情况均不阻止本地配置保存或 resolver 返回配置快照。

配置 JSON Schema 与本地严格读取器只检查：

- 聚合文档结构、字段类型、必填/可空规则、版本和 revision；
- 未知/重复字段及损坏配置的 fail-closed 处理；
- 合理的 UTF-8 存储上限和非空 endpoint 字符串。

它们不检查 endpoint 的 URI 格式、scheme、host、port、user-info、query、fragment、供应商 path 形状或上游服务合法性；不会规范化、裁剪、修复、拒绝或重写 endpoint 文本。Schema 通过与 resolver 成功只是“可在本地保存/取得”的事实，**绝不等于网络授权、请求可构造、主机可连接或上游会接受请求**。

`SecretRef` 仍是正式 API key 的推荐存储形式：用 Windows DPAPI `CurrentUser` 与产品专属、版本化 purpose/entropy 保护。endpoint 含有 user-info 或 query 凭据时也不得因而拒绝 profile；这些嵌入内容只是敏感 endpoint 文本，不能取代 `SecretRef` 的设计。

## 4. 调用时 adapter 解释与稳定失败

A1 只提供 `OpaqueEndpoint` 的 contracts、schema/codec、原子保存、导入、redaction 与 resolver。A1 不为决定“可否保存/resolve”而解析 endpoint，也不实现真实 HTTP。

A2/A3 的已显式绑定 adapter 在真正调用时，才针对自己的 `ProtocolId` 对原始 string 尽力解释并构造唯一请求。构造失败、网络失败或上游拒绝时，返回稳定、脱敏、可关联的失败结果。失败不得：

- 回写、清洗、规范化、禁用、删除或替换用户保存的 endpoint/configuration；
- 选择第二 profile/model/endpoint/adapter/protocol/channel；
- 把一个 Image 失败交给 Chat，或反过来处理。

因此“可保存”与“某次调用可用”是明确分离的概念。调用时的 adapter 也不从 endpoint 文本推断协议；显式 binding 才是唯一选择依据。

## 5. 本地存储、脱敏 UI 与导出

AI 设置是 current-user application data，不是 Unity project configuration。严格版本化 aggregate JSON 使用有上限的 UTF-8 序列化、同卷原子写入与 `.bak`；未来版本、损坏 primary/backup、不可读文件或失败恢复都 fail closed，且不能静默用空设置覆盖用户已有配置。该完整性规则不重新引入 endpoint 语法阻断。

endpoint 的 query 或 user-info 可能敏感。日志、异常、receipt、telemetry、普通 UI、cache key 与默认导出只能显示脱敏摘要和稳定 code；不得显示原值、key/token、`Authorization`、认证 header、SecretRef payload、prompt、raw request/response 或原始图片数据。

专门的 provider profile 编辑页面可在用户明确编辑时显示原 endpoint，以便用户修改。该值不能泄漏到该编辑界面之外的诊断表面。导出默认不包含 provider configuration；用户必须明确勾选“包含 provider configuration”并看到警告：导出的原 endpoint 可能包含 query/user-info 凭据或其他敏感文本。即使显式导出，SecretRef payload 仍不能导出。

## 6. Tom_doc 导入：保留用户文本，不迁移密文

Tom 导入是本地、用户确认的草稿导入，不能自动激活 profile 或 binding。`BaseUrl` 等 endpoint 来源字段可作为原样 `OpaqueEndpoint` 草稿保留，预览与非编辑表面只显示脱敏摘要；它们不经过 URI 接受规则，也不触发协议猜测。

| Tom 字段类别 | 本计划的处理 |
|---|---|
| `Id`、`Type`、`DisplayName`、`Enabled`、`BaseUrl`、`DefaultModel`、`RelayWebsiteName`、`RelayProtocol`、`TimeoutSeconds` | 非受信任草稿元数据。可供显示名、Origin 建议、opaque endpoint、model/capability 草稿和超时建议使用；不能自动生成 active binding 或协议。 |
| `ApiKeyProtected` | **绝不复制、解析、解密、重加密、输出或持久化。** 用户需在本产品中重新输入 credential，得到新的 `SecretRef`。 |
| `CommandPath`、sidecar/CLI 线索、cookie/浏览器状态 | 拒绝。不得启动外部命令、读取 PATH 或 Cookie。 |

`RelayProtocol=auto` 只能作为 UI 提示；用户必须明确选定 `ProtocolId`、capability/model、credential 和每条 channel binding。导入不继承 verification/health 状态。

## 7. A1 重构范围与证明义务

A1 的已发布 owned roots 保持不变：

1. `src/VFXComposer.AI.Contracts/**`
2. `src/VFXComposer.AI.Providers/**`
3. `src/VFXComposer.AI.Tests/**`
4. `docs/schemas/desktop/vfxcomposer-ai-provider-config-v1.schema.json`
5. `VFXComposer.sln`
6. `eng/verify-phase2-schemas.py`
7. `eng/run-phase2-gate.ps1`
8. `eng/phase2-baseline-roots.json`

本次重基线不写这些路径；它只编辑七份文档。后续 A1 writer 必须将旧 endpoint admission 重构为 `OpaqueEndpoint`，不得用保存/resolve 阶段的 URI 检查代替 adapter 的调用时解释。A1 仍不实现真实 Chat/Image HTTP、Desktop UI wiring、Broker/Worker/Unity 改动或项目写入。

最小重验矩阵必须包括：

1. 任意有界 endpoint string（含损坏 URI-like 值、user-info、query、fragment）原样 save/resolve；
2. 结构/类型/版本/重复或未知字段/存储超限仍正确失败，但 endpoint 语法绝不成为 schema 或 resolver 拒绝理由；
3. 配置 save/resolve 不调用网络 handler，且不把通过解释成网络权限；
4. A2/A3 调用时解析失败、网络失败和上游拒绝均给出稳定脱敏错误，零回写、零 fallback；
5. 普通 UI、logs、exceptions、receipts、默认 export 不泄漏 endpoint 敏感部分；明确编辑和有警告的显式 configuration export 遵守用户意图；
6. DPAPI/`SecretRef`、primary/backup 恢复、Tom `ApiKeyProtected` exclusion、双通道 mismatch、Contracts/Providers/Tests 与 Broker/Worker/Unity/Desktop 边界仍通过。

## 8. 后续节点边界

### A2 — Chat adapter

只能为已绑定 `ChatLlm` protocol 编写请求时 adapter。它对原始 `OpaqueEndpoint` 尽力构造一次同路由请求；不能修改配置，也不能在失败时换 profile/model/endpoint/adapter/channel。

### A3 — Image adapter

只能为已绑定 `ImageGeneration` protocol 编写请求时 adapter、受限结果处理及私有缓存。provider 返回的资源仍是不可信输入；任何下载/解码失败只会使当次 image 请求失败，不能把控制权转到其他 route。图片不得自动进入 Unity、`Assets`、Recipe、Patch 或项目路径。

### A4–A6

A4 的 Desktop 仅调用 Gateway 与设置管理契约；设置页清晰区分两个 binding，正常显示 endpoint 脱敏摘要，明确编辑时才显示原值。A5 只可产生 mock 证据。A6 必须重新核对 `OpaqueEndpoint`/请求时解析/脱敏/no-fallback 语义，不能把已 supersede 的 A1 `0/0/1` 当成现行实现结论。

## 9. STOP 线

以下均为 STOP：把 endpoint 保存/resolve 重新绑定到 URI、scheme、host、port、user-info、query、fragment 或上游合法性检查；把本地配置通过说成网络授权；任意 fallback；失败时回写配置；普通诊断或默认导出泄漏 raw endpoint；导入 Tom 密文；AI 结果自动写入 Unity。

本文件随此次七文档 rebase 通过 `git diff --check` 且工作树 clean 后 `FINAL STOPPED`。下一步仅是独立 A1 重构和重验。*（Historical——该"下一步"已完成：A1 已按本计划重构并最终验收，A2–A6 亦已全部关闭。）*
