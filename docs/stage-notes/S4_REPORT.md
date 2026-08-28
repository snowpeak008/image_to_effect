# S4 阶段纪要：领域模型与验证器

> 状态：完成，待主 Agent 独立验收  
> 执行日期：2026-08-22  
> 范围：仅 S4；未创建或修改正式 VFX 模板资产，未进入 S5。

## 已完成

- `VFXComposer.Editor` 中新增纯 C# Recipe、Stage、Module、Manifest、参数声明、Cost 与统一 `ValidationReport` 模型；Runtime 程序集未改动。
- Recipe/Manifest 严格 JSON 解析：根、Stage、Module、metadata 与 Manifest 参数声明的未知字段均报错；Stage/Module 未知字段在取得 ID 后使用 `/stages/{stageId}/...`、`/stages/{stageId}/modules/{moduleId}/...` 稳定路径。Module `parameters` 的未知参数由 Manifest 契约层报错，不会静默忽略。
- Recipe 采用正式枚举和稳定 ID；错误路径按 `/stages/{stageId}/modules/{moduleId}/...` 定位。
- Catalog 支持递归扫描/加载 JSON Manifest（文件名 Ordinal 排序）、`templateId` 索引与重复 ID 检测。Manifest 结构/语义不合法、GUID 无法解析或 GUID/path 不一致时均不会进入索引。GUID/path 校验通过可注入的 `IAssetReferenceResolver` 进行，不依赖或操作真实 Unity 资产。
- 实现结构与语义验证：版本、必填/类型/枚举、唯一 ID、模板存在、kind/dimension、参数白名单/类型/范围和 `attachTo`。
- 实现规范化 JSON + SHA-256：对象键序，数组保序，浮点以不受文化区域影响的 `R` 格式输出并统一零值；空白、换行、对象键序不影响 hash。解析/验证同时拒绝 Recipe duration 与实际 float 参数的 `NaN`/`Infinity`。
- 实现 `mobile_medium` / `pc_editor` 静态预算档位，覆盖估算峰值粒子、材质、Trail 与总 Stage 时长，产出 error/warning/info。

## 错误码

- `E100`–`E105`：未知字段、必填、类型、枚举、非法 JSON、非有限 Recipe 数值。
- `E200`–`E212`：Manifest 目录、重复 ID、GUID 无法解析、GUID/path 不一致、Manifest 版本/必填字段/安全路径/Cost/Binding/参数契约、Resolver 异常、文件读取失败。
- `E300`–`E315`：Recipe 版本/ID/ID 唯一性、有限且非负 duration、`attachTo`、Catalog、模板、kind/dimension、参数契约/类型/范围、非有限参数值。
- `E400`–`E404`、`W401`–`W404`、`I400`：预算预检。

## 测试与夹具

- EditMode 共 **19** 条，均通过；其中 S4 新增 `DomainValidationTests` 的 **18** 条（含 TestCase 展开）。
- 默认合法火球样例与 **14** 个非法 JSON 夹具：未知字段、稳定 Stage/Module 路径、缺必填、坏枚举、重复 ID、未知模板、未知参数/越界、参数类型、kind/attachTo、Manifest 契约未知字段、Manifest 版本/路径逃逸/负 Cost/空 Binding、坏 Manifest JSON、非有限 duration/参数。
- 额外测试覆盖 Catalog 重复 ID、文件排序、GUID/path resolver 的无法解析/不一致且不入索引、模板 dimension 不匹配、Manifest boolean/string v1 无 bounds 约定、Hash 等价性与语义变化、预算 error/warning/info。

## 验证记录

| 命令 | 结果 |
|---|---|
| `cmd /c tools\\compile-check.bat` | 退出码 0 |
| `cmd /c tools\\run-tests.bat EditMode` | 退出码 0；19 total / 0 failed |
| `cmd /c tools\\run-tests.bat PlayMode` | 退出码 0；1 total / 0 failed（既有回归，未扩展 S4 逻辑） |

首轮编译遇到 Unity 所带 Newtonsoft 的 `JToken.ToString(IFormatProvider)` 不可用，已改为从 `JValue.Value` 使用 `Convert.ToString(..., InvariantCulture)`；此问题已由后续 compile-check 验证消除。

## 未解决问题与范围说明

- 无 S4 阻塞项。
- S4 不做 Binding 执行或反射；Manifest `binding` 只保留符号值，Handler 注册表留给 S6。
- S4 不加载/修改真实 Unity 模板、Prefab、材质或其他资产；S5 的正式模板资产仍未开始。

## 主 Agent 独立验收

> 验收日期：2026-08-22  
> 判定：**通过，允许进入 S5**

独立验收经历一次整改：首版缺少稳定 Stage/Module 未知字段路径，以及 Manifest 的版本、安全路径、Cost、Binding、min/max/default 和非有限数契约。整改完成后，主 Agent 复核实现并独立执行：

- `compile-check.bat`：退出码 `0`。
- EditMode：`19 total / 19 passed / 0 failed`。
- PlayMode 回归：`1 total / 1 passed / 0 failed`。
- 无效 Manifest 在结构、语义或 resolver 失败时均不会进入 Catalog；目录输入按 Ordinal 排序。
- Recipe duration、实际数值参数和 Manifest 数值契约均拒绝 `NaN`/`Infinity`，坏输入返回报告而非未处理异常。
- Canonicalizer 保持对象键排序、数组保序、InvariantCulture 与非有限数拒绝；Runtime 程序集未引入 Editor 依赖。

S4 数据契约可作为 S5 Manifest 和 S6 编译器的正式输入。
