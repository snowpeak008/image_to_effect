# W24 S6 VFX Graph 兼容性 Spike 报告

状态：**DEFER — 只有资料核查；没有添加 package、没有 Graph、没有 Player/Batch/Capture/编译器/Patch 运行。**  
观察日期：2026-08-25  
项目事实：`project/ProjectSettings/ProjectVersion.txt` 为 Unity `2022.3.62f3c1`；`project/Packages/manifest.json` 为 URP `14.0.12`，且 manifest/package lock 未声明 `com.unity.visualeffectgraph`。

外部资料只以链接和本报告的概述记录；没有把 Unity VFX Graph 外部源码/示例复制进仓库。

## 结论

W24 §11.3 的“只做隔离 Spike、不是 Shuriken 前置替换”仍然成立。资料显示 Unity 2022.3 的 URP 路线可使用 VFX Graph 的一部分 GPU particle 功能，但官方同一版本的 VFX Graph 页面仍说 URP（及 URP-compatible mobile）的完整支持在开发中。因而对本项目是 **DEFER，不是 GO**；尤其不能把“有部分 feature support”写成 Windows Player、批处理 capture、序列化、自动生成、Patch 或回滚已经可靠。

## Unity 一手资料证据

- Unity 2022.3 的 VFX Graph 产品页：HDRP 是 production-ready，而 URP 与其兼容移动设备的完整支持仍在开发中；核心 package version 固定随 Editor 版本。[Unity 2022.3 Visual Effect Graph](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.visualeffectgraph.html)
- Unity 2022.3 render-pipeline feature matrix：URP 的 VFX Graph 需要 compute-capable hardware，**不支持 OpenGL ES**；表内列出 VFX Shader Graph/Lit/Unlit/2D Sprite Lit/2D Custom Lit，3D particles/sorting layers/sprites；不支持 2D Physics、2D sprite emitters 或 2D Lights。[Unity 2022.3 feature comparison](https://docs.unity3d.com/2022.3/Documentation/Manual/render-pipelines-feature-comparison.html)
- 同一矩阵明确列 URP 下 VFX Graph soft particles 为 Yes，而 distortion 为 No；trail 标为 experimental。Lit/Unlit 不应据此等同“当前项目所需的所有光照/拖尾/畸变均可用”。[Unity 2022.3 feature comparison](https://docs.unity3d.com/2022.3/Documentation/Manual/render-pipelines-feature-comparison.html)
- Unity 的 VFX Graph 14.0 requirements 页面也把 URP 描述为 preview，称其只覆盖 URP 支持的平台子集、缺少 HDRP 的部分 feature，且 URP 不支持 gamma color space。该 package 文档的 Editor-version table 与 2022.3 LTS 页面并不完全一致，故不能只凭该表决定 package version；以目标 editor/package 的实际隔离安装和 lockfile 为准。[VFX Graph 14.0 requirements](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@14.0/manual/System-Requirements.html)

## 风险矩阵

| 维度 | 当前判断 | 必须在未来隔离运行中证明 |
|---|---|---|
| Package/version | 未安装，且 14.0 requirements 的 Editor compatibility 表与项目 LTS 组合存在文档歧义 | 仅在副本安装精确版本；记录 manifest/lock diff、package hash、Unity/URP version；可完整卸载恢复。|
| Feature carrier | 部分 URP 支持，但 distortion 不支持、trail experimental、2D 限制明显 | 一个独立 Graph 覆盖 Unlit、Lit、Trail、Depth、所需替代方案；没有以 shader/particle substitute 偷换合同。|
| Windows Player | 官方 feature table 不是本项目 Player 成功证据 | Windows Player Build，启动、生命周期、目标图形 API/compute capability、日志与至少一个 deterministic seed。|
| Batch/capture | 未运行；W24 规定 capture 是带图形设备 batchmode，禁止 `-nographics` | `-batchmode` + graphics device（无 `-nographics`）下正式相机 Beauty/diagnostic metadata、hash、稳定 seed、失败日志。|
| Serialization | Graph YAML/subasset 与 package/import 版本可能改变 | 关闭/重开 Editor 后 GUID、Graph/subasset references、Prefab/scene、manifest hash 与 source control diff 稳定。|
| Compiler | 现有 compiler 是受限 Recipe/Prefab/allow-list pipeline，未生成 Graph | 干跑 plan→受限 Graph/Prefab 生成→重复 build 字节/semantic stability；未知 node/setting 必须拒绝而非反射写入。|
| Patch | 现有 Patch 有语义 allow-list、revision、dry-run、transaction snapshot/rollback；对 Graph 无覆盖声明 | 一个 allow-listed Graph parameter Patch；revision/hash 保持，失败必须还原精确 Graph/Prefab/manifest bytes 与 GUID。|
| Ownership | Generated 输出必须隔离，shared dependency 不能多重拥有 | Production Manifest 列出 Graph、subasset、Prefab 与 dependencies；只写本 effect 的 owned output。|
| Performance/batching | 未测；feature support 不代表 budget 或 batching | target Windows hardware 的 profiler/batch count/GPU memory/peak particle budget，且与 Shuriken baseline 对比。|
| Evidence/visual state | 没有 capture、QA 或用户验收 | 输出四路 evidence；不产生 L3/L4，不把图表或源码称为视觉结果。|

## 精确的未来隔离执行矩阵

下表是未来的**计划**，不是已经执行的结果。每一行均先在独立 project copy 完成；任何失败立即记录并停止进入主工程。

| 阶段 | 输入与前提 | 允许动作 | 通过条件 | 失败/回滚 |
|---|---|---|---|---|
| G0 package admission | 锁定 Unity 2022.3.62f3c1、URP 14.0.12、精确 VFX Graph candidate、许可证/commit/hash | 仅副本修改 manifest，保存前后 manifest/lock hash | 可解析、无非预期依赖、卸载后字节恢复 | 还原副本 manifest/lock；主项目零变化。|
| G1 authoring/serialization | G0 pass、单一 isolated Graph + Prefab | 保存、重开、重新导入、生成 manifest | GUID/引用/Graph 结构可复核且 stable | 删除整个隔离副本；不触及正式 Generated。|
| G2 semantic carrier | 冻结合同、单一 carrier matrix | 仅合同允许的 Lit/Unlit/Trail/Depth 测例 | 所需 feature 在目标 URP 下真实存在；禁止 substitute | feature gap 为 NO-GO 或回 Design，不能静默降级。|
| G3 compiler/Patch | G2 pass、ownership manifest、exact plan/input hash | dry-run first；单个 allow-listed parameter transaction | 两次构建稳定；Patch revision +1；失败回滚 Graph/Prefab/manifest/GUID | 精确 snapshot restore；rollback failure 留可见诊断。|
| G4 Player/batch/capture | G3 pass、Windows compute-capable runner、正式 capture profile | Windows Player；batchmode with graphics only；3 seeds | lifecycle、logs、Beauty + diagnostics + telemetry hashes 完整 | 证据无效则按 W24 重采；不宣称 runtime pass。|
| G5 decision | G0–G4 evidence + cost comparison | 只输出 report | 用户批准的 scoped carrier adoption 或 DEFER/NO-GO | 没有批准则删除隔离副本，主线维持 Shuriken。|

## 不作出的声明

本报告没有安装 `com.unity.visualeffectgraph`，没有创建 `.vfx`，没有运行 Unity GUI、batchmode、Player Build 或测试；也没有实际验证 Lit、Unlit、Trail、Depth、Distortion、batch/capture、序列化、编译器、Patch、交易或 rollback。因此没有 runtime result、性能数据、视觉结果、L3、L4 或 Coplay integration claim。

W24 §11.3/§16/§17/§22 的路径保持不变：VFX Graph 只在 2022.3 URP 的隔离兼容 Spike 通过后，才可针对明确受益的 GPU 粒子/复杂模拟提出 adoption 决策；当前生产底座仍是 Shuriken/Trail/Mesh/URP shader/light 的既有路线。
