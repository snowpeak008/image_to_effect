# W13 / W18 Composite Ultimate 与角色主题套装开发报告

> 日期：2026-08-25  
> 状态：**源码、构建与机器门禁完成；W13/W18 用户视觉结论均为拒绝（批量签署）**  
> 范围：`docs/allwork/14_COMPOSITE_ULTIMATE.md`、`docs/allwork/19_CHARACTER_THEME_KITS.md`

## 1. 交付结果

- W13：6 个大招/Boss Composite Runtime Entry。
- W18：8 个角色专属 Runtime Entry、1 个百鬼夜行 Composite 组件、4 个角色套装 Showcase Composite。
- 合计：19 份默认 Recipe、19 份稳定语义 Patch、19 个 strict Runtime Entry；其中 11 个是纯依赖编排 Composite。
- Preview：`Assets/VFX/Preview/VFXPREVIEW_Ultimate.unity` 与 `Assets/VFX/Preview/VFXPREVIEW_HeroKits.unity`。每个场景只有一个序列化相机和一个 Preview-only selector；Preview Driver 被生产规则禁止进入 Runtime Entry。

## 2. Composite v1 契约

Recipe v1 增加：

- `timeline[]`：`t / ref_id / action(play|stop) / overrides`；override 只允许 palette、scale、position、rotation。
- `camera_hints[]`：shake、zoom、slowmo 数据通道；Runtime 不拥有游戏相机。
- `gates[]`：等待精确外部事件；通过 `gate:<id>` 或 `ReleaseGate(id)` 放行。

Parser 保持闭合字段；Validator 使用 E1850–E1855 拦截非 Composite 滥用、缺失时间轴、乱序/非法引用、非法 override、相机提示和阶段门错误。Composite Patch 使用专用 Validate/Build 路由，不能再被 Projectile 的移动端总时长预算误判。

## 3. 构建与所有权

- Composite Prefab 根节点只有 `CompositeVfxController`，不复制任何子 Prefab 层级。
- 子项路径读取其权威 Build Manifest 的 `runtimeEntry.path`，不能由 id 猜测文件名。
- Prefab 仅序列化子 Prefab 引用；运行时首次播放建立固定池，后续 replay 复用。
- 输出目录只拥有 `VFX_<id>.prefab` 与 `Composition.json`；共享材质、纹理及子 Prefab 全部是递归依赖。
- Build Hash 包含 Recipe、compiler version、Unity version 和每个子 Runtime Entry 的递归 dependency hash；二次构建字节与 GUID 保持稳定。
- `stop` 是确定性的即时停止并隐藏对应运行实例，使机器峰值预算与真实存活区间一致。

## 4. 峰值预算

11 个 Composite 的最大记录值均未超过放宽档：粒子 200、ParticleSystem 10、材质 10、Renderer 14。代表性峰值：

| Composite | particles | PS | materials | renderers |
|---|---:|---:|---:|---:|
| meteor_shower_ultimate_3d | 144 | 3 | 3 | 12 |
| blade_tempest_ultimate_3d | 74 | 6 | 8 | 14 |
| flame_blade_samurai_kit_showcase_3d | 74 | 6 | 8 | 14 |
| ghost_curse_shrine_kit_showcase_2d | 48 | 3 | 3 | 14 |

开发中发现的超峰值没有通过放宽阈值解决，而是为 warning、focus、gale、trail 和嵌套 ultimate 写入明确 stop，改成分段复用。

## 5. 机器验证

- Compile：通过。
- 定向 EditMode：`6 total / 6 passed / 0 failed`，结果 `test-results/w13-w18-edit-v13.xml`。
- 定向 PlayMode：`5 total / 5 passed / 0 failed`，结果 `test-results/w13-w18-play-v1.xml`。
- EditMode 覆盖：Schema 正/反例、19 个正式条目与 Patch、二次构建幂等、GUID、依赖零复制、预算、主题 palette、Preview 场景、Manifest 完整性。
- PlayMode 覆盖：连续时间轴、子实例池复用、相机提示、Boss 阶段门、8 连斩同源多实例、套装复播清理、显式释放池。

最终总回归：EditMode `224 total / 189 passed / 0 failed / 35 historical Explicit skipped`；PlayMode `44 total / 38 passed / 0 failed / 6 visual-capture Explicit skipped` 且 Unity exit 0。Windows Player Build 已将两个 Composite Preview 与四个正式基线 Scene 一并序列化，`1/1 passed`，外部临时构建清理完成。Git ignore audit 与 Generated 残留审计通过。

## 6. 用户视觉签署（2026-08-25）

用户原话：

> 拒绝，无法商用，后续的特效类，都不验收了，这是通病了，都是同样的拒绝，无法商用，

用户明确决定不再逐 Scene 检查后续特效类，并将 W13 大招/Boss 与 W18 角色套装的当前候选统一签署为**拒绝**。`VFXPREVIEW_Ultimate.unity` 与 `VFXPREVIEW_HeroKits.unity` 未作逐套/逐帧视觉核对；本记录不伪造时间节奏、构图、主题一致性、镜头提示或阶段过渡的单项观察结论。

“无法商用”只记录为用户对视觉制作完成度的评价，不作版权、许可或法律解释。两个当前候选均记为 `rejected`；机器通过仍只证明结构、运行协议、资源所有权、预算和清理正确。本次签署未授权重做、修改源码/资产或生成下一候选。
