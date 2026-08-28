# S6 阶段纪要：正式编译器

> 状态：完成，**主 Agent 独立验收通过**  
> 执行日期：2026-08-22  
> 范围：S6 正式编译器、EditMode 覆盖、最小操作面；未实现 S7 播放控制器或预览。

## 实现范围

- `VfxBindingHandlerRegistry` 是唯一的正式 binding 执行表。它以 `VfxBindingKeys` 的 11 个常量为 key，显式写入 `Transform`、`ParticleSystem` 与 `TrailRenderer`：Core scale、Embers rate/lifetime、Impact count/speed、Trail time/width、Launch lifetime/size、Shockwave lifetime/endSize。没有反射、类型名执行或任意属性路径。未知 binding 为 `E500`，Handler 执行失败为 `E501` 并指向稳定的 `/stages/{id}/modules/{id}/parameters/{key}` 路径。
- `VfxCompiler` 的 Dry Run 依次执行 Recipe/Catalog/语义/预算/allow-list 检查，输出 create/update/unchanged/blocked；所有 error（包括预算 `E401`–`E404`）均阻止 Build，Dry Run 不创建任何资产。每个已使用模板以 `AssetDatabase.GetAssetDependencyHash(assetPath)` 获取 Prefab 的递归依赖 hash；该值进入 build hash 和每条 Build Manifest template 记录，因此在 GUID/version 未变但 Prefab、材质或纹理依赖变化时，Dry Run 为 update。
- 正式 Build 按 S1 决策实例化并 `UnpackPrefabInstance(Completely)`，将 Stage 置为 Launch/Travel/Impact 根，按 `attachTo` 组织模块；为每个 renderer 建立 `Generated` 下的材质副本，设置确定随机种子，并在 Prefab 根挂仅作 managed 标记的 `GeneratedVfxController` 空壳。该空壳没有 S7 的播放 API 或控制逻辑。
- 产物在 `Assets/VFX/Generated/{recipeId}/`；输出路径不来自 Recipe 任意路径且每次写入前受 `Generated` 边界检查。临时目录也位于该边界内，短名称为 `vfxs6tmp_` 加 8 位随机值；严格使用 `AssetDatabase.CreateFolder` 返回 GUID 再 `GUIDToAssetPath` 获取实际路径，确认其直属 `Generated` 且前缀正确后才使用和 finally 删除。
- 临时 Prefab 会做结构、缺材质、项目内依赖边界检查。提交时先把临时材质复制/就地序列化到最终输出，随后用 `SaveAsPrefabAsset(tempPrefab, samePath)` 覆盖目标 Prefab，因此已有 `.meta`/GUID 保持不变。提交前会复制既有 Prefab/材质到临时备份；提交或 Manifest 写入失败时恢复它们以及旧 Manifest。Build Manifest 用 pending 文件后 `File.Replace`/`File.Move` 写入；Build Hash 包含 template version/GUID/dependency hash，Manifest 另记录 template path，并记录 Recipe hash/revision、编译器/Unity 版本、输出、时间和静态成本。
- 幂等比较层级遵循 S1：以 canonical Recipe + compiler/Unity + 去重排序模板 ID/version/GUID/**dependency hash** 的 build hash 判断 unchanged；输入不变不写盘。资产回归以文字结构快照（层级、组件、关键粒子/Trail 参数）、深拷贝/材质路径断言和首次稳定生成后输出目录文件 SHA-256，而非首次材质浮点序列化的字节差异。
- Recipe 的 `recipeVersion` 与实例 `revision` 分开：S6 新增 top-level `revision`，显式值必须是 `>= 1` 的整数（否则 `/revision` 为 `E316`；类型不对仍由结构错误 `E102` 报告）。为兼容 S1/S2/S4 已存在 v1 JSON，缺失字段按 revision `1` 解析；正式 `fireball-2d.default` 显式声明 `revision: 1`。Build Plan 与 Build Manifest 记录 `recipeRevision`。
- 默认手写 Recipe：`project/Assets/VFX/Recipes/fireball-2d.default.json`，包含 Launch 的 Flash、Travel 的 Core/Trail/Embers、Impact 的 Flash/Burst/Shockwave，和 S5 Gold Sample 的模块职责对应。
- 最小 Editor UI：`Tools/VFX Composer/Compiler`，选择 Recipe `TextAsset` 后可 Validate、Dry Run、Build，报告列出 code/severity/path/message 与 changed/unchanged/blocked 项。

## 自动化证据

新增 `CompilerIntegrationTests`（11 条）使用真实 S5 Catalog/模板，覆盖：全部 binding 和结构文字快照、默认合法 Build、Dry Run 只读、预算门禁、相同路径 GUID、同语义输入的 unchanged、失败 binding 的全 `Generated` 文件 hash 回滚、Build Manifest、未知 binding 的精确路径、输出边界。测试注入的 dependency-hash provider 证明同一 Recipe/GUID/version 下依赖 hash 改变会生成新 build hash 并将已有输出标记 update；Manifest 断言 recipeRevision 和 dependencyHash 均存在。unchanged 测试在首次成功 Build 后记录整个 recipe 输出目录的文件名和 SHA-256，第二次以语义等价 JSON Build 后断言 hashes 完全一致；比较层级因此为规范化输入 hash 的 skip 决策、结构/关键参数快照和已稳定生成物的字节 hash。每个成功/失败路径都枚举 `Generated` 直属目录，断言不存在 compiler temp 前缀；测试开始/结束只会删除已验证“直属且为空”的历史 compiler temp 目录。另有 internal、仅测试可见的提交 hook：在目标 Prefab 和材质写入后、Manifest 写入前受控抛异常，断言已有输出的全 `Generated` hash、Prefab GUID、Manifest 及所有临时/backup/pending 文件完全恢复；另有首次 Build 的相同 commit-fault 测试，断言空基线的 Generated 文件 hash 与直属目录集合不变、不会遗留新建 recipe 输出目录或 `.meta`。S4 Domain test 新增旧 Recipe revision 缺省 1、非法 revision `/revision` 错误测试；已有 canonicalizer 测试继续覆盖键顺序/空白等价 hash 与语义差异。

| 命令 | 实测结果 |
|---|---|
| `cmd /c tools\compile-check.bat` | 退出码 0 |
| `cmd /c tools\run-tests.bat EditMode` | 36 total / 0 failed |
| `cmd /c tools\run-tests.bat PlayMode` | 1 total / 0 failed（仅既有 Runtime 回归，未扩展 S7） |

批处理均在无项目 Unity 实例下运行；脚本会报告并忽略无进程时的历史 `SourceAssetDB-lock` 诊断，实际 Unity 进程均正常退出。

## 原子/恢复策略

1. Validate、预算和 binding allow-list 在任何写前完成。
2. 全部模板深拷贝、参数化、材质副本和结构检查在 `Generated/vfxs6tmp_*` 中完成。
3. 仅在临时 Prefab 有效后备份现有目标，再将材质就地更新/创建，并用同路径 Prefab 覆盖保 GUID。
4. 若第 3 步任何一处抛出，恢复旧 Prefab、旧材质与旧 Manifest；若还没有旧资产，则只在该 recipe 输出目录在提交前不存在、严格直属 `Generated` 且恢复后为空时，通过 `AssetDatabase.DeleteAsset` 删除该精确目录及其 `.meta`。finally 必删临时目录。`E501`/`E602` 保留错误原因和稳定位置，不静默吞掉异常。

## S6 退出条件对照（供主 Agent 验收）

- [x] 默认 Recipe 一键生成完整 2D 火球受管 Prefab，Launch/Travel/Impact 模块结构与 S5 Gold Sample 对齐。
- [x] 第二次同语义 Build 的 Dry Run 为 unchanged；测试以 S1 规定的输入语义和结构/参数层验证。
- [x] 非法/中途 binding 失败保持 `Assets/VFX/Generated` 文件 hash 不变，并输出 `E501` 精确路径。
- [x] Prefab 同路径重建保 GUID；生成 renderer 引用 `Generated` 材质副本；子节点不存在 Nested Prefab source。
- [x] 最小 Validate/Dry Run/Build 界面与 Build Manifest 已落地。
- [x] 主 Agent 独立复核与验收结论。

## 主 Agent 独立验收（2026-08-22）

结论：**通过 S6 退出门禁，可以进入 S7。**

- 第一轮静态审查发现 `Generated` 残留 15 个被 Unity 改名的 `_tmp*` 目录；整改后临时目录改用短名并严格采用 `CreateFolder` 返回 GUID 解析实际路径。主验收结束时 `Assets/VFX/Generated` 递归条目数为 0。
- 回滚测试由“临时组装阶段失败”扩展到两种真实提交中断：已有成功输出时恢复 Prefab、材质、Manifest、文件 SHA-256 和 GUID；首次提交失败时恢复空目录基线且不遗留 recipe 目录或 `.meta`。
- 幂等测试直接比较首次稳定生成后整个输出目录的文件名与 SHA-256；第二次语义等价 Build 为 unchanged 且字节无变化。
- 模板递归依赖 hash 已纳入 Build Hash/Manifest，解决模板内容变化但 GUID/version 未变时的误判；Recipe `revision` 以“旧 JSON 缺省 1、正式 Recipe 显式 1”的兼容方式补齐，并记录进 Build Plan/Manifest。
- 主 Agent 独立重跑：`compile-check` 退出码 0；EditMode `36/36`；PlayMode `1/1`。测试后无 Unity 进程、无临时/生成物残留，`git diff --check` 通过。

## 残余限制

- `GeneratedVfxController` 仅为 S6 managed 标记；阶段激活、播放、Preview、Stop/Pool 逻辑严格留给 S7。
- 预算仍是 S4 定义的静态预检，不是实际设备性能结论。
- 首版没有 Detach/Bake，也不保留用户对 Managed Prefab 的 Inspector 手改；重建会覆盖。
- Build Manifest 的时间戳仅在输入发生变化、需要写入时更新；unchanged 路径不会触碰文件。
