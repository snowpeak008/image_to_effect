# Recipe v2 Schema 草案（Recipe v2 Schema Draft）

状态：`DRAFT`（T2a 产出，2026-09-05，待主 agent 验收；T2b 修订编译器边界后转正式）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）
配套：`PROTOTYPE_CATALOG_v1.md`、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`；JSON Schema 草稿见同目录 `recipe-v2.schema.draft.json`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本草案覆盖 |
|---|---|
| **原型轴** | `archetype` 字段取值 = 原型目录 58 个 id + `orchestration`；层结构通过 `layers[].role` 引用原型层角色词表；节拍通过 `layers[].timing.phase` 引用原型标准阶段 |
| **元素轴** | `element.id` 取值 = 元素目录 13 个 + `none`；支持 `layers[].elementOverride` 按层多元素 |
| **风格轴** | `style.id` 取值 = 首批 `cartoon / pixel` + `none`（写实基线）+ 候选预留；`style.parameters` 为风格子图参数 |
| **维度轴** | `dimension` 2d / 3d；2D 专有的 `layers[].sorting` 字段 |
| **档位** | `performanceTier` 六档枚举；同一 recipe 按档位编译出不同预制体；`layers[].tierOverrides` 允许覆写目录默认降级 |

**与 v1 的关系：不兼容、不迁移**（§8）。本草案的示例节是全文档集里唯一允许出现具体特效组合的地方（ADR-010 §10-1）。

---

## 1. 设计原则

1. **Recipe 只描述"选择"，不描述"实现"**：原型给结构、元素给倾向、风格给画法、档位给预算，Recipe 在这四个目录上做选择并给出少量显式覆写。一个 recipe 的字节数应远小于它编译出的预制体的复杂度。
2. **一切引用目录**：`archetype / role / technique.variant / element.id / style.id` 全部是对目录资产的引用，编译器 fail-closed 校验存在性与兼容性（如某 role 在该 archetype 下是否存在、某 technique.family 是否在该层的可选族内）。
3. **层是唯一的编译单元**：每层 → 预制体一个子节点 + 一个渲染 / 粒子 / 光组件 + 材质；没有"模块 / 模板"中间层。
4. **接口显式声明**：对外暴露的事件与参数在 `interface` 中列出并给出到层参数的绑定；未声明的不暴露。
5. **档位是编译输入不是运行时开关**：同一 recipe × 6 档 = 最多 6 个预制体；预制体内不含档位切换逻辑（自包含、零假设）。
6. **确定性**：`seed` + 相同目录版本 + 相同档位 → 字节相同的预制体（编译器保证；用于门禁与快照）。

---

## 2. 顶层结构

```jsonc
{
  "recipeVersion": 2,
  "id": "string, 全局唯一，^[a-z0-9_]+$",
  "name": "string, 可选显示名",
  "dimension": "2d | 3d",
  "archetype": "原型 id（PROTOTYPE_CATALOG_v1 §3）或 orchestration",
  "element": {
    "id": "元素 id（ELEMENT_CATALOG_v1 §3）或 none",
    "intensity": 0.0 ~ 1.0,                 // 元素味道浓度（元素目录 §1.2），默认 0.7
    "physicalProfile": "dust|rubble|cloth|liquid|spark_metal", // 仅 id=none 时可选，覆盖默认子画像选择
    "paletteOverride": { "primary": [r,g,b], "secondary": [...], "hot": [...], "cool": [...], "residue": [...] } // 可选，线性 HDR
  },
  "style": {
    "id": "cartoon | pixel | none | （候选预留）",
    "parameters": { /* 风格子图参数，见 STYLE_CATALOG_v1 §2.2 / §3.2 */ }
  },
  "performanceTier": "mobile_low | mobile_medium | mobile_high | pc_low | pc_medium | pc_high",
  "seed": 0 ~ 4294967295,
  "timing": { /* 可选：原型节拍覆写，见 §4 */ },
  "layers": [ /* 见 §3；archetype != orchestration 时必需且 ≥1 */ ],
  "children": [ /* 仅 orchestration，见 §6 */ ],
  "timeline": [ /* 仅 orchestration */ ],
  "wiring": [ /* 仅 orchestration */ ],
  "interface": { /* 见 §5 */ },
  "metadata": {
    "createdBy": "string",
    "catalogVersions": { "prototype": "1.0.0", "element": "1.0.0", "style": "1.0.0" },
    "notes": "string, 可选"
  }
}
```

**字段纪律**：`additionalProperties: false` 全树生效（与 v1 解析器的 E100 未知字段策略一致，fail-closed）。

---

## 3. 层（layer）

```jsonc
{
  "layerId": "string, recipe 内唯一，^[a-z][a-zA-Z0-9_]*$",
  "role": "层角色（PROTOTYPE_CATALOG_v1 §2.1 词表，可带 .后缀），必须是该 archetype 声明的层之一",
  "technique": {
    "family": "material | gpu_particles | cpu_particles | mesh | local_light",
    "variant": "string, 该族下的变体 id（T2b 定义变体库；如材质子图名、VFX Graph 模板名、网格生成器名）"
  },
  "parameters": { /* 按 technique.variant 的参数面，键值由变体清单校验 */ },
  "timing": {
    "phase": "launch | travel | sustain | impact | end",   // 相对原型标准阶段
    "offset": 0.0,        // 秒，相对该阶段起点（可负：提前）
    "duration": null,     // 秒；null = 跟随阶段长度；数值 = 固定时长
    "loop": false         // 仅 sustain 阶段可 true
  },
  "enabled": true,
  "elementOverride": "元素 id，可选（按层多元素，元素目录 §2.4）",
  "sorting": { "layer": "string", "order": 0 },        // 仅 dimension=2d；缺省由编译器按原型层顺序自动分配
  "tierOverrides": {                                    // 可选：覆写目录默认降级表
    "mobile_low": { "enabled": false },
    "pc_high":    { "parameters": { "noiseLayers": 3 } }
  }
}
```

**约束**：
- 原型声明为**必需**的层角色必须在 `layers[]` 中出现且 `enabled: true`（否则 E2xx 错误：原型不成立）。
- 同一 `role`（含后缀）在一个 recipe 里只能出现一次。
- `technique.family` 必须在该原型该层的"可选技术族"集合内；`variant` 必须存在于该族变体库且声明兼容该 `dimension`。
- 若 `technique.family` 在当前 `performanceTier` 下被目录降级表替换（如 gpu_particles→cpu_particles），编译器按降级表选替代族与其默认变体，并在报告中登记；recipe 可用 `tierOverrides` 指定替代变体。
- `local_light` 族的层：3D 编译为 URP Light（Point / Spot），2D 编译为 Light2D；`parameters` 面统一为 `{ color, intensity, range, flickerMode, flickerRate, flickerDepth, decayShape, castShadows }`（元素预设给默认，风格约束再量化）。

**参数合并顺序**（与元素 / 风格目录一致）：原型层默认 ← 元素预设 (role, family) ← 元素预设 (role.suffix, family) ← 风格约束参数集 ← `layers[].parameters` ← `tierOverrides[tier].parameters` ← 档位预算截断。

---

## 4. 节拍（timing）与事件映射

原型目录为每个原型定义了标准阶段与量级；recipe 顶层 `timing` 可覆写阶段时长与循环：

```jsonc
"timing": {
  "phases": {
    "launch":  { "duration": 0.15 },
    "travel":  { "duration": null },     // null = 外部驱动（如 setTravelPose）或直到 impact 入事件
    "sustain": { "duration": null, "loop": true },
    "impact":  { "duration": 0.1 },
    "end":     { "duration": 0.6 }
  },
  "autoAdvance": true    // true：阶段结束自动进入下一阶段；false：每个阶段等待入事件
}
```

编译器校验：覆写值必须在原型目录给出的量级范围内（超出 → 警告，不阻塞；这是设计自由度）。

**入 / 出事件**由原型定义（标准 `launch / travel|sustain / impact / end` + 原型特有如 `hitAt / break / setProgress`），recipe 不重定义事件语义，只在 `interface.events` 中决定暴露哪些。

---

## 5. 对外接口（interface）

```jsonc
"interface": {
  "events": {
    "expose": ["launch", "impact", "end", "hitAt", "break"],       // 入事件白名单（缺省 = 原型全部标准入事件）
    "emit":   ["onImpact", "onBreak", "onComplete"]                // 出事件白名单（缺省 = 原型全部标准出事件）
  },
  "parameters": [
    {
      "id": "integrity",                    // 对外参数名（原型特有参数或自定义）
      "type": "float | int | bool | enum | color | gradient | vector2 | vector3 | transformRef | rendererRef",
      "range": [0, 1],                      // 数值类必填
      "default": 1.0,
      "enumValues": ["a", "b"],             // enum 必填
      "bindings": [                         // 参数如何落到层
        { "layerId": "shell", "path": "material.crackProgress", "map": "invert" },
        { "layerId": "cracks", "path": "material.threshold", "map": "invert" }
      ]
    }
  ]
}
```

**标准参数**（`palette / intensity / scale / speed / seed`）默认暴露且自动绑定到全部层，无需声明。`bindings[].map` 取 `identity | invert | remap(a,b,c,d) | curve(<预置曲线 id>)`。`transformRef / rendererRef` 类型参数用于原型需要的外部引用（如 `targetRenderer`、`weaponTip`），预制体在引用为空时按原型目录的退化规则工作（几何壳 / 包围盒）。

### 5.1 编译到预制体的映射

| Recipe 元素 | 预制体 |
|---|---|
| 根 | `GameObject <id>__<tier>` + 运行时控制组件（v2 组件，命名由 T2b 定；现有 `GeneratedVfxController` 的三阶段 Launch/Travel/Impact 根结构是雏形，但 v2 需要：任意阶段集合、`sustain` 循环、原型特有入事件分派、出事件 C# event + UnityEvent 两种、参数块） |
| 每个 `layer` | 子节点 `Layers/<layerId>`，挂对应族组件：`material`→`MeshRenderer`(quad / 生成网格) 或 `SpriteRenderer`（2D quad 类）+ 材质实例；`gpu_particles`→`VisualEffect`；`cpu_particles`→`ParticleSystem`；`mesh`→`MeshFilter+MeshRenderer`(+ `Rigidbody/Cloth/TrailRenderer/LineRenderer` 按变体)；`local_light`→`Light` 或 `Light2D` |
| `timing` | 控制组件上的阶段表：每阶段 → 该阶段激活的层列表 + 各层 offset / duration |
| `interface.parameters` | 参数块组件（序列化字段 + Inspector 可调）+ 绑定表（运行时 `MaterialPropertyBlock` / `VisualEffect.SetFloat` / `ParticleSystem` 模块写入 / `Light.intensity` 等） |
| `interface.events` | `SendEvent(string id, payload)` 分派表 + `event Action<...>` 出事件 |
| 2D `sorting` | 每层 `SortingGroup` / `sortingLayerName + sortingOrder` |
| `performanceTier` | 预制体名后缀 + 编译报告中的预算占用表 |

---

## 6. 编排（orchestration）

`archetype: "orchestration"` 时 `layers[]` 必须为空，改用：

```jsonc
"children": [
  { "childId": "charge", "recipeRef": "<另一 recipe id>", "anchor": "root", "priority": 2,
    "overrides": { "element": { "id": "arcane" }, "parameters": { "chargeDuration": 1.2 } } },
  { "childId": "bolt",   "recipeRef": "<recipe id>", "anchor": "root", "priority": 1 },
  { "childId": "burst",  "recipeRef": "<recipe id>", "anchor": "follow:bolt", "priority": 1 }
],
"timeline": [
  { "t": 0.0, "target": "charge", "event": "launch" },
  { "t": 1.2, "target": "bolt", "event": "launch", "waitFor": "charge.onRelease" }
],
"wiring": [
  { "from": "bolt.onImpact", "to": "burst.impact", "payloadMap": { "position": "position", "normal": "normal" } },
  { "from": "burst.onComplete", "to": "__self.end" }
]
```

规则见 `PROTOTYPE_CATALOG_v1.md` §10.2：≤ 8 子实例、同 dimension / style / tier、不可嵌套、`anchor ∈ {root, follow:<childId>, offset:[x,y,z]}`、总预算超限按 `priority` 低→高禁用可选子实例。`recipeRef` 引用的子 recipe 的 `dimension / style / performanceTier` 必须与编排一致（编译器校验），`element` 可被 `overrides` 替换。`interface` 在编排层重新声明（聚合），子实例接口不自动透出。

**内嵌 vs 编排**（原型目录 §10.4）：单原型 recipe 也允许包含 `impact` 阶段的层（如投射物 recipe 自带 `flash / shock / emission.debris`），只要这些 role 在该原型的层表中；推荐用编排以获得可替换性。

---

## 7. 六档如何影响编译

1. **输入**：`performanceTier` 是编译命令的输入之一（recipe 内的值是默认，CLI / MCP 可覆写以批量出六档）。
2. **降级表应用**：对每层，取原型目录该层在该档的策略（`关 / C:n / G:n / 材n / 光:烘 / 块:n …`），转换为：层 `enabled`、技术族替换、参数上限截断、变体替换（如 `材:静` → 静态变体）。`tierOverrides` 优先。
3. **预算校验**（fail-closed）：按原型目录 §2.4 六档基准预算累计 GPU 粒子 / CPU 粒子 / 材质噪声层 / 局部光数 / 破碎块 / 透明叠加层 / 网格顶点；任一项超限 → 编译错误 E3xx 并给出超限项与建议（禁用哪些可选层可通过）。
4. **风格与档位交互**：风格约束（描边壳、抖动级）按 `STYLE_CATALOG_v1.md` §5 与档位取小。
5. **产物**：`<id>__<tier>.prefab` + 编译报告（每层最终族 / 变体 / 参数、预算占用、降级登记、警告）。同一 recipe 六档产物的**接口完全一致**（参数块与事件表相同，即使某层被关闭——关闭层的绑定为空操作），保证用户切换档位无需改代码。
6. **门禁继承**：ADR-009 的"按族的机器可查视觉谓词 + fail-closed + 显式豁免"方法论在 v2 由**按技术族的构造性谓词**继承（如：`material` 层必须有 ≥1 个时间驱动输入除非变体标记 `static`；`local_light` 层在非 `光:烘` 档必须存在 Light 组件且 intensity 曲线非常量；`cpu_particles` 层峰值 > 0）。谓词清单由 T2b 出。

---

## 8. 与 v1 的关系：不兼容、不迁移

| 维度 | v1 | v2 | 为何不迁移 |
|---|---|---|---|
| 内容单元 | `stages[].modules[]`，module = `kind`（`energy_body / sprite_emitter / …`8 种）+ `templateId`（6 sprite 模板） | `layers[]`，layer = `role`（21 角色词表）+ `technique`（5 族 × 变体） | v1 的 kind 与模板全部是 billboard sprite 范式产物，ADR-010 §9 定性为全部清理；没有对应关系可映射 |
| 原型 | 20 个封闭枚举（`projectile / impact / … / loot`），无层结构定义 | 58 个目录原型 + `orchestration`，每个有层结构 / 节拍 / 接口 / 降级 | v1 archetype 只是标签，不约束结构；v2 archetype 决定必需层与事件集 |
| 元素 | `content.family`（15 个混杂：含 `environment / hit_feedback / screen_ui / game_ui` 这些不是元素） | `element.id`（13 元素 + none + 物理子画像） | v1 把原型大类混进元素轴，违反三轴正交 |
| 风格 | `style.token` 14 个 + 平铺 30 余参数，含 `atlas_id / atlas_fps / snap_fps / virtual_res / loop_mode`（序列帧与低分辨率 RT 语义） | `style.id` + 子图参数；**禁止**序列帧 / RT 语义 | 违反 ADR-010 §2 / §5 裁定 |
| 行为 | `behavior.{motion,hit,emission,timing}` 40 余参数 | 归入原型特有参数（`hitRule / emissionPattern / …`）与 `timing.phases` | v1 行为块是脱离层结构的平行参数面，v2 全部落到原型接口 |
| 资产外 | `camera_hints`（震屏 / 缩放 / 慢动作） | **删除**；用户订阅出事件自行处理 | 违反 ADR-010 §4 |
| 档位 | `targetProfile` 2 档（`mobile_medium / pc_editor`） | 6 档 | 无法映射 |
| 编排 | `composite` 原型 + `timeline / gates` | `orchestration` + `children / timeline / wiring` | 结构相近但引用对象（v1 引用 Runtime Entry 产物，v2 引用 recipe）不同 |
| 元数据 | `templateCatalogVersion` | `catalogVersions{prototype, element, style}` | 模板目录退役 |

**结论**：v1 recipe 的每一个字段要么被删（资产外 / 序列帧语义）、要么语义被重定义（archetype / style / content）、要么引用的资产被清理（templateId / kind）。写迁移器只会把"清理对象"以新形状保留下来，违背 ADR-010 §9"不归档为 legacy"。v1 解析器（`VfxDomainParser`）、`recipe-v1.schema.json`、全部 v1 recipe 资产与 T2c 旧资产清理同批退役；v2 解析器为新实现（可复用 v1 的错误码风格 E100~E105 与 fail-closed 未知字段策略，这是治理外壳，ADR-010 明示不在推翻范围）。

---

## 9. 示例 recipe（本文档集唯一允许出现具体特效组合的位置）

### 9.1 示例一：3D · 护盾原型 × 冰元素 × 卡通风格 · PC 中档

结构来源：`PROTOTYPE_CATALOG_v1.md` B04 `shield`。层：`mesh_shell`（必需）/ `edge` / `flash.hit`（必需）/ `mesh_shell.crack` / `debris` / `flash.break` / `light`（必需）。

```json
{
  "recipeVersion": 2,
  "id": "shield_ice_cartoon_3d",
  "name": "Ice Shield (Cartoon, 3D)",
  "dimension": "3d",
  "archetype": "shield",
  "element": { "id": "ice", "intensity": 0.8 },
  "style": {
    "id": "cartoon",
    "parameters": { "shadingSteps": 3, "edgeSharpness": 0.9, "outlineWidth": 0.02, "saturationMul": 1.25, "detailMul": 0.4 }
  },
  "performanceTier": "pc_medium",
  "seed": 20260905,
  "timing": {
    "phases": {
      "launch": { "duration": 0.3 },
      "sustain": { "duration": null, "loop": true },
      "impact": { "duration": 0.4 },
      "end": { "duration": 0.35 }
    },
    "autoAdvance": true
  },
  "layers": [
    {
      "layerId": "shell",
      "role": "mesh_shell",
      "technique": { "family": "mesh", "variant": "shell_polyhedron_faceted" },
      "parameters": { "shellShape": "sphere", "shellRadius": 1.2, "facetCount": 96, "fresnelPower": 3.0, "innerOpacity": 0.18 },
      "timing": { "phase": "launch", "offset": 0.0, "duration": null, "loop": false },
      "enabled": true
    },
    {
      "layerId": "contactRing",
      "role": "edge",
      "technique": { "family": "material", "variant": "ring_ground_contact" },
      "parameters": { "ringWidth": 0.08, "pulseRate": 0.5 },
      "timing": { "phase": "launch", "offset": 0.1, "duration": null, "loop": false },
      "enabled": true
    },
    {
      "layerId": "hitRipple",
      "role": "flash.hit",
      "technique": { "family": "material", "variant": "shell_ripple_multi" },
      "parameters": { "maxSimultaneousHits": 4, "rippleDuration": 0.35, "rippleWidth": 0.12 },
      "timing": { "phase": "impact", "offset": 0.0, "duration": 0.4, "loop": false },
      "enabled": true
    },
    {
      "layerId": "cracks",
      "role": "mesh_shell.crack",
      "technique": { "family": "material", "variant": "crack_voronoi_progress" },
      "parameters": { "crackDensity": 6, "crackWidth": 0.015 },
      "timing": { "phase": "sustain", "offset": 0.0, "duration": null, "loop": true },
      "enabled": true
    },
    {
      "layerId": "shards",
      "role": "debris",
      "technique": { "family": "mesh", "variant": "prefractured_rigidbody" },
      "parameters": { "fragmentCount": 48, "breakForce": 6.0, "fadeMode": "dissolve", "settleTime": 1.2 },
      "timing": { "phase": "end", "offset": 0.0, "duration": 1.5, "loop": false },
      "enabled": true,
      "tierOverrides": {
        "mobile_low": { "parameters": { "fragmentCount": 6 } },
        "mobile_medium": { "parameters": { "fragmentCount": 12 } }
      }
    },
    {
      "layerId": "breakFlash",
      "role": "flash.break",
      "technique": { "family": "material", "variant": "flash_radial_sdf" },
      "parameters": { "peakScale": 2.4, "holdFrames": 2 },
      "timing": { "phase": "end", "offset": 0.0, "duration": 0.15, "loop": false },
      "enabled": true
    },
    {
      "layerId": "innerLight",
      "role": "light",
      "technique": { "family": "local_light", "variant": "point_beat" },
      "parameters": { "range": 3.0, "castShadows": false },
      "timing": { "phase": "launch", "offset": 0.0, "duration": null, "loop": false },
      "enabled": true
    }
  ],
  "interface": {
    "events": {
      "expose": ["launch", "hitAt", "setIntegrity", "break", "end"],
      "emit": ["onHitRipple", "onBreak", "onComplete"]
    },
    "parameters": [
      {
        "id": "integrity", "type": "float", "range": [0, 1], "default": 1.0,
        "bindings": [
          { "layerId": "cracks", "path": "material.crackProgress", "map": "invert" },
          { "layerId": "shell", "path": "material.innerOpacity", "map": "remap(0,1,0.05,0.18)" }
        ]
      },
      {
        "id": "shellRadius", "type": "float", "range": [0.5, 3.0], "default": 1.2,
        "bindings": [
          { "layerId": "shell", "path": "mesh.shellRadius", "map": "identity" },
          { "layerId": "contactRing", "path": "material.ringRadius", "map": "identity" },
          { "layerId": "innerLight", "path": "light.range", "map": "remap(0.5,3.0,1.5,6.0)" }
        ]
      }
    ]
  },
  "metadata": {
    "createdBy": "t2a-design",
    "catalogVersions": { "prototype": "1.0.0", "element": "1.0.0", "style": "1.0.0" },
    "notes": "冰元素注入：shell 取 Voronoi 晶格 + 菲涅尔硬高光；debris 取块状直落；light 取 10000K steady。卡通叠加：3 级色阶、法线外扩描边壳（pc_medium 启用）。"
  }
}
```

**编译期发生什么（pc_medium）**：`shell` 取 `几:高 · 材3`（目录 B04 降级表 PM 列）→ 冰元素 material 预设 `mesh_shell` = Voronoi 晶格 + 菲涅尔 → 卡通子图 3 级量化 + 描边壳（PM 启用）；`shards` 块:64 上限，recipe 48 通过；`innerLight` 光:1 → 冰元素 10000 K、`steady`，卡通光约束 3 档强度阶跃；预算校验：材质噪声层 3 ≤ 3、局部光 1 ≤ 3、破碎块 48 ≤ 96、透明叠加 4 ≤ 6 → 通过。

### 9.2 示例二：2D · 连锁原型 × 雷元素 × 像素风格 · 手机中档

结构来源：`PROTOTYPE_CATALOG_v1.md` A10 `chain_link`。层：`link.segment`（必需）/ `flash.node`（必需）/ `emission.node` / `edge` / `light.node`（必需，MM 档 1 盏）。

```json
{
  "recipeVersion": 2,
  "id": "chain_lightning_pixel_2d",
  "name": "Chain Arc (Pixel, 2D)",
  "dimension": "2d",
  "archetype": "chain_link",
  "element": { "id": "lightning", "intensity": 1.0 },
  "style": {
    "id": "pixel",
    "parameters": { "pixelSize": 0.0625, "colorSteps": 4, "alphaCutoff": 0.5, "ditherLevels": 2, "frameRate": 12, "outline1px": true }
  },
  "performanceTier": "mobile_medium",
  "seed": 77,
  "timing": {
    "phases": {
      "launch": { "duration": 0.05 },
      "travel": { "duration": null },
      "end": { "duration": 0.3 }
    },
    "autoAdvance": false
  },
  "layers": [
    {
      "layerId": "arc",
      "role": "link.segment",
      "technique": { "family": "mesh", "variant": "polyline_jagged_band" },
      "parameters": { "segmentVertices": 8, "jitter": 0.6, "sag": 0.0, "bandWidth": 0.12, "segmentLifetime": 0.25, "rephaseRate": 14 },
      "timing": { "phase": "travel", "offset": 0.0, "duration": null, "loop": false },
      "enabled": true,
      "sorting": { "layer": "VFX", "order": 20 }
    },
    {
      "layerId": "nodeFlash",
      "role": "flash.node",
      "technique": { "family": "material", "variant": "flash_star_sdf" },
      "parameters": { "points": 4, "peakScale": 0.6, "holdFrames": 1 },
      "timing": { "phase": "travel", "offset": 0.0, "duration": 0.12, "loop": false },
      "enabled": true,
      "sorting": { "layer": "VFX", "order": 30 }
    },
    {
      "layerId": "nodeSparks",
      "role": "emission.node",
      "technique": { "family": "cpu_particles", "variant": "burst_short_streaks" },
      "parameters": { "countPerNode": 10, "lifetime": 0.12, "speed": 3.5 },
      "timing": { "phase": "travel", "offset": 0.0, "duration": 0.2, "loop": false },
      "enabled": true,
      "sorting": { "layer": "VFX", "order": 25 },
      "tierOverrides": {
        "mobile_low": { "enabled": false },
        "pc_medium": { "technique": { "family": "gpu_particles", "variant": "burst_short_streaks_gpu" }, "parameters": { "countPerNode": 500 } }
      }
    },
    {
      "layerId": "arcGlow",
      "role": "edge",
      "technique": { "family": "material", "variant": "band_soft_halo" },
      "parameters": { "haloWidth": 0.3, "haloOpacity": 0.35 },
      "timing": { "phase": "travel", "offset": 0.0, "duration": null, "loop": false },
      "enabled": false,
      "sorting": { "layer": "VFX", "order": 10 },
      "tierOverrides": { "mobile_high": { "enabled": true }, "pc_low": { "enabled": true }, "pc_medium": { "enabled": true }, "pc_high": { "enabled": true } }
    },
    {
      "layerId": "nodeLight",
      "role": "light.node",
      "technique": { "family": "local_light", "variant": "light2d_point_beat" },
      "parameters": { "range": 1.5, "followLatestNode": true },
      "timing": { "phase": "travel", "offset": 0.0, "duration": null, "loop": false },
      "enabled": true
    }
  ],
  "interface": {
    "events": {
      "expose": ["launch", "setNodes", "addNode", "end"],
      "emit": ["onHop", "onComplete"]
    },
    "parameters": [
      {
        "id": "hopCount", "type": "int", "range": [1, 12], "default": 5,
        "bindings": [ { "layerId": "arc", "path": "mesh.maxSegments", "map": "identity" } ]
      },
      {
        "id": "hopInterval", "type": "float", "range": [0.03, 0.3], "default": 0.08,
        "bindings": [ { "layerId": "arc", "path": "controller.hopInterval", "map": "identity" }, { "layerId": "nodeFlash", "path": "controller.retriggerInterval", "map": "identity" } ]
      },
      {
        "id": "damping", "type": "float", "range": [0, 1], "default": 0.15,
        "bindings": [ { "layerId": "arc", "path": "material.intensityPerHopDecay", "map": "identity" }, { "layerId": "nodeLight", "path": "light.intensityPerHopDecay", "map": "identity" } ]
      },
      {
        "id": "topology", "type": "enum", "enumValues": ["chain", "fan", "star"], "default": "chain",
        "bindings": [ { "layerId": "arc", "path": "controller.topology", "map": "identity" } ]
      }
    ]
  },
  "metadata": {
    "createdBy": "t2a-design",
    "catalogVersions": { "prototype": "1.0.0", "element": "1.0.0", "style": "1.0.0" },
    "notes": "雷元素注入：link.segment 取折线跳变（rephaseRate 14 Hz）+ 双阈值硬核宽晕；light 取 strobe 15~30 Hz。像素叠加：世界 XY 栅格 pixelSize 0.0625（正交相机下 1/16 单位一格），12 fps 时间量化，4 色阶，2 级 Bayer 抖动，1px 描边；粒子替换为方块短条并吸附栅格。MM 档 light.node 只留最新节点 1 盏。"
  }
}
```

**编译期发生什么（mobile_medium）**：`arc` 取 `8 顶点 · 材1`（A10 降级表 MM）；雷元素 mesh 预设 `link` = `jagged_polyline`，材质 = 一维折线噪声 + 跳变；像素子图 UV→世界 XY 栅格量化 + 时间量化 12 fps；`nodeSparks` CPU 粒子 10/节点 × 5 节点峰值 50 ≤ 150；`arcGlow` MM 关（`enabled:false` 且 MM 无 override）；`nodeLight` 光:1（最新节点），雷元素 strobe → 像素光约束 4 档 + 12 fps 量化；预算：CPU 粒子 50 ≤ 150、材质层 1 ≤ 2、光 1 ≤ 1、透明叠加 3 ≤ 3 → 通过。

### 9.3 编排片段（结构示意，非完整 recipe）

```json
{
  "recipeVersion": 2,
  "id": "gather_release_arcane_3d",
  "dimension": "3d",
  "archetype": "orchestration",
  "element": { "id": "arcane", "intensity": 0.7 },
  "style": { "id": "cartoon", "parameters": {} },
  "performanceTier": "pc_high",
  "seed": 3,
  "layers": [],
  "children": [
    { "childId": "gather", "recipeRef": "charge_gather_arcane_cartoon_3d", "anchor": "root", "priority": 2 },
    { "childId": "orbs",   "recipeRef": "orbitals_arcane_cartoon_3d", "anchor": "root", "priority": 3, "overrides": { "parameters": { "count": 3 } } },
    { "childId": "shot",   "recipeRef": "projectile_homing_arcane_cartoon_3d", "anchor": "root", "priority": 1 },
    { "childId": "hit",    "recipeRef": "impact_burst_arcane_cartoon_3d", "anchor": "follow:shot", "priority": 1 }
  ],
  "timeline": [
    { "t": 0.0, "target": "gather", "event": "launch" },
    { "t": 0.0, "target": "orbs", "event": "launch" },
    { "t": 1.0, "target": "gather", "event": "release" },
    { "t": 1.0, "target": "orbs", "event": "consume", "payload": { "index": 0 } }
  ],
  "wiring": [
    { "from": "gather.onRelease", "to": "shot.launch" },
    { "from": "shot.onImpact", "to": "hit.impact", "payloadMap": { "position": "position", "normal": "normal" } },
    { "from": "hit.onComplete", "to": "__self.end" }
  ],
  "interface": {
    "events": { "expose": ["launch", "setTarget", "end"], "emit": ["onImpact", "onComplete"] },
    "parameters": [
      { "id": "chargeDuration", "type": "float", "range": [0.3, 3.0], "default": 1.0, "bindings": [ { "layerId": "gather", "path": "child.chargeDuration", "map": "identity" } ] }
    ]
  },
  "metadata": { "createdBy": "t2a-design", "catalogVersions": { "prototype": "1.0.0", "element": "1.0.0", "style": "1.0.0" } }
}
```

（编排中 `bindings[].layerId` 指向 `childId`，`path` 前缀 `child.` 表示转发到子实例的对外参数。）

---

## 10. 错误码草案（沿用 v1 风格，fail-closed）

| 码 | 含义 |
|---|---|
| E100~E105 | 沿用：未知字段 / 缺必填 / 类型错 / 枚举外 / JSON 无效 / 非有限数 |
| E200 | `archetype` 不在原型目录 |
| E201 | 必需层角色缺失或 `enabled:false` |
| E202 | `role` 不属于该原型 / 重复 |
| E203 | `technique.family` 不在该层可选族 / `variant` 不存在 / 变体不支持该 `dimension` |
| E204 | `element.id` 不在元素目录 / `elementOverride` 无效 / `physicalProfile` 在非 none 元素下出现 |
| E205 | `style.id` 不在风格目录 / 风格参数越界 |
| E206 | `timing.phase` 不是该原型的阶段 / `loop` 用于非 sustain |
| E207 | `interface.parameters[].bindings` 指向不存在的层或路径 / 类型不匹配 |
| E208 | `interface.events` 暴露了原型不存在的事件 |
| E210 | 编排：`layers` 非空 / 子实例 > 8 / 嵌套编排 / 子 recipe 维度・风格・档位不一致 / `wiring` 端点无效 / `anchor` 引用无效 |
| E300 | 档位预算超限（附超限项与可禁用建议） |
| E301 | 降级表无法找到替代族（原型目录缺项，属目录错误） |
| W400 | `timing` 覆写超出原型量级范围（警告） |
| W401 | 风格建议密度倍率与 recipe 显式参数冲突（警告，取 recipe） |

---

## 11. 未决问题（转 `T2A_REPORT.md` §6 汇总）

1. `technique.variant` 命名规范与变体库清单由 T2b 定；本草案示例中的变体名（`shell_polyhedron_faceted` 等）为占位语义名。
2. `interface.parameters[].bindings[].path` 的路径语法（`material.x / mesh.x / light.x / controller.x / child.x`）需 T2b 与运行时组件同定。
3. 编排 `recipeRef` 是引用 recipe id（编译时内联）还是引用已编译预制体（链接）——建议前者（保证六档一致与确定性），待拍板。
4. UGUI Overlay Canvas 下粒子层的可行性（F 类）影响 `dimension` 是否需要第三个值 `ui`，或 F 类在 `2d` 下加 `canvasMode` 字段——待 T2b 验证后决定。
