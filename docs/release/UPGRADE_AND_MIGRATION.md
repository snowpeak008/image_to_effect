# 升级与迁移边界 — 0.1.0

当前发布没有 Recipe v1→v2 迁移器，因为 v1 在 2D 闭环和 S10 3D 扩展中未发现破坏性字段缺陷。不要为了升级包版本修改 `recipeVersion`、Manifest 字段或 Recipe 语义。

允许的 0.1.x 操作：在同一 Unity 2022.3.62f3c1 基线内升级包/编译器，然后对每个受管 Recipe 执行 Validate、Dry Run、Build。compiler/dependency hash 变化会要求重建，但构建在同一路径就地更新并保留正式 Prefab `.meta` GUID；场景引用应持续有效。先备份项目，再逐个复跑 A1–A8 和完整套件。

必须停止并立项的边界：Recipe/Manifest 字段删除或改义、模板参数删除/改义、需要改变 v1 Patch 路径、Unity 大版本升级、Generated→手改永久保留（Detach/Bake）、或需要真机性能认证。破坏性 Recipe 变更必须提高 `recipeVersion` 主版本、提供显式迁移器和已生成样例迁移测试；不能静默“兼容”或篡改旧 Recipe。

回滚：若新 Build 失败，Compiler/Patch 事务保留上一次成功 Generated 输出。若要回退包版本，恢复已验证的项目文件，然后以该版本的 Compiler 对原始 Recipe 重建；不要手工编辑 BuildManifest 或 Unity YAML。

## Recipe v1 契约修订 1.2（能力批次）

本次是向后兼容的同主版本契约修订，不改变 JSON 中的 `recipeVersion: 1`。一次性加入两个可选顶层块：

- `style`：接受旧字符串 `"stylized"`，或新的对象块；对象块在本次迁移完成字段、token、类型和范围校验，具体风格 Shader/资产实现留给 W1。
- `behavior`：包含可选的 `motion / hit / emission / timing` 四个子块；每块的 `type` 必须在 `CapabilityRegistry` 登记，参数必须符合登记合同，组合必须通过合法性表。

兼容保证：旧 Recipe 不写两个块时，Parser 采用旧缺省语义；文件不重写，canonical hash 与 Generated 构建输入保持不变。新能力 Recipe 才显式写入 `behavior`。从旧字符串 style 切换为对象 style 会改变 Recipe hash，属于用户明确选择的风格迁移。

迁移回滚：删除新 Recipe 中的 `behavior` 和对象式 `style` 即回到原 v1 缺省行为；不得修改旧正式 Recipe 来“补齐”可选字段。Slash Recipe v2 继续由隔离的 Slash v2 Parser/Compiler 管理，不经过本契约。
