# 静态性能预检 — 内部 MVP 0.1.0

结论：**静态预检，非真机认证。** 本报告不是 CPU/GPU 帧耗时、Draw Call、内存或移动设备性能结论；PC Editor 是本 MVP 的唯一实际目标，`mobile_medium` 仅为静态预算档位。

数据由正式 Recipe、递归扫描的 Catalog/Manifest 和两个已生成 `BuildManifest.json` 采集。正常 EditMode 测试 `StaticPerformancePreflightReport_MatchesLiveRecipesCatalogAndGeneratedManifests` 会重新读取这些真实输入，对**每个** Recipe 显式计算 `mobile_medium` 与 `pc_editor` 两档 live profile，验证本表数字、limits、warning codes 和无 budget error，避免手填结果漂移。

| Recipe | peak particles | materials | trails | total duration (s) |
| --- | ---: | ---: | ---: | ---: |
| fireball_2d | 129 | 7 | 1 | 1.62 |
| fireball_3d | 83 | 8 | 1 | 1.62 |

| Profile | particles | materials | trails | duration (s) | Static result |
| --- | ---: | ---: | ---: | ---: | --- |
| mobile_medium | 200 | 8 | 2 | 6 | fireball_2d: W402; fireball_3d: W402 |
| pc_editor | 1000 | 32 | 8 | 30 | fireball_2d: pass; fireball_3d: pass |

`W402` 是 materials 达到 profile 80% warning threshold（2D 为 7/8，3D 为 8/8）；两者仍无 budget error。3D 的 8/8 仅说明静态 profile 恰达上限，不可被表述为真机性能认证。

采样范围不包含动态灯光、Distortion、碰撞或 Sub Emitter：Recipe v1 没有将它们暴露为可写输入，当前正式模板/Manifest 也未登记它们。后续若要求设备性能认证，必须另立目标设备、压力场景、帧时间、Draw/SetPass、GC、内存和多实例实测门禁；不得以本报告替代。
