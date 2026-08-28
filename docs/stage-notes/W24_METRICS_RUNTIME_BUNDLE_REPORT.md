# W24 Metrics Runtime Bundle Report

日期：2026-08-26
状态：`NO-GO / SYNTHETIC_SCAFFOLD_ONLY / TRUSTED_PUBLISH_VERIFY_LEASE_FAIL_CLOSED / INDEPENDENT_REAUDIT_PENDING`

## 1. 目标与权限边界

本轮保留的只是 S5 machine evaluator runtime 的**合成机械夹具**。`build_scaffold_bundle(..., acknowledge_test_only=True)` 可把测试提供的 Python/NumPy/Pillow 字节白名单复制到 content-addressed 样式目录，`verify_scaffold_bundle` 可重放 manifest/byte 检查。二者不从 `PATH` 发现 Python，不读取 user site，不调用 pip，也不联网。

当前进程没有独立 gate principal，也没有对父目录持久 handle、handle-bound rename、不可增删目录边界或最终 replay 前的 use gate。同 principal 可凭 owner/`WRITE_DAC` 改 ACL 或替换 namespace；pin 全部文件仍不能阻止目录注入。因此 `build_bundle`、`verify_bundle`、`acquire_verified_bundle_lease` 以及对应 CLI 均显式 fail closed。没有生成正式 runtime bundle，没有执行正式 `render_metrics.py`，没有写 machine-gate report/receipt，也不产生 Machine Pass、Machine Fail、Visual QA、L3/L4、执行或用户裁决权威。

## 2. 冻结协议

- bundle schema：`w24-s5-metrics-runtime-bundle/1`
- bundle 状态：`SYNTHETIC_SCAFFOLD_ONLY`（schema 独立拒绝 `SEALED`）
- 平台：Windows x86_64
- Python：仅接受明确的 `Python 3.12.<patch>` 身份
- 启动策略：唯一 executable，固定参数 `-I -s`
- 禁止项：ambient PATH、user site、network、package mutation 全部为 `false`
- 输入：合成测试中恰好三个互异、位于 Windows `DRIVE_FIXED` 的绝对源根 `python`、`numpy`、`pillow`，以及 exact 文件白名单；UNC、mapped drive、device/extended path 与绝对路径 ADS 均拒绝
- scaffold 输出：`sha256-<bundleTypedHash>/`；目录内只允许 manifest 声明的文件和 `runtime-bundle.json`；该命名不表示正式 namespace
- byte seal：整体 file set、Python/NumPy/Pillow 三组件树、manifest self-hash 均使用 `w24-typed-binary-v1`，但不构成发布或执行 seal
- 文件身份：每项绑定 normalized ASCII path、SHA-256、byte length、component、kind；文件必须是 single-link regular file，hardlink/reparse 均拒绝
- 上限：20,000 文件、单文件 128 MiB、总计 2 GiB、JSON 32 MiB、路径 512 字符/12 层
- ACL/replay scaffold：机械层仍测试 protected ACL、pinned file reads 与 replay，但 owner 保留 `WRITE_DAC`，逐路径 ACL 操作也未绑定稳定目录 handle；它们不是对抗同 principal 的安全边界

OS 的只读位、ACL 文本匹配、一次 scaffold verifier 返回值、或 pinned-file replay 都不是 authority。未来实现必须由独立服务/principal 拥有不可增删父目录和 handle-bound publication/execution gate；在此之前没有可消费输出的生产路径。

## 3. 原子性与 fail-closed 行为

scaffold 先两次读取并哈希每个源，再经 final-path/root containment 校验的 pinned read handle 与 exclusive destination handle 复制到 UUID pending 目录；复制后逐文件复核、写入 exclusive manifest、做 exact-tree 检查，再执行路径级 rename。该流程只测试机械失败路径：`st_dev/st_ino` 的路径采样不能消除 rename/target 竞态，所以生产 publication 已关闭。cleanup 失败通过 exception note 附到原始异常，原始构建错误不再被覆盖。

所有外部绝对路径先经过纯词法 gate：UNC、device、extended namespace、ADS 在 `Path.absolute`、`exists/stat/lstat`、reparse 检查或文件读取前拒绝；随后 Windows 仅以 drive letter 调 `GetDriveTypeW` 确认 `DRIVE_FIXED`，最后才允许 stat/reparse。测试不触碰真实 UNC，而用 mock 证明拒绝顺序与零 supplied-path I/O。

异常 staging 不再调用 path-based recursive delete；只有创建时 pin 的目录 identity 可被改名移出 active pending namespace，随后作为 rejected residue 保留。身份异常时原地保留，绝不扩大删除范围。

## 4. 文件与当前身份

- `tools/vfx/w24_metrics_runtime_bundle.py` — `sha256:9b6bc681d2ecc5df433b701e9c565bd04b83cbb9a05ea3124c5d56da5c772641`
- `docs/schemas/w24-s5-metrics-runtime-bundle-v1.schema.json` — `sha256:e2ec9bf09671c641f3d3388816a95534ff613d848219d36ce38f7b4dc4ac6def`
- `tools/vfx/tests/test_w24_metrics_runtime_bundle.py` — `sha256:b5387ced9d805d82dd28e87e4d8eb345ba17a8a7648cfa450f84f1ed5be0b278`

任何源码、schema 或依赖版本变化都必须重新运行本测试并由 evaluator registry 升版/重签，不能静默复用旧 runtime identity。

## 5. 当前验证

- focused Python：25 discovered，24 passed，1 skipped，0 failed
- descriptor-schema + runtime focused：36 discovered，35 passed，1 skipped，0 failed
- `tools/vfx/tests` 全量：114 discovered，112 passed，2 skipped，0 failed（其中一个 skip 为本项 symlink 权限；另一个是既有 Windows 权限条件）
- skip 原因：当前 Windows 主机未授予 unprivileged symlink 创建权限；同一 reparse 拒绝分支另有 deterministic mock 负例通过
- `py_compile`：tool 与 tests 通过
- Draft 2020-12：schema meta-validation 与生成 manifest 正例通过
- typed-binary golden vector：与冻结 `render_metrics.py` 的 UTF-8/Unicode/hash vector 一致
- tamper：payload、manifest、extra file/empty directory、same-byte external hardlink、ACL drift、source-copy identity drift 均拒绝
- strict JSON：duplicate、NaN、overflow Infinity、lone surrogate、pre-parse depth、超长数值词法均拒绝；100,000-node 正边界接受、100,001-node 独立负边界拒绝
- resource/path：单文件、总量、JSON、relative/duplicate root、UNC/mapped volume、drive-relative/ADS/reserved/overdepth/oversegment output、component mismatch、unowned preservation 均拒绝
- scaffold 机械性：并发 exact publisher、publish 后验失败隔离、异常 pending 保留均有合成覆盖；cleanup 双失败保留 primary exception 并附 secondary note；不再把这些结果描述为原子生产 publication
- 生产边界：trusted build/verify/lease API 与 CLI fail closed；不存在 `VerifiedRuntimeBundleLease` 公共类型

测试使用的是小型合成字节，不是正式 Python runtime 或 Unity evidence。

证据卫生：最初一次不可恢复 ACL 原型在系统临时目录留下两份各 `3,416` bytes 的测试残件：`C:/Users/admin/AppData/Local/Temp/tmp1oumx0b0` 与 `C:/Users/admin/AppData/Local/Temp/tmppa01c_8h`。当前非提权进程无法删除；它们不在 repository、正式 runtime 或 evidence namespace 内。现行 ACL 已显式保留 owner `WRITE_DAC`，后续测试 teardown 已通过。旧两份只允许在获得管理员权限后做 exact ACL reset + exact-path cleanup，不能用名称级/广域删除。

## 6. Remaining gates

1. 由 OS 服务或独立 gate principal 拥有 runtime store 与不可增删父目录；普通调用 principal 不得持有 owner/`WRITE_DAC` 或 rename/delete 权限。
2. 以持久目录/文件 handle 绑定 staging、publication target、最终校验和实际进程启动，且在最终 replay 前禁止 use；不得退回路径采样的 `st_dev/st_ino` 证明。
3. 对 ACL seal 的每个对象使用 handle-bound identity 校验，并证明 parent namespace 的独占性。
4. 独立复审上述真实隔离实现、正式 allow-list/runtime bytes 与无网络 runnable smoke 后，才可重新开放 trusted API/CLI。
5. 完成 descriptor writer、gate-owned rerun、三态 terminal receipts 与 opaque authority verifier 后，才允许进入真实 Machine Pass/Fail/Invalid 路由。

在这些 gate 完成前，本项不得被写成 production evaluator 完成或任何候选通过。
