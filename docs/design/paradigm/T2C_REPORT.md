# T2c 交付报告（T2C_REPORT）

日期：2026-09-06
执行：T2c 子 agent（worktree `D:\wt\i2s-t2c`，分支 `task/T2c-paradigm-samples`，基线 `242223dc` 前接 T2b `9d14e131` 线）；单元 A/B/C 由前任完成，D/E/F 由续做者完成
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）；规格线：`COMPILER_BOUNDARY_V2.md`、四份技术族规格、`STYLE_IMPL_CARTOON_PIXEL.md`、`GALLERY_SPEC.md`、`docs/design/references/REFERENCE_ANALYSIS.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本卡覆盖 |
|---|---|
| **原型** | 3 / 58：`shield`（状态持续）、`chain_link`（战斗连锁）、`dissolve_out`（反馈消散）——跨 3 个分类骨架 |
| **元素** | 3 / 13：`ice`、`lightning`、`poison`（poison 预设本卡新增） |
| **风格** | 1 / 2 首批：`cartoon`（pixel 维度样片属 T4 铺开） |
| **维度** | 2 / 2：3D（体积/网格/3D 局部光）与 2D（quad+SortingGroup+Light2D）各一整组 |
| **样片合计** | 3 原型 × 3 元素 × 1 风格 × 2 维度 = **18 张范式样片**，全部 PM 档，确定性种子，自包含预制体 |
| **旧范式资产** | **清零**（§4 删除清单）——sprite 模板库、全部旧 recipe/Generated/Preview/清单、仅服务旧资产的代码与测试 |

纪律自检：不以单一特效为目标——样片清单跨 ≥3 原型 × ≥3 元素（§10-1），具体组合名只出现在本报告与画廊页资产。

---

## 1. 按单元改动（提交列表）

| 单元 | 提交 | 内容摘要 |
|---|---|---|
| A（前任） | `a07ec32c` | `VfxSampleAssembler` 样片构造器（原型×元素×风格×维度×档通用输入面，确定性种子，子资产同目录自包含）+ 3D 九格入库 + 控制器 v2 扩展（`hitAt`/`setIntegrity`/`break` 涟漪槽、节点序列 `onHop`+逐跳揭示、2D 债务碎片）+ `VFX_ShellRipple` 补 `mat_shell_ripple` 缺口 + GlowStack billboard 顶点路 + poison 预设 + C_over 32×32 p95 静态估计（碎片组并集）+ 画廊 3D 判定页（行=三原型，列=三元素） |
| B（前任） | `cdd13eb4` | 2D 九格：同构造器 2D 投影（quad+SortingGroup+排序偏移 −40..+24+Light2D+Rigidbody2D 碎片+SDF 环退化菲涅尔），画廊 2D 判定页指向 `Generated/2d`，门禁测试同套覆盖 2D 维度 |
| C（前任） | `242223dc` | 截帧对比：`T2cParadigmSampleCaptureTests`（复用 W24 recorder，7 节拍帧 × 双 Bloom pass × 2 维度，GA-8 场景走 `LoadSceneInPlayMode`，profile/源哈希齐全，证据落 `test-results/gallery-capture`）+ contact-sheet（参考图标尺条 + 四辅助指标）入库 `docs/design/paradigm/t2c-review/` + 质量两轮收敛（详见 §3） |
| D | `674ea539` | 旧范式资产全清（§4），.NET 侧连锁 re-export（§5），全量双绿 |
| E | `09d98c17` | `AssetAllowList.json` 落盘 + WL-1/2/3/6 双向真实性测试；156 谓词登记表 `GATE_PREDICATE_LEDGER.md` + fail-closed 审计测试；台账 #12 C_over 校准终值；`mat_liquid_blob` 折射槽组合变体 `VFX_LiquidBlobRefract` |
| F | 本提交 | 本报告 |

---

## 2. 样片与画廊产物

- 样片：`project/Assets/VFX/Generated/3d/` 与 `.../2d/` 各 9 个目录（`{archetype}_{element}_cartoon/`），每目录一个自包含预制体 + 克隆材质 + 生成网格（子资产同目录）。
- 画廊：`project/Assets/VFX/Gallery/VFXGallery_3D.unity` / `VFXGallery_2D.unity` + 判定页资产 `GalleryPages_3D.asset` / `GalleryPages_2D.asset`（行=shield/chain_link/dissolve_out，列=ice/lightning/poison）。
- 审阅材料：`docs/design/paradigm/t2c-review/contact-sheet_{3d,2d}_verdict.png`（7 节拍帧 × Bloom 开/关双行 + 参考图标尺条）+ `metrics.json`（四辅助指标）。

---

## 3. 样片质量六准则自检（REFERENCE_ANALYSIS §2）

前任 C 单元做了两轮质量收敛（提交信息记录），本表按六准则展开：

| # | 准则 | 落地机制 | 自检 |
|---|---|---|---|
| 1 | 亮度分级而非渐变（≥3~4 台阶） | `VfxHdrGrade` 四级倍率链 0.06/0.30/1.0/4.0（相邻 ≥2.0，HG-1/HG-2 数值门禁）；元素预设 5 色线性 HDR 色板 | **过**：实测 `metrics.json` luminanceStops 3~4（3D bloom-on 3，其余 4）；收敛第 2 轮把光晕亮度按色板归一化，防止辉光淹没台阶 |
| 2 | 硬柔并存 | 主体 SDF/阈值硬边（`_EdgeSharpness` 卡通默认 0.85）+ GlowStack 柔光层 | **过**：edgeSharpnessRatio 3D 10.9 / 2D 16.4（硬边与柔晕的梯度比显著 >1） |
| 3 | 形态各向异性 | 运动轴与垂直轴噪声频率分离（`VfxCommon` 噪声族）；lightning 锯齿 1D 各向异性最强 | **过（弱项登记）**：anisotropyRatio 3D 1.16 / 2D 1.02——2D 判定页在帧 60 的静态截面上各向异性弱（shield 系正圆结构占 2/3 画面属预期），运动中各向异性由节拍驱动；T3 输入#4 登记 |
| 4 | 低亮大范围雾幕 | GlowStack 第 3 层（大半径极淡）+ veil 类层 | **过**：收敛第 2 轮 STEP 衰减改量化高斯能量分布，雾幕层能量不再被阶梯截断 |
| 5 | 离散高亮点克制而极亮 | 碎片/火星数量 PM 档 `C_frag ≤ 96`，实际样片碎片 ≤32；hot 段 ≥1.5（HG-2） | **过**：门禁数值断言 + contact-sheet 人工核对 |
| 6 | 光晕溢出（自带七八成 + 用户 Bloom 补足） | GlowStack 自带光晕参数面全量（§3bis 参数化）；画廊 Bloom 可开关（B 键） | **过**：glowSpill 3D 0.031（自带光晕溢出量存在且受控）；Bloom 开/关双行对比可见"关掉 Bloom 也成立"（GA-4） |

收敛记录（C 单元两轮）：① 光晕亮度按色板归一化 + 尺寸补偿反除；② 卡通光晕豁免量化（STYLE_IMPL 2.8-1）改走 4 级阶梯环 + STEP 衰减改量化高斯能量分布；每轮后样片重建、门禁重跑。

---

## 4. 删除清单（单元 D，逐项）

### 4.1 Unity 资产

| 组 | 内容 | 数量 |
|---|---|---|
| sprite 模板库 | `Assets/VFX/Templates/`（2D 6 模板 prefab+manifest+材质+贴图；3D 6 模板；Slash 域 prefab/网格/贴图/序列帧纹理/manifest） | 全树 |
| 旧 Generated 产物 | `Assets/VFX/Generated/` 下 254 个旧目录（fireball_2d/3d、cap_* 能力系、w13nc_* 候选系、风格样片系、hero kit、UI 系、volume 系…）；**保留** `2d/`、`3d/` 新样片目录 | 254 目录 |
| 旧 recipe | `Assets/VFX/Recipes/` 全树（fireball 默认 + 25 个分类子目录） | 全树 |
| 预览场景 | `Assets/VFX/Preview/` 30 个 S/W 系预览场景 + S12 AI 验证 prefab/材质 | 全树 |
| 构建清单 | `ProjectSettings/VFXComposer/BuildManifests/` 293 个旧 manifest | 293 文件 |
| Spike / Authoring | `Assets/VFX/Spike/S12/`、`Assets/VFX/Authoring/`（S5/S10/S12 模板作者脚本） | 全树 |
| 旧 Shared | `Shared/{Capability,CoverageGalleryB,Fire,Frost,Styles,ValidationGallery,W11W13NextCandidate,W15NextCandidate,ElementNextCandidate}` + `Shared/Shaders` 全部 14 个旧 shader + 3 个 Hidden 诊断 shader（保留内容 GUID 引用扫描 = 零命中后删除） | 全树 |
| 测试期垃圾 | 基线测试运行时旧作者代码再生的 `Assets/VFX/NextCandidates/`（W3W5/W6W8 元素系） | 全树 |

### 4.2 Editor 代码（仅服务旧资产，勘察确认后删）

`Archetypes/ Area2D/ Composite/ Elements/ Impact2D/ Independent/ NextCandidates/ Preview/ SlashV2/ Style/ ValidationGallery/ W11W13NextCandidate/ Workflow/` 13 个目录（S/W/cohort 系作者与证据链、S12 slash v2 编译器、风格系统、AI workflow 导出器）；`Capabilities/` 3 个预览场景文件（保留 `CapabilityRegistry`/`CapabilitySlotValidator`——v1 recipe 校验管线仍消费）；`W24/` 下 `S0a S0b S3 S4` + `W24FirstFormalBuildTransaction`、`W24FormalBatchAuthoringEntrypoints`、`W24PreviewRendererInfrastructure`、`W24StatusRegistry`、`W24CandidateIdentityFreezer`、S5 的 5 个证据写读器；`Rules/VfxProductionRulesMenu.cs`（重建旧三产物的菜单）。

**保留**（共用基础设施）：`VfxCompiler` 及其原子提交/回滚机制、`VfxRecipeBuildEntrypoint` 三件套写入面、`VfxOutputAuditor`（R80xx 门禁沿用）、`TemplateCatalog`（空目录=空目录 fail-closed）、W24 recorder（`W24ContinuousCaptureRecorder`/`W24CaptureProfile`/`W24EvidenceStore`——T2c 截帧管线消费）、`W24S5ProductionGate`+S1 契约/追踪、S6 worker/MCP 全套、Studio/Compiler 窗口（死分支清除：S12 分派、旧预览场景打开、Style 引用）。

### 4.3 Runtime 代码

`Components/` 除 `CameraFacingBillboard`、`GeneratedVfxController`、`IVfxRuntimeEntry` 外全部旧控制器与预览驱动（Slash 系 6、Styled/Sustained/Composite/Coverage/Interaction/Validation/Planned/Beam/TimingArea/W1 系等 30+ 文件）；`Capabilities/ ElementsNextCandidate/ StyleSpecialsNextCandidate/ W11W13NextCandidate/ W15/ W17W18NextCandidate/ W24/` 7 个目录；`Diagnostics/` 孤儿诊断捕捉 6 文件（NpyWriter/ObjectIdDepth/RendererMask/TrailMask/LinearLdr/ObjectRegistration）。

### 4.4 测试

EditMode 删 77 文件（含 `TemplateVisualQualityTests`+其豁免表、全部 S9 cohort 证据链、S10/S11/S12 系、W1~W24 旧内容系、CompilerIntegration/FormalTemplateIntegration 等旧模板依赖）；PlayMode 删 38 文件 + PlayerEvidence 场景；TestData 删 `fireball-2d.structure.snapshot.txt`。保留并修订：`ProductionRulesTests`（legacy 清零断言 + 旧表面全退休断言）、`VfxRecipeBuildEntrypointTests`（正路径改"空目录 fail-closed 拒绝"语义）、`W24S6EditorIntegrationTests`（fixture 自建/自清 Recipes+Preview 根）、`DomainValidationTests`（合成 manifest，无磁盘依赖）。

### 4.5 .NET 侧连锁（§5 详述）

`batches/` 11 个 recipe 固件与 3 个 manifest 中的旧模板 id 全部替换（文件保留，内容指向新变体面）；旧 6 模板行从快照删除。

---

## 5. .NET AI 回路的最小一致替代

- **模板目录快照** `recipe-v1-template-catalog.snapshot.json` re-export 为 `templateCatalogVersion 2.0.0`：6 行 = 范式技术变体的 v1 形状投影（`mat_volume_fbm`=energy_body、`mesh_sweep_band`=motion_trail、`cpu_buoyancy_turbulence`=secondary_particles、`cpu_burst_radial`=impact_burst、`mat_glow_stack`=impact_flash、`mat_ring_polar`=shockwave），canonicalExample 改 `probe_projectile_2d`。
- **登记缺口（留 AI 回路改造卡）**：v1 快照形状（templateId+参数表）无法表达 Recipe v2 面（58 原型 / 13 元素 / 2 风格 / 层结构 / 谓词），本次仅做语义最近邻投影维持 AI 回路可运行；prompt 从模板表变为"作曲规则 + 三轴目录"属 ADR-010 §11 的 AI 回路改造卡。
- **预设骨架** 6 卡与 **精修知识** fragment（v2，源文档 `refine-artist-knowledge.md` 同步改写 + sourceSha256 重 pin）随之切换；prompt 组合版本 `refine-knowledge/2`，System 消息 pin 4607→4609 字符重 pin。
- **AI.Tests 294 / Desktop.Tests 187 / Cli.Tests 142 全绿**；哈希 pin、金样、成本断言全部同步。

---

## 6. 测试数字前后对照

| 面 | 开工基线 | 交付 | 说明 |
|---|---|---|---|
| Unity EditMode 全量 | 1004 总 / 1 失败* / 54 跳过（846 基线 + 前任 A/B/C 新增） | **572 总 / 572 过 / 0 失败 / 0 跳过** | 下降 = 旧内容测试删除（预期）；上升面 = 单元 E 新增 9 条（AllowList 5 + Ledger 4）。*基线唯一失败 `W24StatusRegistry` 系旧 Generated 库存扫描断言，随旧资产与该注册表一并退役 |
| .NET 全量（`dotnet test -c Release`） | 1106 / 1 失败*（12 套件） | **1106 / 0 失败（12 套件）** | *基线唯一失败 = 任务书已知 flake `PostAdmissionChildExit*`，单跑绿；交付轮全绿。总数不变：删除的旧模板 pin 测试均改指新变体面而非删除 |
| PlayMode（T2c 截帧套件） | 前任 C 交付绿 | 未重跑 | 样片资产与截帧管线本批未改动（D/E 不触样片）；证据 `test-results/gallery-capture/` 与 t2c-review 产物在库 |

前置：`JobExecutorLockHost` 先行 `dotnet build -c Release` 成功。

---

## 7. C_over 终值（台账 #12）

18 样片静态估计（32×32 p95、碎片组并集、粒子按发射体+速度×寿命膨胀）：**1~5 层**，全部 ≤ PM 档上限 6，零 W403/E300。截帧实测代理（前景覆盖率 3D 0.710 / 2D 0.070，Bloom 无关）与 contact-sheet 人工核对未发现"估计 < 可见堆叠"反例。**校准结论：膨胀系数 2.0、网格 32×32、p95 全部维持，无系统性偏差，白名单 schema 版本不升。** 明细：`GATE_PREDICATE_LEDGER.md` §13 + `test-results/t2c-cover-calibration.csv`。

---

## 8. 用户九格判定指引（ADR-010 §8）

1. **打开场景**：`Assets/VFX/Gallery/VFXGallery_3D.unity`（透视相机）与 `VFXGallery_2D.unity`（正交相机）——两个独立场景，各自点 Play 即开始。
2. **判定页**：Play 后默认页即范式判定页（页资产 `GalleryPages_3D.asset` / `GalleryPages_2D.asset`）：**行 = shield / chain_link / dissolve_out，列 = ice / lightning / poison**，风格 cartoon、档位 PM。左右方向键翻页，空格全格重播，数字键 1~9 单格重播，0 全停。
3. **Bloom 开关**：Play 中按 **B** 键切换场景 Volume 的 Bloom（开 = 完整观感，关 = 资产裸质量；ADR-010 §4bis-6 判定面）。
4. **contact-sheet**（不进 Unity 也可初判）：`docs/design/paradigm/t2c-review/contact-sheet_3d_verdict.png` 与 `contact-sheet_2d_verdict.png`——每张 7 节拍帧 × 上行 Bloom 开 / 下行 Bloom 关 + 底部参考图标尺条；辅助指标 `metrics.json`。

---

## 9. 已知限制与 T3 输入

1. **GPU 粒子 14 个 .vfx 模板未产出**（T2b 遗留，引包属依赖变更需人工裁定）；AllowList `vfxTemplates: []` fail-closed，任何 .vfx 引用即拒。T3 引包后逐模板作 VFX Graph 版并补 GP-1~9。
2. **156 谓词中 109 条 T3 待做**：断言面（编译器 v2 产物/sidecar/编译报告）尚不存在，登记表已逐条列明；4 条条件豁免（SG-1/MG-1/PR-1/GP 族）含理由与到期卡。
3. **Recipe v1 AI 回路是过渡投影**：快照 2.0.0 只承载 6 个变体的 v1 形状面；完整三轴目录 prompt 属 AI 回路改造卡（ADR-010 §11）。`VfxCompiler` v1 管线保留但空目录下 fail-closed（E308），Recipe v2 编译器属 T3。
4. **2D 静态截面各向异性弱**（anisotropyRatio 1.02）：shield 正圆结构占比 + 静帧测量所致；运动中各向异性与 2D 拉伸光晕轴属 T3 打磨输入。
5. **`VfxLightBeat.followTarget` 未实现**（T2b 遗留 §6-5，需原型层表 `latestNode` 语义）；`sg_comp_soft_depth_fade` 的 domeBillboard 降级几何未实现。
6. **`mat_liquid_blob` 折射组合变体已入库但无样片消费**：变体 shader（RF-1 达标）+ AllowList 登记完成；进入样片/配方属 T3/T4 铺开。
7. **像素风样片属 T4**：shader 侧量化机制（`VfxStyleStage.hlsl` 三关键字）已在库并有 PX-6 条件谓词，样片维度未铺。
8. **F 类（UGUI）`mat_ui_*` 三变体未立**（T2b P2 批次），UI-1~5 谓词 T3 待做。

---

## 10. 红线自检

| 红线 | 结果 |
|---|---|
| 绝对禁止序列帧 | **通过**。删除面：Slash 序列帧纹理随模板库清零。新增面：`VFX_LiquidBlobRefract` 零 Texture 属性（`ShaderLibrary_ZeroTextureSamplingProperties` 覆盖）；样片门禁 `Sample_NoFlipbook_NoTextureSampling` 持续在库 |
| 资产外不碰 | **通过**。本批零资产外写入；画廊 Bloom 属场景工具（拍板 #11），Gallery 不在依赖根（GA-9 在库） |
| 不以单一特效为目标 | **通过**。D/E/F 无新样片；新增变体按形态能力命名（`LiquidBlobRefract`）；具体组合名仅在判定页与本报告 |
| 只依赖 Unity + 既有 feed，不引新包 | **通过**。单元 A（`a07ec32c`）新增 `com.unity.modules.physics2d: 1.0.0`（Unity **内置模块**，source: builtin，2D 债务碎片的 Rigidbody2D 需要；lock 文件同步）——与 T2b 启用 cloth/screencapture 同性质，零外部依赖、零 registry 包；D/E/F 单元零 manifest 变更；.NET 零新依赖 |
| 删除与替代同批闭合 | **通过**。单元 D 一个提交内完成删除+替代+双绿；任何时刻全量不处于"无替代物的红"（删除轮次间以编译检查+全量验证） |
| 禁写主仓 / 未 push / 不碰 master | **通过**。全部提交在 worktree 分支 `task/T2c-paradigm-samples`，未 push |

---

## 11. 交付定义核对

- [x] 单元 D / E / F 各 1 提交（`674ea539` / `09d98c17` / 本提交），加前任 A/B/C 共 6 笔
- [x] EditMode 全量 0 失败（572/572，新基线如实报：846→旧内容删除→563→单元 E +9→572）
- [x] .NET 全量 0 失败（1106，12 套件）
- [x] 工作区干净；未 push；未碰主仓
