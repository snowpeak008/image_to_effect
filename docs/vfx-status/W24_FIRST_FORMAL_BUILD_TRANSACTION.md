# W24 首次正式构建原子性报告

状态：源码与静态检查阶段；不构成视觉、C0 证据、L3 或 L4 结论。

## 目标

首次正式构建在 S5 pre-C0 准入通过后，仍可能在 Prefab、预览场景、Manifest 或 C0 candidate 写入时失败。`W24FirstFormalBuildTransaction` 为这段 authoring 区间提供精确快照/回滚边界：成功才保留结果；失败则恢复开始前的字节与 `.meta` GUID。

## 受控目标

- S0b：Recipe、`sustained_flame_3d` 效果输出根、预览 Scene、正式 Manifest、C0 candidate 根。
- S3：三份 Recipe、三条效果输出根、三个预览 Scene、三个正式 Manifest、三个 C0 candidate 根。

事务不接受重叠目标；每个失败路径都反向恢复目标，并执行同步 `AssetDatabase.Refresh`。第一次构建原本不存在的目标会被完全删除；已有目标会按构建前状态恢复。

## 所有权收紧

- S0b 的五个 Material 移入 `Assets/VFX/Effects/Aura/sustained_flame_3d/Materials/`。
- S3 的三个 Material 分别移入各自 effect output root。
- S0b 预览改为只读使用当前项目默认 Renderer；不再复制 RendererData 或编辑 active URP Pipeline Asset。
- Shader 仍为只读依赖，不属于本构建可写对象。

因此，失败回滚不需要、也不允许修改共享材质、RendererData、管线配置或任意非本 effect 所有的资产。

## 故障注入与验证

`W24FirstFormalBuildTransactionTests` 覆盖：

1. 已有状态：在所有目标写入后注入异常，逐字节比较输出、Recipe、Preview、Manifest、candidate 和 `.meta`；
2. 首次状态：异常后每个目标与根 `.meta` 都不存在；
3. 所有权：S0b/S3 Material 路径都位于对应 effect 输出根；
4. authoring fault seam：S0b/S3 分别在 bootstrap receipt 前与 C0 freeze 后触发，可验证 S5 commit 或 candidate freeze 出错时也回滚。

Unity EditMode/Player 的正式运行验证仍待在独立 shadow project 执行。本报告不声称该验证已经完成。
