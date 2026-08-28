# 3D Style Pipelines（W10）

> 状态：源码与机器协议完成；视觉签署待最终用户验收。

## Semi-real

`style.token=semireal`；专项 Recipe 必须声明 `noise_primary_speed=0.3` 与 `noise_detail_speed=1.7`，表达大形/细节双层异速。无 Bloom 后处理依赖。

## Holographic

`style.token=holo`；`glitch_rate` 与 `glitch_offset` 进入 Runtime。故障步进由 Recipe seed 与离散时间片计算，相同 seed/时间得到相同 offset，且幅度不越界。

## Dark ritual

`style.token=dark`；主体/次色保持低明度，accent 是唯一亮色豁免。共享 Rune/Noise 资源只作为 dependency。

`VFXPREVIEW_Style3D.unity` 保存正视主相机与关闭状态的 45° 斜视相机，供最终人工双机位验收。
