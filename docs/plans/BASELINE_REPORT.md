# 构建与测试基线报告（O3）

> 状态：BASELINE　|　建立日期：2026-08-29　|　执行者：O3 开发子 agent
>
> 本报告是 P0-1 合并后 master 主线的构建/测试基线，同时作为 P0-1 合并结果的独立复核。后续任务的构建与测试结果以本报告数字为对照。

## 1. 基线环境

| 项 | 值 |
|---|---|
| 提交基点 | `master` @ `e606b570`（含 P0-1 合并 `3375a8fe`）+ 锁文件漂移修复 `a6ee7253` |
| 执行位置 | 独立 worktree `D:\wt\i2s-o3`（分支 `task/O3-baseline`） |
| OS | Microsoft Windows 11 专业版（NT 10.0.22631） |
| .NET SDK | 8.0.420（`global.json` 固定，rollForward 禁用） |
| NuGet 源 | 仅本地批准 feed `.codex_tmp/w24-phase1-approved-feed`（39 个文件），`globalPackagesFolder` 指向 `.codex_tmp/w24-phase1-packages` |
| Unity | 2022.3.62f3c1（`E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`） |

注：worktree 为全新检出，NuGet 全局包缓存从空开始，restore 耗时已包含从本地 feed 填充缓存；`project/Library` 亦从空开始，Unity 耗时包含首次资产导入。在有缓存的环境中各项耗时会显著更短。

## 2. 锁文件漂移修复（任务 A）

P0-1 交付报告发现两个 `packages.lock.json` 自 baseline 起与项目引用图不同步（非合并引入）。修复命令：

```
dotnet restore VFXComposer.sln -p:RestorePackagesWithLockFile=true --force-evaluate
```

实际改动 3 个锁文件，逐一核对如下（全解决方案 18 个工程中其余锁文件均无变化，**无任何包版本变化**）：

| 文件 | 变化 | 与 csproj 核对 |
|---|---|---|
| `services/VFXComposer.Broker.HandleProbe/packages.lock.json` | 原为空（`net8.0: {}`），补入 `vfxcomposer.broker`（依赖 Protocol）与 `vfxcomposer.protocol` 两个 Project 条目 | 与 csproj 的两条 `ProjectReference`（Broker、Protocol）及 Broker→Protocol 引用链一致 |
| `services/VFXComposer.Broker.Tests/packages.lock.json` | ①`vfxcomposer.broker.handleprobe` 条目补入依赖（Broker、Protocol）；②新增 `vfxcomposer.client` 条目（依赖 Protocol） | ①与 HandleProbe csproj 引用一致；②csproj 引用了 Client，Client 仅引用 Protocol，一致 |
| `src/VFXComposer.AI.Tests/RevisionLockHost/packages.lock.json` | **仅行尾规范化**：LF+末尾换行（294 字节）→ CRLF+无末尾换行（308 字节），JSON 内容逐字相同 | 无依赖变化。仓库其余锁文件已提交版本均为 CRLF（NuGet on Windows 写出格式），此文件原为异类；保留规范化结果可避免后续每次 `--force-evaluate` 产生噪音 diff |

修复提交：`a6ee7253 chore(O3): fix baseline package lock drift`。

### 锁定模式验证（重型 gate 前提）

```
dotnet restore VFXComposer.sln -p:RestoreLockedMode=true
```

结果：**成功**（18/18 工程还原，耗时 0.98 s）。

## 3. 基线数字（任务 B）

以下命令均在 `D:\wt\i2s-o3` 根目录、提交 `a6ee7253` 上依次执行。

### 3.1 锁定模式 restore

```
dotnet restore VFXComposer.sln -p:RestoreLockedMode=true
```

- 结果：成功，18/18 工程。
- 耗时：0.98 s（包缓存已填充；空缓存下 `--force-evaluate` restore 为 5.61 s）。

### 3.2 Release 构建

```
dotnet build VFXComposer.sln -c Release
```

- 结果：**成功，0 warning / 0 error**（`TreatWarningsAsErrors=true` 全局开启）。
- 耗时：10.88 s（MSBuild 报告值；含 restore 检查的墙钟 11.12 s）。

### 3.3 全量 .NET 测试

```
dotnet test VFXComposer.sln -c Release --no-build
```

- 结果：**450 通过 / 0 失败 / 0 跳过**（8 个测试工程全绿），总墙钟 10.10 s。
- 该结果与 P0-1 初审报告（450/450）一致，构成 P0-1 合并的独立复核 PASS。

| 测试工程 | 通过/总数 | 运行器耗时 |
|---|---|---|
| VFXComposer.Protocol.Tests | 108/108 | 667 ms |
| VFXComposer.Broker.Tests | 183/183 | 4 s |
| VFXComposer.Broker.ServiceHost.Tests | 16/16 | 282 ms |
| VFXComposer.Client.Tests | 16/16 | 150 ms |
| VFXComposer.AI.Tests | 77/77 | 3 s |
| VFXComposer.Desktop.Tests | 22/22 | 1 s |
| VFXComposer.LocalE2E.Tests | 17/17 | 6 s |
| VFXComposer.AiLocalE2E.Tests | 11/11 | 4 s |

### 3.4 Unity EditMode 测试

本机 Unity 2022.3.62f3c1 可用，已执行两次（第二次用 `-UseGraphics` 复跑，用于排除 `-nographics` 环境因素）：

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-Unity.ps1 -Mode EditMode -TimeoutSeconds 1800
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-Unity.ps1 -Mode EditMode -TimeoutSeconds 1800 -UseGraphics -ResultsPath test-results\EditMode-graphics.xml
```

| 运行 | 总数 | 通过 | 失败 | 跳过 | 测试时长 | 墙钟（含启动/导入） |
|---|---|---|---|---|---|---|
| `-nographics`（Library 冷启动） | 657 | 596 | **8** | 53 | 516.9 s | 10 min 56 s |
| `-UseGraphics`（Library 已缓存） | 657 | 596 | **8** | 53 | 444.0 s | 7 min 54 s |

两次运行失败集**完全一致**，即失败为确定性结果，与图形环境无关。失败清单（`VFXComposer.Tests.EditMode.*`）：

| 失败用例 | 症状 |
|---|---|
| `S11ReleaseAcceptanceTests.ErrorCodeAudit_EditorSourceAndDocumentationRemainBidirectionallyInSync` | 错误码审计期望 `E17xx` 系列，实际枚举得到 `E1xx/E2xx` 系列——Editor 源码与文档的错误码清单不同步 |
| `W17W18NextCandidatePreviewTests.W17Preview_…` | 构建的 preview 场景中找不到 `W17W18NextCandidatePreviewDriver` 组件（`Single()` 空序列） |
| `W17W18NextCandidatePreviewTests.W18Preview_…` | 同上 |
| `W24S3BaselineContractTests.CaptureToolBundle_IsARealReproducibleIdentitySharedByAllThreeContracts` | `W24RealLightingModule.cs` 的 sha256 与契约 pin 不符 |
| `W24S6LocalReadOnlyFilesystemAdapterTests.DirectoryTarget_IsActuallyOpenedAndRejectedAsNonRegular` | 目录目标被拒时返回 `W24FS107`，期望 `W24FS109` |
| `W24S6WorkerHandleAdmissionTests.SessionRevocationInvalidatesLeaseAndOpaqueSurfaceExposesNoNativeHandle` | 不透明句柄面暴露了 `SafeFileHandle` 签名字符串 |
| `W24SustainedFlameProductionTests.CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet` | 授权源集合 sha256 与契约 pin 不符 |
| `W24WorkflowTests.StatusRegistry_RegistersAllGeneratedEntriesAsProvisionalWithoutVisualClaim` | 状态注册断言为 False |

证据文件：`test-results/EditMode.xml`、`test-results/EditMode-graphics.xml`、`test-results/unity-editmode.log`（均在 gitignore 内，不入库；数字以本报告为准）。

## 4. 失败项分类

- **阻塞项：无。** 日常任务验收基线（Release 构建 0/0、锁定 restore、.NET 测试全绿）全部满足。
- **非阻塞——Unity EditMode 8 项失败**：属于合并后 master 上 Unity 包测试的既有内容/契约漂移（sha256 pin、错误码清单、场景组件），与 O3 改动无关（O3 仅动 .NET 锁文件，不影响 `project/`）；两种图形模式下结果一致，可复现。**需主 agent 安排 triage**：这些失败会影响以 Unity 编译链路为验收面的任务（尤其 F2），届时必须先归零或明确豁免。
- 非阻塞遗留（不在本任务范围）：`.gitignore` 未覆盖 `tests/**` 构建产物（`obj/`、`bin/` 在 `git status` 中可见），已归入 O1。

## 5. 基线适用范围与失效条件

本基线适用于对照"**不改变依赖图与协议面**"的日常任务（代码逻辑改动、文档、测试新增等）。出现以下任一情形时本基线失效，须重跑本报告全部命令重建基线：

1. **依赖变更**：任何 csproj 的 `PackageReference`/`ProjectReference` 增删、`Directory.Packages.props` 中央版本变更、批准 feed 内容变更——锁文件必须随之更新且锁定模式 restore 重新验证。
2. **协议变更**：`VFXComposer.Protocol` 的 DTO/合同面变更（测试数量与通过基线随之变化）。
3. **工程结构变更**：解决方案增删工程、`Directory.Build.props` 全局属性变更（如警告策略）。
4. **工具链变更**：`global.json` SDK 版本、Unity 版本升级。
5. **Unity 包内容变更**：`project/Packages/com.vfxcomposer.unity` 的源码/契约 pin/文档变更会使 Unity EditMode 部分（596 通过 / 8 失败 / 53 跳过）失效，须复跑。
6. 耗时数字仅供量级参考（受缓存与机器负载影响），不作为验收阈值；验收阈值是 0 warning / 0 error 与测试全绿。
