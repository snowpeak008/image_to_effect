# Inferno Vortex Area 2D — Gate 0 Brief

状态：`implemented / pending user visual acceptance`  
EffectId：`inferno_vortex_area_2d`  
Archetype：`area`  
Dimension：`2d`  
Lifecycle：`sustained / event_driven`  
Topology：`area`  
Attachment：`world`  
Target Profile：`pc_editor`，保留 `mobile_medium` 降档  

## 1. 视觉目标

一个持续燃烧的炼狱火焰领域，不是简单圆环或一次性爆炸。最终实现以连续程序化火焰盘承载暗红火体，两层错相热纹与中心热核形成六臂旋涡，周期脉冲只在 Tick 时短暂出现，小型火星负责外围破形。白黄色只用于少量高温边缘，橙色为主体，深红负责体积和残留，避免中心整片过曝。

目标参考：[reference/target-v1.jpg](reference/target-v1.jpg)  
SHA-256：`B574E940D3752C3531CF100AFB3C259377533CF6BBFF371A76E7CC4A8BA93381`  
说明：目标图由内置 ImageGen 生成，只约束构图、层级、颜色和复杂度。项目内保存的 `1024×1024` JPEG 为 `259,124 B`，只位于 `docs`，不会进入 Unity `Assets`、Player 或 Runtime 依赖。

## 2. 明确不做

- 不把完整目标图或其中完整火环裁成 Runtime Sprite。
- 不使用 Flipbook/序列帧表现主体燃烧。
- 不通过复制多张火焰 PNG 增加层数。
- 不接受单线火圈、规则星芒、均匀齿轮或重复图章。
- 不把中心白色裁满，也不依赖 Bloom 掩盖层级不足。
- 本轮只实现视觉 Area 与生命周期接口，不实现伤害、Tick 数值、寻路或网络同步。

## 3. Runtime PNG 硬预算

| 项目 | 约束 |
|---|---|
| PNG 数量 | MUST `1`，不得新增第二张局部纹理 |
| 尺寸 | MUST `256×256`，单通道/灰度语义 |
| Source PNG | 目标 `<= 48 KiB`，硬上限 `64 KiB` |
| 内容 | `2×2` Mask Atlas：主火舌A、主火舌B、火星/余烬、烟焰Breakup |
| 颜色 | 不烘焙；由 Shader Gradient/参数生成 |
| 动画 | 不烘焙；由 UV流动、顶点变形、相位、粒子轨道和生命周期生成 |
| PC导入 | 优先 BC4、无 Mipmap，预计 `32 KiB` GPU |
| Mobile导入 | 优先 ASTC 8×8、无 Mipmap，预计 `16 KiB` GPU |
| 完整依赖 | Manifest 必须报告，不因进入 Shared 而隐藏成本 |

如果最终 PNG 超过 `64 KiB`、依赖中出现完整效果截图或任何序列帧，Gate 3 直接失败。不能为了满足字节数把主体 Alpha 降成细线；体积与成本必须同时通过。

## 4. 动作与生命周期

| 状态 | 时间/规则 | 表现 |
|---|---|---|
| Ignite | `0.00–0.25s` | 六个错相点火沿螺旋建立，外环从断续火舌连接为完整流动区域 |
| Establish | `0.25–0.60s` | 双火带扩至目标半径，内旋涡建立，火星密度上升但中心不裁白 |
| Active Loop | `1.60s` 周期 | 内外层反向旋转；火舌相位错开；循环边界不可见 |
| Tick Pulse | 每 `0.80s` | 一道亮脉冲由内向外传播，短促增强火星，不重启整个效果 |
| Stop | `0.35s` AllowTail | 主火带先降温为深红，停止新火星，余焰收缩并清空 |
| Force Stop | 当帧 | 宿主场景卸载/池回收时清空所有状态 |

Area 使用世界空间，不跟随施法者移动。范围缩放通过统一 Root Scale 和 Shader 参数完成，不复制 Prefab。重复施放同一 EffectId 可选择 Refresh 持续时间，但不得重置循环相位造成亮度跳变。

## 5. Module Decomposition Table

| 视觉角色 | 实现候选 | 节点 | PNG使用 |
|---|---|---:|---|
| Outer Counter-Rotating Field | 两个 Quad Mesh，共享极坐标 Fire Shader，错相/反向旋转 | 2 | 无，纯程序化 |
| Inner Six-Arm Vortex | 一个缩放 Quad Mesh，以极坐标六臂热纹生成 | 1 | 无，纯程序化 |
| Molten Core | 一个小型极坐标热核 | 1 | 无，纯程序化 |
| Tick Pulse | 闭环 Pulse Mesh/Shader | 1 | 无或Breakup Mask |
| Flame Motes | 一个低密度 ParticleSystem，切向轨道+Size/Color over Lifetime | 1 | 火星 Mask |
| Embers | 一个 ParticleSystem，方向性小粒子 | 1 | 火星 Mask |
| Heat Residue | 一个低频 ParticleSystem或合并到火舌系统 | 0–1 | Breakup Mask |
| Runtime Controller | Start/Refresh/Tick/Stop/Pool | Root | 无 |

逻辑拆分允许多个渲染模块，但纹理只允许一个 Shared Mask Atlas。颜色与层次由最多两个共享 Material 变体表达：Alpha/Premultiplied 主体与受控 Additive 高温边缘。

## 6. 结构与运行预算

- Runtime Entry：严格 `1` 个 Prefab。
- GameObject：目标 `8`，硬上限 `10`；最大深度 `1`。
- Renderer：目标 `7`，硬上限 `9`。
- ParticleSystem：目标 `2`，硬上限 `3`。
- Shared Material：目标 `2`，硬上限 `3`；Local Material/Shader/Texture 为 `0`。
- 稳定态粒子：目标 `<= 48`，`mobile_medium <= 28`；运行十分钟不得增长。
- 单实例不创建 `renderer.material`，不产生运行时 Material 泄漏。
- Generated 目录只保留 Runtime Entry 和必要 Data Asset；截图、参考图、测试与 Manifest 不进入该目录。

## 7. 适用经验库

- `EXP-001`：工程门禁与用户视觉签署分开。
- `EXP-002`：PNG压缩、粒子降档和 Shader 合并必须同镜头 A/B，禁止以体积收益覆盖视觉退化。
- `EXP-003/004`：逻辑火带拆分不得显示规则接缝；所有闭环 Mesh 与周期 Shader 双重闭合。
- `EXP-005`：Area Compiler 行为纳入版本或内容 Hash。
- `EXP-006`：更换 Shader 后旧纹理 GUID 必须递归不可达。
- `EXP-007`：Source、Build、GPU 分栏，报告完整依赖驻留。
- `EXP-008`：固定 Preview Scene、同一 Camera、自然 Update 和真实循环。
- `EXP-009`：无缝循环、十分钟粒子稳定、Stop清空、纹理预算与闭环形成回归测试。

## 8. Gate 1 视觉验收

进入 Unity 正式实现前，用户只确认目标方向：

1. 复杂度是否达到“炼狱领域/大招”，而不是简单环或Impact。
2. 双外环、内旋涡、脉冲、火星和暗红外围是否构成清晰层级。
3. 是否接受高温黄白面积受控、橙色主体、深红残留的能量分配。
4. 是否接受运行时只有一张 `256×256 / <=64 KiB` 灰度 Mask Atlas，其余由 Shader/Mesh/Particle 生成。

确认后才进入 Runtime接口、Recipe、Compiler、正式资产和Preview；拒绝时只改Brief/参考，不制造Unity活动目录垃圾。
