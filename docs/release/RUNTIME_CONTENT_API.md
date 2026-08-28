# Runtime Content API

> 版本：0.2 development  
> 权威实现：`PlannedContentVfxController` 与 `IVfxRuntimeEntry`

所有 W11/W12/W14/W17 正式 Prefab 根节点只有一个 `PlannedContentVfxController`。通用生命周期仍使用 `Initialize / Play / SendEvent / Stop / ResetForPool / IsAlive`。

## 外部驱动方法

| 方法 | 适用内容 | 约定 |
|---|---|---|
| `SetIntensity(float)` | Environment、Screen/UI、反馈内容 | 输入钳制到 0–1，可运行期连续插值 |
| `SetWind(Vector3)` | Environment | 世界方向与强度；不重新分配粒子池 |
| `SetStackLevel(int)` | `combo_surge_aura_2d`、`poison_veil_ui`、`frost_creep_ui` | 分别钳制到 1–5 或 1–3 |
| `BindExternalRenderers(Renderer[])` | `hit_flash_status_2d` | 只写 MaterialPropertyBlock，不替换或修改材质资产 |
| `SetWorldEndpoints(Vector3, Vector3)` | 链接、飞行收集类 | 只接受有限坐标；非法值记 E1842 |
| `SetAnchorRect(RectTransform, bool)` | Screen/UI 与 Game/UI | `follow=true` 时 anchor 不得为空，否则 E1841 |
| `SetRarity(int)` | card/gacha 内容 | 钳制到 1–5；其它条目调用记 E1840 |
| `SkipToReveal()` | 单抽/十连 | 跳至 reveal 段，不新建对象；其它条目调用记 E1840 |
| `SetFillRatio(float)` | `progress_charge_fx_ui` | 钳制到 0–1；其它条目调用记 E1840 |

`LastProtocolErrorCode` 提供最近一次协议拒绝码；成功的专用方法会清空它。`SendEvent` 对未知事件返回 `false` 并记录 E1840。

## 资源与池化约束

- Screen/UI 与 Game/UI Runtime Entry 必须为 Canvas/Graphic 语义，零 ParticleSystem。
- Environment 允许固定容量的近/中/远粒子层；强度变化不得改变容量或产生持续增长。
- `Stop(Immediate)` 与 `ResetForPool()` 必须清空粒子、隐藏 Graphic/Renderer，并清除外部 Renderer 的 `_FlashAmount`。
- 预览驱动器 `PlannedContentPreviewDriver` 只允许存在于 Preview Scene，项目规则禁止其进入生产 Prefab。

## Composite Runtime Entry

W13/W18 的组合成品使用 `CompositeVfxController`，仍实现同一个 `IVfxRuntimeEntry`。正式 Prefab 不嵌入子特效层级，只序列化子 Runtime Entry Prefab 引用，并在首次播放时建立可复用实例池。

| API / 状态 | 约定 |
|---|---|
| `Play()` | 从 t=0 复播；复用既有子实例，不持续增长 |
| `ReleaseGate(string)` | 只放行当前等待的精确外部事件 |
| `SendEvent("gate:<id>")` | `ReleaseGate` 的事件形式；错误 id 返回 false |
| `PlaybackRate` | 钳制到 0.1–4；仅改变组合时间轴速度 |
| `CameraHintSerial / LastCameraHintType / LastCameraHintStrength` | 只读提示通道；接入方决定是否消费，不由 Runtime Entry 操纵相机 |
| `ResetForPool()` | 停止组合并隐藏所有子实例，保留池供下次复用 |
| `ReleaseInstances()` | 销毁运行池；用于明确卸载，不在每次复播调用 |

时间轴 `stop` 使用 Immediate 并隐藏对应实例，使实际存活集合与 Manifest 的组合峰值预算一致。`CompositePreviewDriver` 只存在于 Preview Scene，负责轮播、自动放行 gate 与演示相机提示，生产规则禁止它进入 Runtime Entry。
