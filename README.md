# VFX Composer — Unity 源码开发工作区（内部基线 0.1.0）

VFX Composer 用受审核的 Unity 模板、严格 JSON Recipe、能力协议和事务式 Compiler/Patch 生成可管理的 2D/3D Runtime Entry。当前源码覆盖 Projectile、Impact、Slash、Aura、Area、Beam、Trail、Shield、Spawn、Environment、Screen/UI、新增 Archetype、元素族、14 种风格、30 个行为能力素体、组合大招和角色主题套装。

Projectile v1 与 Slash v2 保持各自权威 Validator/Compiler；AI 只生成受限 Recipe/Patch，不能直接编辑 Unity YAML。Composite 只引用既有 Runtime Entry 并在运行时复用固定池，不复制子特效资源。该仓库仍是内部源码阶段：不宣称外部发布、真机性能认证、Git tag、Package Registry、Unity 内嵌聊天、通用运行时 AI 或 Cocos/UE 适配已经完成。

基线：**Unity 2022.3.62f3c1 + URP 14.0.12**，Windows PC Editor。正式操作入口见 [安装、打开与验证](docs/release/INSTALL_AND_VERIFY.md)；完整计划与状态见 [总索引](docs/allwork/00_INDEX_AND_ACCEPTANCE.md)；源码开发完成后的用户视觉签署顺序见 [最终验收清单](docs/stage-notes/FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md)。
