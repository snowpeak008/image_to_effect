# S3 阶段纪要：决策定版与工程基线

> 状态：完成，待主 Agent 独立验收  
> 执行日期：2026-08-22  
> 范围：仅 S3；S1/S2 代码和资产未进入 `project/`。

## 已完成

- 已新建正式 Unity 2022.3.62f3c1 / URP 14.0.12 项目与嵌入式 `com.vfxcomposer.unity` 包。
- 已实际生成 `Assets/Settings/UniversalRP2D.asset` 与 `Renderer2D.asset`，并将 Graphics Settings 与当前 Quality Settings 指向该 2D URP 管线；项目默认没有 Bloom Volume，符合“Bloom 默认关闭”。
- 已创建 Runtime、Editor、EditMode、PlayMode asmdef，依赖方向为 Editor/Test → Runtime；Runtime 中没有 `UnityEditor`。
- 已建立 `Assets/VFX/Templates`（只读输入）与 `Assets/VFX/Generated`（工具唯一写入）边界。
- 已建立命令行编译、EditMode、PlayMode 验证脚本；结果会保留到被忽略的 `test-results/`。

## 验证记录

所有命令均从仓库根执行，调用锁定的 `E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`；脚本按启动的精确 PID 等待，默认外层超时 900 秒，日志与 XML 保留在被 Git 忽略的 `test-results/`。

| 命令 | 退出码 | 结果 / 证据 |
|---|---:|---|
| `cmd /c tools\\compile-check.bat` | 0 | 整改后复跑编译通过；`test-results/unity-compile.log` 以 `Exiting batchmode successfully now!` 结束。 |
| Unity `-executeMethod VFXComposer.S3Bootstrap.S3UrpProjectBootstrap.Configure` | 0 | 生成 2D Renderer 与 URP 资产并写入 Graphics Settings 和当前 Quality Settings；`test-results/unity-urp-bootstrap.log` 记录 `S3 URP 2D baseline configured`。一次性引导代码随后已移除。 |
| `cmd /c tools\\run-tests.bat EditMode` | 0 | 整改后复跑；运行前删除旧 `EditMode.xml`，新 XML 经 NUnit 门禁通过：1 total、1 passed、0 failed。包含 `RuntimeAssembly_IsPlayerSafe_AndDoesNotMentionUnityEditor`。 |
| `cmd /c tools\\run-tests.bat PlayMode` | 0 | 整改后复跑；运行前删除旧 `PlayMode.xml`，新 XML 经 NUnit 门禁通过：1 total、1 passed、0 failed。包含 `RuntimeAssembly_LoadsInPlayMode`；进程正常退出，无挂起。 |
| `powershell -File tools/Invoke-Unity.ps1 -Mode ValidateResults -ResultsPath test-results/S3-xml-gate-failure.xml` | 3（预期） | 对一次性 `total=1, failed=1` 的临时 NUnit XML 返回非零并输出摘要；文件已删除，未影响正式 XML。 |

首次测试脚本误传了 `-quit`，Unity 在执行 Test Runner 前退出且没有 XML；已移除该参数。首次项目编译后留下的 `Library/SourceAssetDB-lock` 是无活动 Unity 进程时的陈旧诊断文件；脚本现只在检测到实际指向该项目的 Unity PID 时拒绝运行，并在陈旧文件存在时给出 warning，不会误报项目锁。静态验收后，`Invoke-Unity.ps1` 已加固：隐藏 Unity 窗口、运行前删除目标平台旧 XML、兼容 NUnit 2/3 属性名解析并要求 `total > 0` 与 `failed = 0`；等待出现任意异常时，只有启动时记录的精确 PID 及启动时间仍匹配才会终止该 PID 并返回 124，其他 Unity 不受影响。

## 未解决问题

无阻塞项；不进入 S4。

## 主 Agent 独立验收

> 验收日期：2026-08-22  
> 判定：**通过，允许进入 S4**

独立验收结果：

- 静态检查确认 `project/` 不含 S1 Runner、Spike 模板、Spike Generated 或 Evidence。
- `project/Assets` 下所有文件与目录均有对应 `.meta`；Git 忽略 Library、Logs、UserSettings、spike 与 test-results，但不忽略 Assets、Packages、ProjectSettings 和 `.meta`。
- Runtime asmdef 无 Editor 引用，Runtime 源码不含 `UnityEditor`；Editor/Test 依赖方向正确。
- URP 资产 GUID `c5da5e685a2e6874bae434147a0fb58d` 已绑定 Graphics Settings 和当前 Quality Settings，Renderer2D GUID 为 `316ce1b97ff10634eb1cffdbbc8d534e`。
- 主 Agent 依次重跑 `compile-check.bat`、EditMode、PlayMode：退出码均为 `0`；EditMode 与 PlayMode XML 均为 `total=1, failed=0`，结束后无 Unity 进程残留。
- 自动化脚本首轮验收发现旧 XML、XML 内容门禁和超时清理风险；整改后再次复核，现已在运行前删除旧 XML、解析 NUnit 结果、隐藏 Unity 窗口，并只按 PID + 启动时间清理本次进程。

非阻塞说明：Unity 会留下 `Library/SourceAssetDB-lock` 诊断文件；脚本通过实际进程命令行判断真实项目锁，当前行为符合预期。
