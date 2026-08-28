# 2D Style Pipelines（W9）

> 状态：源码与机器协议完成；视觉签署待最终用户验收。

## Pixel

Recipe 使用 `style.token=pixel`，并声明 `snap_fps`、`palette_lut`、`virtual_res`。Runtime 将 `_Phase` 按 `snap_fps` 离散采样，禁止以 60fps 平滑插值伪装像素动画。共享 LUT/Mask 位于 `Assets/VFX/Shared/Styles/Textures`。

## Cartoon / hand-drawn atlas

Recipe 使用 `style.token=cartoon`，并声明 `atlas_id`、`atlas_fps`、`loop_mode`。共享的 `T_AnimeSmearAtlas_256.png` 是可复用最小程序图集；主体图集与程序碎片仍须在模块层分工。Runtime 按 atlas fps 离散 `_Phase`。

## Ink wash

Recipe 使用 `style.token=inkwash`，并声明 `ink_density`、`bleed_radius`、`flyaway_threshold`。主色/次色保持墨阶，accent 是唯一点缀色。共享 Brush mask 禁止复制进单个 Generated 目录。

所有三条管线均由 strict Recipe、共享依赖、幂等 Prefab 和 Preview Scene 组成；人眼只在最终统一视觉阶段签署笔触、帧感和色域观感。
