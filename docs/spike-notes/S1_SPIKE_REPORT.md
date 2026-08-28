# S1 技术贯通 Spike 纪要

> 状态：⬜ 进行中 / ☑ 已完成
> 执行日期：2026-08-22 ~ 2026-08-22
> 环境：Unity 2022.3.62f3c1，URP 14.0.12，项目 `spike\image_to_smart`
> 本文档是 S1 的唯一保留产出。逐项填写实测结果；"预期外行为"一栏没有就写"无"，不许留空——留空无法区分"没测"和"没问题"。

## 1. 验证清单结果

结论一栏只允许填：**通过 / 有条件通过（附条件）/ 失败**。

| # | 验证项 | 结论 | 实测记录（做了什么、看到了什么） | 预期外行为 |
|---|---|---|---|---|
| 1 | 粒子模块参数写入并持久化（startLifetime 等，存盘后重开 Unity 复查值） | 通过 | 对 A/B 分别设置 Embers `startLifetime=0.55`、Burst `startSpeed=3.5`；关闭 Unity，以新批处理进程重载 Prefab，读取值仍为 0.55/3.5。证据：`SpikeEvidence/phase2-persistence-idempotence.txt`。 | 无 |
| 2 | Emission rateOverTime 写入持久化 | 通过 | 使用 `var emission=ps.emission; emission.rateOverTime=new MinMaxCurve(18f)`；新进程重载 A/B 均读回 18。 | 无 |
| 3 | Burst（SetBursts）写入持久化 | 通过 | 使用 `SetBursts(new Burst(0,24))`；新进程重载后 `burstCount=1`、`burst.count.constant=24`。 | Unity 2022.3 的废弃访问器 `Burst.minCount` 可暴露常量模式下无效的旧 `minScalar`；应检查 `Burst.count.constant`，详见 `api-pit-initial-burst-accessor-failure.txt`。 |
| 4 | 材质实例生成（副本落盘，生成 Prefab 引用副本而非模板材质） | 通过 | A/B × Core/Embers/Burst 共创建 6 个 `.mat` 副本；逐个验证 Prefab 引用路径位于 `Assets/Generated/Materials/`，颜色在新进程中仍为 `(1,0.58,0.08,1)`。 | 新建粒子材质首次跨进程重建时，`_Color.b` 从 `0.079999976` 规范化为 `0.08`；之后稳定。 |
| 5 | 组装策略 A：深拷贝（Instantiate → Unpack → 存为独立 Prefab） | 通过 | 三个模板实例均 `UnpackPrefabInstance(Completely)`；输出 YAML 中 `PrefabInstance` 记录为 0；模板颜色改变后、显式重建前，生成物保持旧色。输出 Prefab 238,590 bytes。 | `GetPrefabAssetType(child)` 返回包含它的生成 Prefab 类型，不能用于判断内部子节点是否仍为 Nested；本次以序列化 `PrefabInstance` 记录和模板传播行为交叉验证。 |
| 6 | 组装策略 B：Nested 引用（不解包，参数成 override） | 通过 | 输出 YAML 中有 3 个 `PrefabInstance` 和模板 GUID；硬编码参数作为 overrides 持久化。模板未被 override 的 startColor 改变后，重建前即传播到生成物。输出 Prefab 11,435 bytes。 | Nested 会使模板变化绕过显式 Build，生成结果可能在 Recipe/build hash 未变时发生变化。 |
| 7 | 幂等：同输入构建两次，git diff 对比 Generated 目录 | 有条件通过（结构层始终成立；材质首次规范化后字节层成立） | 首次 Unity 进程完全退出后提交临时 git 基线；首次重复构建时两个 Prefab 均字节不变，4 个粒子材质各仅有一处浮点文本规范化。以规范化结果作为新基线，在第三个全新 Unity 进程重复构建，8 个 `.prefab/.mat` SHA-256 全部相同且 `git diff` 为空。证据：`git-diff-build1-build2.txt`、`hash-comparison-build2-build3.txt`、`git-diff-build2-build3.txt`。 | Unity 在首次进程的退出尾声仍可能再次序列化材质，所以进程内生成的 `hashes-build1.txt` 不是关闭后的最终基线；可靠比较必须等 Editor 完全退出后再取 hash/提交 git。 |
| 8 | 模板修改后重建：策略 A/B 下生成物的行为差异 | 通过 | T_Embers startColor 从橙改青：重建前 A 仍为橙、B 已变青；显式重建后 A/B 都为青。证据：`phase3-template-change.txt`。 | Nested 的自动传播对“Recipe 是事实源”的工作流构成隐式输入。 |
| 9 | 生成 Prefab 在 PlayMode 正常播放，无 Editor 依赖报错 | 有条件通过（Prefab 行为通过；批处理退出有工具问题） | 参考场景真实进入 PlayMode，找到 4 个 ParticleSystem，全部 `isPlaying=true`，至少一个已发射粒子，missing script=0，确定性 seed 可读。证据：`phase4-playmode.txt`。 | 本机 Editor 在证据写出并请求 `EditorApplication.Exit(0)` 后后台进程不自行结束，两次均在确认 PASS 文件落盘后按精确 PID 结束；正式自动化应使用 `-runTests` 并设置外部超时。 |
| 10 | 重建的 GUID/引用稳定性（场景引用重建后不断裂，.meta GUID 不变） | 通过 | A GUID `3e4b78b4c1dde734bae3a6282dd5256b`、B GUID `1d70dedf83897f848a16d4ab4a32a83e`；覆盖重建前后不变。重新打开 `S1ReferenceScene` 后两个实例均能解析回对应生成 Prefab。 | 无 |
| 11 | 粒子随机种子（useAutoRandomSeed=false + randomSeed 写入持久化） | 通过 | A/B 的 Embers/Burst 均设置 `useAutoRandomSeed=false; randomSeed=42`；新 Editor 进程和 PlayMode 中均保持。 | 无 |

## 2. 三个命门问题的答案

### Q1：Editor API 能否可靠生成与参数化？

答案：☑ 能 / ⬜ 有限制地能 / ⬜ 不能

说明（含所有踩到的 API 坑，供 S6 编译器开发时查阅）：

- `ParticleSystem.main`、`emission` 等 struct 模块访问器的标准写法在 Editor 下可靠，`SaveAsPrefabAsset` + `AssetDatabase.SaveAssets` 后跨进程持久化成功。
- Burst 常量值应从 `Burst.count.constant` 读取，不要用已废弃的 `minCount` 判断常量模式结果。
- 材质副本应先完成全部属性赋值再创建资产，或创建后经过一次导入规范化；正式版更应在 build hash 未变化时直接跳过资产写入。
- 不能在发出 `EditorApplication.Exit` 前就把磁盘 hash 当成最终值；首次进程退出尾声仍观察到材质再次序列化，外层流程应等待进程完全结束后再比较文件。
- 覆盖同一路径 `SaveAsPrefabAsset` 会保留 `.meta` 和 GUID，场景引用不断裂；禁止先删旧资产再创建。
- `GetPrefabAssetType` 不足以判断生成 Prefab 内部的子节点是否为 Nested，正式版若需诊断关系，应使用 Prefab 内容作用域/实例 API，不依赖本 Spike 的 YAML 文本检查。

### Q2：幂等在哪一层成立？

⬜ 字节层（两次构建文件完全一致）
☑ 结构层（第一次重复构建字节有差异，但层级/组件/参数值一致；差异来源：4 个粒子材质的 `_Color.b` 一次性从 `0.079999976` 规范化为 `0.08`；规范化后的第二→第三次构建已达到字节一致）
⬜ 不成立（说明原因）

结论对 S6 的约束：Build Hash 与“unchanged”判断应在**规范化的 Recipe + 模板/编译器版本这一输入语义层**比较；输入 hash 未变就跳过写盘。输出资产可用结构/参数复核，不能把第一次创建后的原始文件字节直接当作唯一真值。首次创建材质时应在 `CreateAsset` 前完成赋值，以减少一次性规范化 diff。

### Q3：组装策略定版

**选定：☑ A 深拷贝 / ⬜ B Nested 引用**

理由（对照实测项 5/6/7/8）：

- A 在显式重建前不会继承模板变化，生成结果只在编译器执行时变化，符合“Recipe + 模板版本 → 受管生成物”的确定性边界。
- A 与 B 的 Prefab 本体在同输入重复构建中都稳定，GUID/场景引用也都稳定，因此无需为幂等性牺牲独立性。
- A 的代价是本样例 Prefab 约 238,590 bytes，而 B 约 11,435 bytes；MVP 的受管资产数量有限，这个体积代价低于隐式传播带来的追踪成本。

落选方案被排除的关键证据：

- B 的未 override 属性会在不执行 Build 的情况下随模板改变；实测 T_Embers startColor 在生成 Prefab 重建前已从橙变青。此时 Recipe 与 build hash 都没有变化，却改变了运行表现。
- B 需要长期维护 override 与模板演进规则；模板删除/改层级也会扩大失效面，不适合作为首版唯一组装策略。

## 3. 遗留问题与移交事项

| 事项 | 影响的正式阶段 | 处理建议 |
|---|---|---|
| 首次创建的粒子材质跨进程重建时有一次浮点规范化 diff | S6 | 创建材质时先配置再 `CreateAsset`；以规范化输入 hash 判断 unchanged，避免无意义写盘 |
| `Burst.minCount` 在常量模式可返回无效的旧字段 | S5/S6 | 使用 `Burst.count.constant`；为 SetBursts 跨存盘写 EditMode 测试 |
| 覆盖同路径可保 GUID；删除再新建会使引用面临断裂风险 | S6 | 就地 `SaveAsPrefabAsset`，禁止删除 `.meta`；将 GUID/场景引用稳定性纳入回归测试 |
| Nested 属性会在不重建时随模板传播 | S6 | 首版定版深拷贝；若未来引入 Nested，必须把模板依赖版本纳入 build hash/dirty 状态 |
| 批处理 PlayMode 在本机写出 PASS 后 `EditorApplication.Exit` 仍不结束进程 | S3/S11 | 正式自动化用 Unity Test Runner `-runTests`，外层脚本设置超时并保留日志；不要照搬 Spike 的自制 PlayMode 控制器 |
| `AssetDatabase.StartAssetEditing` 未在本 Spike 使用 | S6 | 若正式编译器使用，必须 try/finally 配对 `StopAssetEditing` |

## 4. 退出判定

对照 EXECUTION_PLAN §1.6 逐条勾选：

- ☑ 硬编码字典生成的组合 Prefab 可在 PlayMode 播放
- ☑ float 数值 / Burst / 材质颜色三类参数均验证写入与持久化
- ☑ 幂等结论明确（Q2 已选定结构/输入语义层；规范化后输出字节稳定）
- ☑ 组装策略已定版且理由成文（Q3）

判定：☑ 通过，进入 S2 / ⬜ 失败，按 §1.6 失败处理流程止损

## 5. Spike 代码处置确认

- ☑ 已确认 Spike 代码未复制进任何正式目录（实现和资产全部位于 `spike/image_to_smart/`，未创建或修改正式 `project/`）

## 6. 证据与复核入口

- 复核说明：`spike/image_to_smart/SpikeEvidence/README.md`
- 一次性 Runner：`spike/image_to_smart/Assets/Editor/Spike/S1SpikeRunner.cs`
- 生成资产：`spike/image_to_smart/Assets/Generated/`
- 模板资产：`spike/image_to_smart/Assets/SpikeTemplates/`
- 场景引用：`spike/image_to_smart/Assets/SpikeScenes/S1ReferenceScene.unity`
- 完整 Unity 日志、SHA-256、git diff 与阶段结论：`spike/image_to_smart/SpikeEvidence/`

## 7. 主 Agent 独立验收

> 验收日期：2026-08-22  
> 验收角色：主 Agent（未参与 Spike 实现）  
> 判定：**通过，允许进入 S2**

独立复核内容：

- 使用 `Start-Process -Wait` 独立运行 Unity `VerifyStableRebuild`，退出码为 `0`。
- 重建前后重新计算 8 个 `.prefab/.mat` 文件的 SHA-256，差异数为 `0`。
- 独立复核生成 YAML：DeepCopy 含 `0` 个 `PrefabInstance`，Nested 含 `3` 个。
- 独立复核 Prefab `.meta`：DeepCopy GUID 为 `3e4b78b4c1dde734bae3a6282dd5256b`，Nested GUID 为 `1d70dedf83897f848a16d4ab4a32a83e`，与纪要一致。
- 检查最终 Phase 1/4 日志，未发现编译错误、运行异常或失败标记；独立重建日志见 `SpikeEvidence/acceptance-unity-phase2b-waited.log`。

非阻塞移交项：PlayMode 自制批处理控制器退出异常不影响本阶段对 Prefab 播放行为的判断，但 S3 正式测试脚本必须采用 Unity Test Runner，并由外层脚本实施进程超时与日志保留。
