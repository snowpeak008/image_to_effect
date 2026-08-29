# 错误码契约（Recipe/Manifest v1）

每个报告条目固定包含 `code`、稳定 JSON/资产 `path`、人话 `message`，并在值或范围有意义时提供 `actualValue` 与 `allowedRange`。下表的路径是稳定路径族；具体 stage/module 使用 Recipe 的稳定 ID，不使用数组下标。`actual/allowed` 一栏的“适用”表示对应调用会填充这两个字段，缺少该字段本身不是错误。

本页受正常 EditMode 测试 `ErrorCodeAudit_EditorSourceAndDocumentationRemainBidirectionallyInSync` 保护：测试从所有实际 `Editor/**/*.cs` 报告发射器（包括正式 AI-workflow export 与保留的 Cohort audit `E720`）提取 `E###`/`I###`，要求与本表双向完全一致。`W401`–`W404` 不是独立字符串契约：预算器从相应 `E401`–`E404` 以固定 `Replace("E","W")` 生成，含义/路径/actual/allowed 与对应预算行相同。

| Code | Stable path | Human meaning | actual/allowed |
| --- | --- | --- | --- |
| E100 | offending field | Unknown field is rejected. | actual field value / n.a. |
| E101 | required field | Required field is missing. | n.a. / required |
| E102 | typed field | JSON value has the wrong type. | value / expected type |
| E103 | enum field | Value is outside the supported enumeration. | value / enum set |
| E104 | `/` | JSON cannot be parsed. | parser detail / valid JSON |
| E105 | numeric field | Number is non-finite. | value / finite number |
| E200 | `/catalog` | Manifest directory is unavailable. | n.a. / readable catalog root |
| E201 | manifest/templateId | Template ID is duplicated. | duplicate ID / unique ID |
| E202 | manifest/assetGuid | Manifest GUID cannot resolve to an asset. | GUID / existing asset |
| E203 | manifest/assetPath | GUID resolves to a different asset path. | resolved path / manifest path |
| E204 | manifest/manifestVersion | Manifest is missing or unsupported. | version / `1` |
| E205 | manifest identity field | Required manifest text is empty. | value / non-empty string |
| E206 | manifest/assetPath | Template path is not canonical and protected. | path / Templates path |
| E207 | manifest/cost field | Cost must not be negative. | value / `[0,+inf)` |
| E208 | parameter/binding | Binding symbol is empty. | value / registered symbol |
| E209 | parameter declaration | Parameter definition or numeric range is invalid. | declaration/value / declared range |
| E210 | parameter min/max/default | v1 non-numeric declaration is invalid. | value / declared type |
| E211 | manifest/assetGuid | GUID resolver itself failed. | resolver detail / resolvable GUID |
| E212 | manifest file | Manifest JSON could not load. | parser detail / valid manifest |
| E300 | `/` | Recipe object is missing. | n.a. / Recipe object |
| E301 | `/recipeVersion` | Recipe version is unsupported. | version / `1` |
| E302 | `/id` or `/stages` | Recipe identity or stage list is empty. | value / non-empty |
| E303 | stage/id | Stage ID is not unique. | ID / unique ID |
| E304 | stage/duration | Stage duration is invalid. | value / finite `>=0` |
| E305 | module/id | Module ID is not unique. | ID / unique ID |
| E306 | module/attachTo | Attachment is missing, self-referential, cross-invalid, or cyclic. | target / same-stage acyclic ID |
| E307 | module/templateId | Semantic validation lacks a catalog. | n.a. / catalog |
| E308 | module/templateId | Template is not in the catalog. | ID / catalog IDs |
| E309 | module/kind | Module kind differs from its Manifest. | kind / manifest kind |
| E310 | module/templateId | Template dimension differs from Recipe dimension. | dimension / matching dimension |
| E311 | module/parameters/name | Parameter is undeclared. | name/value / manifest parameters |
| E312 | module/parameters/name | Required Manifest parameter is absent. | n.a. / required parameter |
| E313 | module/parameters/name | Parameter JSON type is wrong. | value / manifest type |
| E314 | module/parameters/name | Parameter is outside inclusive bounds. | value / `[min,max]` |
| E315 | module/parameters/name | Numeric input or Manifest bounds are non-finite. | value / finite number |
| E316 | `/revision` | Recipe revision is below one. | value / integer `>=1` |
| E320 | `/behavior/<domain>/type` | Capability token is not registered for that behavior domain. | token / registered tokens |
| E321 | `/behavior/<domain>/<parameter>` | Capability parameter is not declared by the registered token. | value / declared parameters |
| E322 | `/behavior/<domain>/<parameter>` | Capability parameter JSON type is wrong. | value / expected type |
| E323 | `/behavior/<domain>/<parameter>` | Capability parameter is outside its range/enum or a visual slot ID is empty. | value / registered contract |
| E324 | `/behavior/<domain>/type` | Capability token is incompatible with the Recipe Archetype. | token / compatible Archetypes |
| E325 | `/behavior` | Behavior token combination is illegal. | combination / compatible combination |
| E326 | `/style/token` | Style token is not registered. | token / registered style tokens |
| E327 | `/style/*` | Style palette or numeric contract is invalid. | value / style contract |
| E328 | `/behavior/<domain>/<slot>` | Visual slot does not resolve uniquely or references an incompatible Recipe Archetype. | Recipe ID / compatible saved Recipe |
| E329 | `/behavior/<domain>/<slot>` | Visual slot is recursive or exceeds the supported single nesting level. | Recipe ID / non-slotted Recipe |
| E400 | `/` | Budget calculation lacks Recipe or catalog. | n.a. / both inputs |
| E401 | `/budget/estimatedPeakParticles` | Peak particle budget is exceeded. | count / profile limit |
| W401 | `/budget/estimatedPeakParticles` | Peak particles reached the 80% warning threshold. | count / profile limit |
| E402 | `/budget/materials` | Material budget is exceeded. | count / profile limit |
| W402 | `/budget/materials` | Materials reached the 80% warning threshold. | count / profile limit |
| E403 | `/budget/trails` | Trail budget is exceeded. | count / profile limit |
| W403 | `/budget/trails` | Trails reached the 80% warning threshold. | count / profile limit |
| E404 | `/budget/totalDuration` | Duration budget is exceeded. | duration / profile limit |
| W404 | `/budget/totalDuration` | Duration reached the 80% warning threshold. | duration / profile limit |
| E500 | stable module parameter | Manifest binding is not allow-listed. | symbol / compiler allow-list |
| E501 | stable module parameter | Allow-listed binding failed while applying. | exception detail / bindable target |
| E600 | `/output` | Compiler output escaped Generated. | path / Generated child |
| E601 | `/build` or stage path | Temporary/generated Prefab invariant failed. | observed hierarchy/dependency / valid generated Prefab |
| E602 | `/build` | Build transaction failed. | exception detail / successful build |
| E700 | patch document | Patch JSON or Recipe clone is invalid. | parser detail / valid bare array |
| E701 | patch operation field | Patch operation contains a forbidden field. | field/value / operation contract |
| E702 | patch operation | Patch required field/operation is missing. | n.a. / required operation data |
| E703 | patch document | Patch root or operation field type is invalid. | value / required type |
| E704 | patch op | Patch operation is unsupported. | op / allow-list |
| E705 | patch path | Path is not a stable-ID allow-listed Patch path. | path / v1 path grammar |
| E706 | patch path | Stable path target does not exist. | target / existing stage/module/parameter |
| E707 | `/revision` | Expected revision conflicts; no write occurs. | expected revision / current revision |
| E708 | patch module path | Required travel energy-body cannot be removed. | target / retained module |
| E709 | patch add path | Added module shape or ID is invalid. | module / complete matching module |
| E710 | `/transaction/snapshot` | Transaction snapshot could not be captured. | failure detail / restorable snapshot |
| E711 | `/transaction/rollback/*` | Rollback failed; manual recovery is required. | rollback path/detail / restored state |
| E712 | `/recipeVersion` | v1 Patch service rejects Recipe v2; use the isolated Slash Patch service. | value / `1` |
| E720 | frozen cohort path | Historical Cohort acceptance operation/effect differs. | actual operation / frozen operation |
| E900 | exporter path | Formal AI-workflow export failed. | exception detail / exportable source |
| I400 | `/budget` | Static budget preflight completed. | computed profile / n.a. |

## Production rules and post-MVP archetype compilers

| Code | Stable path | Human meaning | actual/allowed |
| --- | --- | --- | --- |
| E1600 | `/` | Impact Recipe JSON cannot be parsed. | parser detail / valid JSON |
| E1601 | Impact offending field | Impact Recipe contains an unknown field. | field value / closed contract |
| E1602 | `/revision` | Impact Recipe revision is below one. | value / integer `>=1` |
| E1603 | `/id` | Impact effect ID is not lower snake case. | value / lower snake case |
| E1604 | `/targetProfile` | Impact target profile is unsupported. | value / supported profile |
| E1605 | `/shardCount` | Impact shard count is outside the safe range. | value / `[4,20]` |
| E1606 | Impact fixed field | Impact fixed contract value differs. | value / expected value |
| E1607 | Impact numeric field | Impact number is non-finite or outside range. | value / declared range |
| E1608 | Impact required field | Impact required field is missing. | n.a. / required |
| E1609 | Impact typed field | Impact field has the wrong JSON type. | value / expected type |
| E1610 | `/shared` | Impact shared library preparation failed. | exception / prepared shared assets |
| E1611 | `/build` | Impact transactional build failed. | exception / successful build |
| E1700 | `/` | Area Recipe JSON cannot be parsed. | parser detail / valid JSON |
| E1701 | Area offending field | Area Recipe contains an unknown field. | field value / closed contract |
| E1702 | `/revision` | Area Recipe revision is below one. | value / integer `>=1` |
| E1703 | `/id` | Area effect ID is not lower snake case. | value / lower snake case |
| E1704 | `/targetProfile` | Area target profile is unsupported. | value / supported profile |
| E1705 | `/flameCount` | Area flame count is outside the safe range. | value / `[12,40]` |
| E1706 | Area fixed field | Area fixed contract value differs. | value / expected value |
| E1707 | Area numeric field | Area number is non-finite or outside range. | value / declared range |
| E1708 | Area required field | Area required field is missing. | n.a. / required |
| E1709 | Area typed field | Area field has the wrong JSON type. | value / expected type |
| E1710 | `/shared` | Area shared library preparation failed. | exception / prepared shared assets |
| E1711 | `/build` | Area transactional build failed. | exception / successful build |
| E8000 | `/rules` | Production rule configuration/audit failed to load. | exception / valid rules |
| E8001 | `/runtimeEntry/path` | Runtime Entry prefab is missing. | path / existing prefab |
| E8002 | `/runtimeEntry/path` | Runtime Entry is outside its owned output folder. | path / owned folder |
| E8005 | runtime renderer/materials | Runtime Renderer has a missing Material. | renderer / assigned materials |
| E8006 | runtime renderer/shader | Runtime Material has a missing Shader. | material / assigned shader |
| E8008 | runtime transform path | Runtime Entry contains a missing MonoBehaviour. | object / valid component |
| E8009 | runtime transform path | Runtime Entry contains an Editor-only component. | type / player-safe type |
| E8010 | runtime transform path | Runtime Entry contains a forbidden preview/evidence component. | type / production component |
| E8013 | `/dependencies` | Runtime Entry has an out-of-policy dependency. | path / allowed roots |
| E8014 | `/sourceRecipePath` | Strict output lacks a saved matching source Recipe. | path/hash / matching Recipe |
| E8019 | `/archetype` | Production archetype configuration is invalid. | archetype / registered rules |
| E8020 | `/formalProduction` | Formal production authority binding is missing, incomplete, or inconsistent with the gate-issued plan. | binding / gate-issued immutable binding |

## S12 Slash v2

| Code | Stable path family | Meaning | actual / allowed |
| --- | --- | --- | --- |
| E1200 | `/` | Invalid v2 Recipe JSON. | parser detail / valid JSON |
| E1201 | `/<field>` | Unknown v2 Recipe field. | field / closed schema |
| E1202 | `/recipeVersion` | Missing or non-integer version. | value / integer |
| E1203 | `/recipeVersion` | Unsupported dispatch version. | value / `[1,2]` |
| E1204 | `/<required>` | Required v2 value absent. | n.a. / required value |
| E1205 | `/<field>` | Wrong v2 JSON type. | value / type |
| E1206 | `/<field>` | Invalid v2 scalar or identifier. | value / contract |
| E1210 | `/recipeVersion` | Slash semantic version unsupported. | value / `2` |
| E1211 | `/id` | Slash identity/archetype/dimension invalid. | value / Slash contract |
| E1212 | `/revision` | Slash revision invalid. | value / integer >= 1 |
| E1213 | `/timeline` | Slash timeline invalid. | value / finite timeline |
| E1214 | `/phases` | Fixed Slash phase/module structure invalid. | value / five-phase contract |
| E1215 | `/phases/<id>` | Slash timing/story invalid. | value / timeline story |
| E1216 | `/metadata` | Slash metadata invalid. | value / contract |
| E1217 | `/randomSeed` | Slash seed invalid. | value / unsigned integer |
| E1218 | `/phases/<id>/modules/<id>/templateId` | Template/catalog mismatch. | value / formal catalog |
| E1219 | `/phases/<id>/modules/<id>/parameters/<name>` | Manifest parameter missing or unknown. | value / declaration |
| E1220 | `/phases/<id>/modules/<id>/parameters/<name>` | Manifest parameter type/range invalid. | actualValue / allowedRange |
| E1230 | `/budget` | Slash budget input unavailable. | n.a. / catalog |
| E1231 | `/budget/<metric>` | Slash static cap exceeded. | actualValue / allowedRange |
| I1230 | `/budget` | Slash static budget evaluated. | computed values / n.a. |
| E1240 | `/manifests` | Slash Manifest JSON/root invalid. | value / manifest |
| E1241 | `/manifests/<template>` | Manifest identity/path invalid. | value / contract |
| E1242 | `/manifests/<template>/parameters` | Manifest declaration invalid. | actualValue / allowedRange |
| E1243 | `/manifests/<template>/cost` | Manifest cost invalid. | actualValue / allowedRange |
| E1244 | `/manifests/<template>` | Manifest asset/material reference invalid. | actualValue / allowedRange |
| E1250 | `/recipeVersion` | Slash compiler received v1. | value / `2` |
| E1251 | `/build` | Slash build transaction failed. | exception / success |
| E1252 | `/build` | Generated Slash Prefab invariant failed. | observed output / valid Prefab |
| E1253 | `/id` | Non-managed v2 id attempted output write. | value / `slash_3d_stylized` |
| E1280 | `/recipeVersion` | Slash Patch received v1. | value / `2` |
| E1281 | `/revision` | Patch revision conflict. | actualValue / current revision |
| E1282 | `/` | Patch Recipe clone failed. | parser detail / cloneable Recipe |
| E1283 | `/recipe` | Patch asset path is invalid/missing. | path / formal Slash Recipe |
| E1284 | `/history` | Existing Patch history invalid. | parser detail / bare array |
| E1285 | `/transaction` | Patch snapshot/transaction failed. | exception / rollback |
| E1286 | `/transaction/rollback/<area>` | Patch rollback/backup cleanup failed. | detail / restored state |
| E1287 | `/` | Patch is not a bare array. | value / bare array |
| E1288 | `/` | Patch count outside 1–12. | value / `[1,12]` |
| E1289 | `/<operation>` | Patch object is not exact replace/path/value. | value / operation grammar |
| E1290 | `/<operation>/path` | Patch path is not stable Slash grammar. | value / stable path |
| E1291 | `/<operation>/path` | Patch repeats target path. | value / unique path |
| E1292 | `/phases/<id>/modules/<id>/parameters/<name>` | Patch target not formal. | value / declared parameter |
| E1293 | `/phases/<id>/modules/<id>/parameters/<name>` | Patch replacement type/range invalid. | actualValue / allowedRange |
| E1800 | `/recipe` | Styled-content Recipe asset is missing. | asset path / existing Recipe |
| E1801 | `/style/token` | Style token is not registered. | actualValue / style registry |
| E1802 | `/style/token` | Style does not support the Recipe dimension. | style and dimension / supported dimensions |
| E1803 | `/build` | Styled Runtime Entry construction failed. | exception detail / valid strict output |
| E1810 | `/archetypeParameters/<name>` | Parameter is not registered for the selected Archetype. | actualValue / registered keys |
| E1811 | `/archetypeParameters/<name>` | Archetype parameter type, enum, or range is invalid. | actualValue / allowedRange |
| E1812 | `/archetypeParameters/<name>` | Required Archetype parameter is missing. | n.a. / required field |
| E1820 | `/content` or `/content/family` | Content Recipe id is not registered or its element family does not match. | actualValue / registered id and family |
| E1821 | `/content/parameters/<name>` | Content parameter is unknown or a required registered parameter is missing. | actualValue / registered keys |
| E1822 | `/content/parameters/<name>` | Content parameter type, enum, or inclusive range is invalid. | actualValue / allowed type or range |
| E1830 | `/recipe` | Independent-content Recipe asset is missing. | asset path / existing Recipe |
| E1831 | `/id` | Recipe id is not registered in the W11/W12/W14/W17 content catalog. | actualValue / registered id |
| E1832 | `/build` | Independent-content Runtime Entry construction failed. | exception detail / valid strict output |
| E1840 | `/runtime/protocol` | Runtime method or event is unsupported by this content entry. | method or event / entry-specific protocol |
| E1841 | `/runtime/anchor_rect` | Follow mode was requested without a RectTransform anchor. | null / external RectTransform |
| E1842 | `/runtime/endpoints` | A supplied world endpoint contains NaN or Infinity. | endpoint / finite Vector3 |
| E1850 | `/timeline`, `/camera_hints`, `/gates` | Composite-only fields were used by a non-Composite Recipe. | field / Composite archetype |
| E1851 | `/timeline` | Composite Recipe has no playable timeline. | missing/empty / non-empty timeline |
| E1852 | `/timeline/<index>` | Timeline time, order, action, reference, or stop target is invalid. | event / ordered play-stop contract |
| E1853 | `/timeline/<index>/overrides/<field>` | Composite override is outside the stable palette/transform allow-list. | field/value / palette, scale, position, rotation |
| E1854 | `/camera_hints/<index>` | Camera hint time, type, strength, or order is invalid. | hint / shake, zoom, slowmo contract |
| E1855 | `/gates/<index>` | Stage gate id, time, order, or uniqueness is invalid. | gate / ordered unique external event |
| E1856 | `/id` or `/recipe` | Composite Recipe is missing or not registered in the formal composition catalog. | id/path / registered Composite |
| E1857 | `/timeline/<ref_id>` | Referenced Runtime Entry Prefab is missing. | manifest runtime path / existing Runtime Entry |
| E1858 | `/build` | Composite Runtime Entry construction failed. | exception detail / valid strict output |
| E1859 | `/budget/<metric>` | Composite simultaneous peak exceeds its registered composition limit. | computed peak / particles 200, PS 10, materials 10, renderers 14 |

## Element next-candidate compiler (W3–W8)

| Code | Stable path | Human meaning | actual/allowed |
| --- | --- | --- | --- |
| E1930 | `/content/family` | Element next-candidate compiler accepts only the W3–W8 authority families. | actualValue / registered family list |
| E1931 | `/id` | Recipe is not registered in the W3–W8 element next-candidate cohort. | actualValue / registered cohort id |
| E1932 | `/content/family` | Recipe id and element family disagree. | actualValue / id-implied family |
| E1933 | `/content/parameters/<name>` | Content parameter has no physical carrier/timing binding. | actualValue / bound carrier or timing |
| E1934 | `/targetProfile` | Compiled next-candidate budget exceeds the fixed family ceiling. | computed budget / family ceiling |
| E1935 | `/recipe` | Element next-candidate Recipe asset is missing. | asset path / existing Recipe |
| E1936 | `/build` | Element next-candidate build transaction failed. | exception detail / successful build |
| E1937 | `/build/prefab` | Next-candidate Prefab is missing after save. | n.a. / saved Prefab |
| E1938 | `/build/runtimeEntry` | Next-candidate Prefab must own exactly one `IVfxRuntimeEntry`. | actualValue / exactly 1 |
| E1939 | `/build/runtimeEntry` | Dedicated element visual executor is missing. | n.a. / `ElementNextCandidateVisualExecutor` |
| E1940 | `/budget/renderers` | Renderer budget exceeded. | actualValue / allowedRange |
| E1941 | `/budget/particleSystems` | Candidate must use one pooled deterministic detail batch. | actualValue / exactly 1 |
| E1942 | `/build/physics` | Element visual execution must remain deterministic and Rigidbody-free. | actualValue / 0 Rigidbody |
| E1943 | `/budget` | Runtime budget readback failed. | n.a. / executor budget within limits |
