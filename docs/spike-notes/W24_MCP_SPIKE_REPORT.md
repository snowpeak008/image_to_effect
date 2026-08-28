# W24 S6 MCP 外部工具 Spike 报告

状态：**DEFER — 未安装、未执行。**  
观察日期：2026-08-25  
范围：只做一手资料审阅与本地操作信封的静态实现；没有安装 Coplay Unity MCP、Python/`uvx`、容器或任何第三方包，没有启动/停止 Unity，也没有接入网络服务器。

外部资料只以链接和本报告的概述记录；没有把 Coplay 或 Unity 的外部源码/文档复制进仓库。

## 结论矩阵

| 项目 | 结论 | 依据与边界 |
|---|---|---|
| 本地依赖零的结构信封/文档检查器 | **GO（源码隔离、authority none）** | 仅比较信封结构并检查三类内存文档；没有 reviewed-plan authority、transport、文件、`UnityEditor`、`AssetDatabase` 或执行器。|
| Coplay Unity MCP 安装或运行 | **DEFER（授权阻断）** | 上游要求安装 Unity 插件和 `uvx`/Python 服务器；本次无第三方安装授权。|
| Coplay 接入正式项目 | **NO-GO（当前阶段）** | W24 §16/§17/§22 要求 S1 之后先完成隔离 Spike；目前不允许改变 package manifest/lock，也没有用户批准。|
| 远程/LAN MCP | **NO-GO（本 Spike）** | 不需要远程能力；任何将 Unity 编辑器暴露给 LAN/远程调用的设计都必须另行安全评审、显式用户授权和隔离环境。|
| 将 MCP 用于 L4、迁移或资产 Apply | **NO-GO（永久不由本边界授权）** | L4 仍只属于用户签署；S4 迁移要求所有权复核、精确计划和用户 token。本信封直接拒绝这些 authority/token。|

这不是对 Coplay 的安全认证，也不是 Coplay/VFX Graph 的实际集成结果。

## 已审阅的一手资料

- Coplay 的仓库说明称该工具能从 MCP client 控制 Unity Editor、管理资源/场景/脚本、运行测试和构建；其 quickstart 要求通过 Git URL 安装 Unity 包，并要求 Python 3.10+（经 `uv`）。[Coplay Unity MCP README](https://github.com/CoplayDev/unity-mcp)
- 其服务器 README 明确要求 Unity MCP Plugin 和 `uvx`；还给出 PyPI、Git 与 Docker 启动方式，因而会新增 Unity 包、Python 包/解释器或容器供应链。[Coplay server README](https://github.com/CoplayDev/unity-mcp/blob/main/Server/README.md)
- 维护者的 `package.json` 声明包名、Unity 2021.3 最低版本和额外 Unity module/Newtonsoft/Test Framework 依赖；这并不替代对本项目锁文件和实际构建的验证。[Coplay package manifest](https://github.com/CoplayDev/unity-mcp/blob/main/MCPForUnity/package.json)
- 上游安全策略说明本地 HTTP 默认仅 loopback，LAN 需显式 opt-in，远程需 HTTPS（除非显式放开）且 remote-hosted 模式需要 API key；它同时把“项目根之外文件读写”和“绕过网络 allow-list”列为安全问题。[Coplay SECURITY.md](https://github.com/CoplayDev/unity-mcp/blob/main/SECURITY.md)

## 本项目事实与决定

`project/Packages/manifest.json` 固定 URP `14.0.12`，未包含 Coplay 或 VFX Graph；本报告没有改动 manifest 或 package lock。W24 §16.3 要求版本兼容、许可证、批处理/Player Build、卸载方案、Generated 所有权/清理边界、对 Recipe/Patch/GUID/事务影响以及用户批准，才可采用第三方依赖。W24 §17 S6 又把 MCP 限定为“生产链稳定后、且 S1 后已完成隔离 Spike”的已批准 Unity 操作执行层。

因此，本次唯一可提交的实现是一个**non-admission、不会执行操作且不具 execution authority** 的结构比较器与内存文档检查器：

- [W24S6McpOperationEnvelope.cs](/D:/WorkWork/Assist/image_to_smart/project/Packages/com.vfxcomposer.unity/Editor/W24/S6/External/W24S6McpOperationEnvelope.cs)；
- [W24S6LocalDocumentInspector.cs](/D:/WorkWork/Assist/image_to_smart/project/Packages/com.vfxcomposer.unity/Editor/W24/S6/External/W24S6LocalDocumentInspector.cs)；
- [w24-s6-mcp-operation-envelope-v2.schema.json](/D:/WorkWork/Assist/image_to_smart/docs/schemas/w24-s6-mcp-operation-envelope-v2.schema.json) 与 exact request/result schemas；
- 对应 EditMode 单元测试（尚未运行，避免与用户 Unity GUI 争用）。

## 当前 allow-list 和拒绝规则

一个信封只能含 1–16 个操作，每个操作必须有唯一 lower-kebab `operationId`、精确的 `expectedInputHash`，并且仅能是：

| 语义操作 | 唯一允许根 | 后缀 |
|---|---|---|
| `ParseRecipeSyntax` | `Assets/VFX/Recipes/` | `.json` |
| `InspectManifestHeader` | `ProjectSettings/VFXComposer/BuildManifests/` | `.manifest.json` |
| `ValidateContractDocument` | `docs/vfx-contracts/` | `.contract.json` |

结构比较器拒绝未知 operation；拒绝控制符、`|`、绝对盘符、UNC、反斜杠、`..`、重复分隔符和越根路径；也没有 shell、任意 C#、`AssetDatabase.DeleteAsset`、`AssetDatabase.ImportAsset`、创建/删除资产、运行进程、网络、L4 或迁移操作的 enum/API。`DryRun` 是信封字段默认值且 `Apply` 一律拒绝。三种 operation 名称描述 document-only scope，不描述 Unity 动作；检查器只接收调用者提供的 immutable bytes，不读取或写入路径。

检查结果固定为 `authority: none`、`machineGatePassed: false`、`scope: syntax-and-document-inspection-only`。调用者提供的 project/input/plan hashes 只能用于结构/字节相等比较；它们不是 reviewed-plan authority，也不能签发执行权限。v2 JSON 边界只输出 camel-case 属性和 schema 定义的字符串枚举，并在输入时拒绝 missing/extra/wrong-type 字段。当前没有可信 issuer、transport、executor 或结果落盘宿主。

所有 request/Recipe/Contract/Manifest 文本在 Json.NET 前经过同一个严格预检：拒绝 comment、single quote、trailing comma、extra root、decoded-equivalent duplicate key、非有限/越界数和孤立代理项；限制为 64 层容器及 100,000 个容器/属性/标量节点。4 MiB 文档 request 还在词法分析前限制原文总字符数，并在 base64 decode 前限制编码长度。Studio 与 Preview 对 Manifest 使用同一预检和同一分段 Runtime Entry validator。

## 权限、身份、哈希、token 与回滚要求

未来即使另获授权，也必须满足以下全部条件；当前没有任何条件会把信封升级成执行权限。

1. 目标 Unity 实例必须由**预先登记的 project identity SHA-256**精确匹配；不得依赖“当前打开的唯一实例”推断。
2. 请求必须携带 schema、`requestId`、project identity、每个输入 SHA-256、dry-run 字段与 no-write rollback mode。`planHash` 只以 length-prefixed SHA-256 绑定完整有序信封内容；caller 提供的 comparison hash 可发现不相等，但**不是人工审核或 reviewed-plan authority**。
3. 对 Apply 的未来设计必须重新读取目标文件 hash/GUID/ownership，生成不可变 plan，并把每个操作的 input/output hash、工具版本和运行实例身份写入证据；计划或输入任一变化均失效。
4. 迁移 token 只能由 S4 内部用户裁决 authority 为一个已审核的 operation hash 签发；L4 只可由绑定 `contractRevision + contractHash + traceHash + buildHash + captureProfileHash + evidenceCorpusHash` 的用户记录授权。外部 MCP 永不得自行构造、转发或消费这两类 token。
5. 任何未来写操作都要先有精确 owned-output 快照、原子文本/资产事务、失败 rollback 和 rollback-failure 的可见诊断；不得用“重建旧版本”替代恢复。dry-run 没有写事务，故这里只接受 `NoWriteRequired`。
6. 外部依赖必须固定 tag/commit 与包/分发物 hash，记录许可证和 SBOM，限定 loopback/stdio，禁止 LAN/remote 默认开启，关闭或明确记录 telemetry，并在独立副本上做最小权限测试后才可请求用户批准。

## 威胁模型

攻击者/故障源包括：被 prompt injection 影响的 MCP client；本机其他进程调用 loopback endpoint；错误路由到用户正在使用的 Unity instance；移动 Git branch/PyPI/容器镜像或其传递依赖；工具的广泛 Editor/脚本/构建能力；日志或 telemetry 泄漏；以及 plan 通过后输入、GUID 或所有权发生变化。后果包括资产破坏、代码执行、项目外文件访问、凭据/内容外传、错误实例修改和伪造生产状态。

本 Spike 的缓解仅限于：没有安装、没有 server、没有网络、没有执行器、无文件/写 API、固定 v2 schema/document vocabulary、文本语法与路径字符串边界、input/plan 相等比较及 authority/token 拒绝。它**不能**缓解一个未来安装的第三方 server 的供应链或 Editor 权限；那需要上面的隔离试验、可信 issuer 和用户授权。

## 允许的后续隔离试验（仍须新授权）

仅在单独复制的项目、无用户 Unity GUI、无正式资产凭据且网络策略经批准时：固定 Coplay release/commit 与所有分发哈希，安装到隔离副本，禁用 LAN/remote，逐项测量只读/干跑操作；再验证多实例 identity、拒绝越根路径、日志/telemetry、卸载复原、manifest/lock diff、批处理和 rollback。不得把这份 DEFER 报告解释为该试验已经做过。
