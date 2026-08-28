# S2 AI 环节小样纪要

> 状态：⬜ 进行中 / ☑ 已完成（修订版，待主 Agent 重新验收）
> 执行日期：2026-08-22 ~ 2026-08-22
> 使用的 AI 工具与模型：4 个彼此隔离的 `gpt-5.6-terra` / `high` 子 Agent；每个仅收到本回合必要的契约与输入，未获知预期答案，也不得读取工作区或调用工具。
> 前置：S1 纪要判定“通过”。本文档是 S2 的唯一保留产出；实验 JSON 与说明文档作为 S4 输入草稿保留在本目录。

## 1. 实验材料

- Recipe JSON 样例文件：`spike-notes/sample-recipe.json`。它保留 S1 的 `T_Core`、`T_Embers`、`T_Burst` 参数覆盖，但核心枚举已对齐 `DEVELOPMENT_PLAN.md` §5：`recipeVersion: 1`、`dimension: "2d"`、`archetype: "projectile"`、`on_launch`/`on_hit`、`energy_body`/`secondary_particles`/`impact_burst`。
- 给 AI 的说明文档：`spike-notes/ai-readme-draft.md`。Patch 协议对齐 `DEVELOPMENT_PLAN.md` §15：使用稳定 Stage/Module ID 路径和裸操作数组，不使用数组下标或 `{ "operations": ... }` 包装。
- 完整原始提示、输出、错误反馈和逐字段 Validator 记录：`spike-notes/s2-evidence/README.md` 与 `R1.md`–`R4.md`。

## 2. 回合记录

“修正次数”指 AI 首次输出后额外的修正往返次数；首次输出已经合法记 0。R3 的含错 Recipe 和结构化 Validator 报告是输入，首次 AI 输出即为修复结果。

| 回合 | 任务 | 修正次数 | 结果（合法/失败） | AI 的错误表现（臆造字段/越界/改动无关内容等） |
|---|---|---:|---|---|
| R1 | 生成标准火球 Recipe | 0 | 合法 | 无臆造字段、越界或格式偏离；所有正式枚举、触发器和语义模块值正确。 |
| R2 | 更大、更慢、火星更多的变体 | 0 | 合法 | 无臆造字段、越界或格式偏离；`scale 1.8 > 1.2`、`travel.duration 1.6 > 1.0`、`rate 36 > 18`，语义映射可复核。 |
| R3 | 给含错 Recipe + 手写错误报告，要求修复 | 0 | 合法 | 初始输入故意含 3 项错误；输出只修改三条 Validator 报告的稳定语义路径，未改无关字段。 |
| R4 | “火星减少一半” → 输出局部 Patch | 0 | 合法 | 输出裸操作数组，精确替换稳定路径的 `rate: 18` 为 `9`；没有完整 Recipe、对象包装或多余操作。 |

## 3. 结论（S4 的直接输入）

### 3.1 字段命名修正清单

| 原字段/设计 | 问题（AI 如何误解） | 修正决定 |
|---|---|---|
| `recipeVersion: "0.1"` | 与正式领域模型中整数版本冲突。 | 使用整数 `recipeVersion: 1`。 |
| `dimension: "2D"`、`archetype: "fireball"` | 与正式枚举 `2d`、`projectile` 冲突。 | 采用正式小写枚举；火球作为 `projectile` 的具体实例，而非另造 archetype。 |
| `onCast`、`onImpact` | 不在第一版 Trigger 枚举中。 | 使用 `on_launch`、`on_hit`。 |
| `sprite`、`particle` 等 Spike 内部种类 | 与正式 Recipe 的语义模块 `kind` 冲突。 | 使用 `energy_body`、`secondary_particles`、`impact_burst`；仍只绑定 S1 覆盖的三个 `T_*` 模板。 |
| JSON Pointer 数组下标路径 | 模块重排会使 `/stages/0/modules/1/...` 指向其他对象。 | Validator 错误和 Patch 都用 `/stages/{stageId}/modules/{moduleId}/...` 稳定语义路径。 |
| `{ "operations": [...] }` Patch 包装 | 与 §15 的裸操作数组示例冲突，增加无谓格式。 | Patch 顶层为 JSON operation array：`[{ "op": "replace", "path": "...", "value": ... }]`。 |
| “标准火球” | R1 说明自然语言“标准”不必然等于样例的每个默认数值。 | 若需金样精确数值，提示中明确“逐值使用 sample”；合法性仍由 Schema/Validator 判断。 |

### 3.2 错误报告格式定版

实测最有效的错误条目结构（S4 的验证报告按此实现）：

```json
{
  "code": "E102",
  "severity": "error",
  "path": "/stages/travel/modules/embers/parameters/rate",
  "message": "Embers emission rate must be within its inclusive numeric range.",
  "actualValue": 250,
  "allowedRange": "[0, 100]"
}
```

依据：

- R3 的 `code + severity + 稳定语义 path + 人话 message + actualValue + allowedRange` 同时给出机器定位和修复边界；模型一次修复 3 项，且未动任何未报告字段。
- `path` 必须是 `/stages/{stageId}/modules/{moduleId}/...`，不可使用数组索引。它与 Patch 机制共用同一稳定定位规则，重排模块不会改变指向。
- `actualValue` 和 `allowedRange` 都必须保留；整数约束要明确写为 `integer [min, max]`，避免只截断范围却保留小数。
- S4 对未知字段、类型、枚举和模板错误也应输出同一结构，绝不静默忽略。

### 3.3 Patch 格式定版

实测 AI 最不易出错的 Patch 形态（S8 按此实现）：

```json
[
  {
    "op": "replace",
    "path": "/stages/travel/modules/embers/parameters/rate",
    "value": 9
  }
]
```

此为受限语义 Patch，不是 RFC 6902 数组定位：`travel`、`embers` 是稳定 ID。顶层必须为操作数组；当前 S2 仅验证 `replace`，S4/S8 再按正式计划实现 `add`、`remove`、`enable`、`disable` 及 revision 校验。

## 4. 退出判定

- 成功率：4 回合中 ≤2 次修正内产出合法结果的回合数 = **4 / 4**（门槛 ≥ 3）
- ☑ 通过，满足 S2 退出条件；由主 Agent 独立验收后方可进入 S3
- ⬜ 未达标 → 修改 Schema 表达或说明文档后重测（重测记录追加在第 2 节表格下方），不得带病进入 S3

## 5. 主 Agent 独立验收

> 验收日期：2026-08-22  
> 判定：**通过，允许进入 S3**

首轮提交因使用数组下标 Patch、对象包装及非正式领域枚举而被退回，不能计入最终实验结果。整改版重新使用 4 个隔离的 `gpt-5.6-terra` / `high` 子 Agent 执行 R1–R4；主 Agent 对整改版完成以下独立复核：

- 机器解析 `sample-recipe.json` 和 R1–R4 输出，JSON 均合法。
- R1 使用正式 Recipe 版本、维度、原型、Trigger 和 Module kind。
- R2 的 `scale`、Travel `duration`、Embers `rate` 均相对基线增加，且保持范围合法。
- R3 规范化比较后，除错误报告指定的 3 条稳定语义路径外，其余字段和值完全不变。
- R4 为单项裸操作数组，路径为 `/stages/travel/modules/embers/parameters/rate`，值为 `9`，不含数组下标、完整 Recipe 或无关操作。

最终有效结果为 **4/4**，满足 `≥3/4` 且每回合 `≤2` 次额外修正的 S2 退出门槛。
