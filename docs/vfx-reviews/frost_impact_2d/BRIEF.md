# Frost Impact 2D — MVP Brief

- EffectId：`frost_impact_2d`
- Revision：`3`
- Archetype：`impact`
- Dimension：`2d`
- Lifecycle：`one_shot`
- Topology：`point`
- Attachment：`world`
- Target Profile：`pc_editor`
- 计时起点：`2026-08-23T14:00:00+08:00`

## 视觉目标

一次清晰、紧凑的冰霜命中：白青色核心闪光先出现，蓝色放射碎片向外飞散，随后薄冰环扩张并快速淡出，少量细雪尾粒子收尾。背景使用深灰色；不使用 Bloom 作为可读性前提。

## 时间线

| 时间 | 目标 |
|---:|---|
| `0.00s` | 空帧/刚触发 |
| `0.05s` | 白青核心达到峰值，第一批碎片可读 |
| `0.12s` | 冰环展开、碎片形成方向性轮廓 |
| `0.25s` | 核心消失，只剩环与少量雪粒 |
| `0.48s` | 完全清空，可安全回池 |

## 成品预算

- Runtime Entry：严格 `1` 个 Prefab。
- GameObject：`<= 8`；最大深度 `<= 1`。
- ParticleSystem：`<= 6`；序列化容量合计 `<= 40`。
- Local Material/Texture/Shader：全部为 `0`；只引用版本化 Shared 资源。
- Generated 成品目录：只允许 Runtime Prefab，不允许截图、测试、旧版本资源或内置 BuildManifest。
- Recipe：`1` 个，位于 `Assets/VFX/Recipes/Impact/`。
- 权威 Manifest：`1` 个，位于 `ProjectSettings/VFXComposer/BuildManifests/`。

## Module Decomposition Table（Revision 3）

| 视觉角色 | 来源/运行时单元 | 变体 | Pivot/方向 | 复用计划 |
|---|---|---:|---|---|
| Core/Flash | 程序化 Core Shader | 2 层参数变体 | 中心 | Frost Impact/Spawn |
| Broken Ring | `M_Frost_BrokenRingSegments_v1` + 程序化 Ring Shader | 16 段合并为 1 Mesh | 中心 | Frost Impact/Aura/Area |
| Mist Ring | 程序化 Mist Shader | 8 个粒子参数变体 | 环形分布 | Frost Impact/Aura/Area/Environment |
| Ice Shard | `T_Frost_ShardAtlas_A_v1` | 4 | 尖端 `+X`，Stretch 沿速度 | Frost Impact/Projectile/Area |
| Snow Mote | 程序化 Mote Shader | 运行时尺寸/速度随机 | 中心 | Frost 全类型次级粒子 |

完整目标图只用于视觉方向和逐模块比较，不进入 Player。当前 Shared 使用由本效果登记，同时批准后续 Frost Aura、Area 和 Projectile 复用计划；在出现第二个消费者前不得建立更大的 Frost Mega Atlas。

## 本轮提速口径

只统计从 Brief 冻结到以下条件同时满足的墙钟时间：Unity 编译通过、严格 Build 成功、定向 EditMode/PlayMode 通过、Preview Scene 可播放。视觉最终通过仍由用户在 Unity 中观看后签署，不把机器通过冒充视觉通过。

## 视觉验收记录

- `2026-08-23` 首轮：**拒绝**。用户实际 Game 画面为不透明青色方块和白色矩形射线，未形成目标中的透明冰晶、核心层级与冲击环。
- 拒绝证据：`rejected/2026-08-23-initial-opaque-rectangles.png`
- SHA-256：`6B8C0FED978A8F337AB2A677E242DC4734E6F0CAA27A7F5A5DD6EFC58E809816`
- 直接原因：URP 粒子材质仍以 Opaque 状态渲染，纹理 Alpha 未参与最终画面；Stretch 粒子因此暴露完整矩形面片。
- 当前处理：失败版本不得作为视觉通过证据；修正透明 Shader、纹理导入和冰晶拉伸参数后重新 Build、重新观看、重新签署。
- `2026-08-23` 第二轮：**拒绝**。Shader 已切换为透明，但画面仍出现半透明青色方块，主要冰晶和圆环不可读。
- 第二轮证据：`rejected/2026-08-23-second-alpha-math-failure.png`
- SHA-256：`70233E2EEDCB5A504C7134802A88F7D6E6A03F7CD76E7921596C5F813B86D67D`
- 精确根因：纹理生成器误把 Unity `Mathf.SmoothStep(from,to,t)` 当作 HLSL `smoothstep(edge0,edge1,value)`；实测 Ring Alpha 范围为 `204..255`，Shard Alpha 仅为 `0..45`。现改为显式 `InverseLerp + SmoothStep(0,1,t)`，并增加 PNG 像素级回归测试。
- `2026-08-23 15:19 +08:00` 的三独立纹理候选现已废弃并移出活动依赖；其单张 637KB 冰晶和单层规则环不符合新视觉模块/Atlas 流程。
- `2026-08-23` Revision 2 候选：按 Frost Family 模块重建，真实 PlayMode、同一序列化 Preview Camera、单次 `Play()`、自然 `Update` 录制完成。两组共 10 枚大小/角度错开的冰晶、独立破碎环、雾环、核心和雪粒均来自正式 Prefab；`0.483333s` 清空。**工程门禁和内部候选审图完成，最终视觉结论仍由用户在 Unity 中签署。**
- 当前证据：`evidence/current-run/`，包含空帧、`0.05s`、`0.116667s`、`0.25s`、完成帧和 `metadata.json`。
- Revision 2 定向门禁：EditMode `4/4`；Runtime PlayMode `1/1`；真实图形设备视觉录制 PlayMode `1/1`；全部 `0 failed`。
- Revision 2 严格产物：Generated 目录仅 `VFX_frost_impact_2d.prefab`；权威外部 Manifest 记录 `1` 个 owned output、`7` 个 GameObject、深度 `1`、`6` 个 ParticleSystem、容量合计 `38`、`5` 个共享材质、Local Texture Bytes `0`。
- `2026-08-23` Revision 3 拆解候选：完整 Impact Atlas 已退出正式依赖；冰环改为 16 段合并 Mesh，Core/Mist/Mote 改为程序化，12 枚冰晶只复用 22,527 B 的 2×2 静态 Atlas。正式依赖驻留纹理由 `329,040 B` 降为 `66,216 B`，运行时 PNG 源文件由 `184,844 B` 降为 `22,527 B`。定向 EditMode `5/5`、Runtime `1/1`、视觉录制 `1/1`；状态仍为 `pending visual acceptance`。

## 实际耗时结论

- Brief 冻结：`14:00`；最终候选：`15:19`；完整墙钟约 `79` 分钟。
- 这证明标准目录、统一 Runtime Entry、严格 Manifest、自动 Build 与定向测试显著缩短了**工程搭建和验证**；本次从 Unity 关闭、开始独立批处理修正到最终候选约二十余分钟。
- 但“AI 首次视觉命中”没有通过：前两轮需要用户截图才暴露 Alpha/数学错误，之后又经过多轮自动关键帧审图。提速真实存在于工程流水线，不应夸大为视觉一次生成成功。
