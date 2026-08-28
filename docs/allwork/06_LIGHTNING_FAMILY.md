# W5 雷电族扩展（7 个特效）

> 实现状态（2026-08-25）：7/7 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：在已有 arc_lightning_beam_2d / chain_arc_3d / plasma_link_3d 基础上补全雷电族。电系核心手感是**瞬发、抖动、断续**：所有电弧必须用折线抖动（确定性种子逐帧重采样），禁止平滑曲线冒充闪电。
> 批次预览场景：`VFXPREVIEW_LightningFamily.unity`。
> 配色基准：主 `#8FE8FF` / 辅 `#FFFFFF` / 点缀 `#3D6BFF`；neon 变体主 `#B84DFF`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| thunder_strike_impact_3d | Impact | 3D | one-shot | stylized | 天降落雷柱+地面电爆 |
| ball_lightning_projectile_3d | Projectile | 3D | event | stylized | 球状闪电缓速飘移，周身放电 |
| static_field_area_2d | Area | 2D | sustained | stylized | 地面静电场，随机跳弧+周期 Tick |
| storm_charge_aura_3d | Aura | 3D | sustained | stylized | 蓄雷环身：环绕电弧+云状顶环 |
| electro_slash_2d | Slash | 2D | one-shot | neon | 霓虹电光刀，锯齿刃缘残影 |
| emp_nova_impact_2d | Impact | 2D | one-shot | holo | EMP 环形脉冲+设备故障闪断感 |
| volt_shield_3d | Shield | 3D | event | stylized | 电网球壳，受击弹出反击弧 |

## 2. 规格卡

### thunder_strike_impact_3d
- 分层：主体=主雷柱（2–3 段折线宽带 Mesh，2 帧重采样抖动）+1–2 条分叉；高光=柱芯过曝白；外部能量=落点球形电爆+地面放射电弧 4–6 条；次级=电火花弹跳；消散=残留电离微光 0.4s。
- 时间线：`.0` 顶部预闪 → `.05` 雷柱全亮（雷是瞬发，禁止慢生长）→ `.12` 柱体断续闪 2 次 → `.2` 柱灭、地爆达峰 → `.6` 清空。
- 参数：strike_height 4–10m、fork_count 0–3、ground_arc_count、flash_times 1–3。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### ball_lightning_projectile_3d
- 分层：主体=辉光球核（双层，内实外晕）；高光=球面游走短弧（3–5 条，逐帧换位）；外部能量=向随机方向试探性放电须；次级=尾部电离拖尾；消散=命中放电爆+向 2–3 个方向甩出末端弧。
- 参数：orb_radius、tendril_count、drift_wobble（飘移幅度）、discharge_range。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### static_field_area_2d
- 分层：主体=区域边界电纹环（虚线流动）；高光=随机两点间跳弧（每 0.3–0.8s 一次）；外部能量=地面电网纹理微光；次级=悬浮静电粒点；消散=Tick 时全场亮度脉冲。
- 参数：radius、arc_frequency、tick_interval、net_opacity。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### storm_charge_aura_3d
- 分层：主体=头顶小型雷云环（噪声云带）；高光=云内闷闪；外部能量=身体环绕竖向电弧 2–3 条（随机换位）；次级=脚底电纹环；消散=解除时一次全身放电。
- 参数：cloud_height、arc_swap_interval、charge_level 1–3（影响密度与亮度，供叠层）。
- 预算：粒子 ≤56 / PS ≤4 / 材质 ≤5 / Renderer ≤7。

### electro_slash_2d
- 分层：主体=锯齿刃缘新月刀光（刃缘折线化）；高光=刃芯白电；外部能量=刀路残留 2 帧电弧残影；次级=切口处爆出电火花；消散=残影断续闪灭（电系专属：允许 2 次受控闪烁，与火系"禁止闪烁重现"区分，写入 Manifest 语义）。
- 参数：jag_amplitude、afterimage_count 1–3、spark_count。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### emp_nova_impact_2d
- 分层：主体=扩张同心双环（外环细、内环粗）；高光=环上扫描亮点；外部能量=环过处触发短暂故障色偏条纹（holo 风格 glitch）；次级=环缘掉落像素化碎点；消散=中心核缩灭。
- 参数：ring_radius、glitch_strength、ring_count 1–3。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤4 / Renderer ≤6。

### volt_shield_3d
- 分层：主体=球壳电网（六边网格 UV 流动）；高光=网格节点脉冲；外部能量=壳面随机游走弧光；次级=底部接地电弧偶发；消散=解除时网格逐面熄灭。
- 事件：OnHit → 命中点亮斑+向攻击方向弹出一条反击弧（0.15s）。
- 参数：net_density、walk_arc_count、counter_arc（开关）。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤5 / Renderer ≤7。

## 3. 批次验收
通用 DoD + 附加：所有电弧折线必须确定性种子（同 Recipe 同 build 帧序列一致）；thunder_strike 的"瞬发"验收帧 `.05` 必须已全亮；electro_slash 的受控闪烁次数机器可校验。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将本项统一签署为：**拒绝，无法商用**。本 Scene 未作逐格/逐帧视觉核对，不据此伪造具体雷电视觉问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. 后续 W5 next-candidate（视觉待签署）

后续独立任务已建立新路径；本节保留第 4 节批量拒绝原文。新 Scene 为 `Assets/VFX/Preview/VFXPREVIEW_LightningFamily_NextCandidate.unity`，状态根必须保持 `W5_NEXT_CANDIDATE_VISUAL_PENDING`，输出使用独立的 `Assets/VFX/NextCandidates/W3W5Elements/` 和 compiler `element-next-w3-w5-1`。

- 新 Runtime executor 在固定 30 Hz 或 Recipe 指定间隔上重采样确定性折线；同 seed、同离散步得到相同分叉点，下一步才跳变。空间形状不由平滑 Shader 噪声游移。
- 7 个效果分别执行主落雷+分叉、游走须球、跳弧电网、雷云蓄电、锯齿刀弧+残影、glitch 同心 EMP 环和护盾游走/反击弧；这组拓扑与火、冰载体分离，不是纯配色变体。
- 落雷高度/分叉/地弧/闪次数、球半径/电须/漂移/放电距离、静电场半径/跳弧频率/Tick/透明度、云高/换弧间隔/蓄能等级、电斩锯齿/残影/火花、EMP 半径/glitch/环数、电盾网密度/游走弧/反击开关均驱动真实 LineRenderer、Mesh、粒子或离散时序。
- `.05s` 落雷门禁要求主柱和分叉已进入 Discharge；后续断续闪与 `.2s` 后 afterglow 分开读回。固定上限为 5 条 arc、每条 12 点、7 Renderer、1 ParticleSystem、56 粒子（全局硬上限 120），无 Rigidbody、可池化复播。
- `walk_arc_count` 的注册范围允许到 8；为保持 7 Renderer 硬预算，超过 5 的游走弧按离散步确定性轮换到 5 个池化 arc carrier，而不是同时扩容 Renderer。运行时保留请求层数读回，数值同时改变换位 cadence 与采样拓扑；密集电网受击时，0.15s 反击弧会临时占用其中一个 carrier，随后恢复游走轮换。

新增门禁会检查同 seed 同步确定性、不同离散步的折线路径变化、分叉数到真实 arc 数、蓄/放电/余辉阶段、受控闪烁、预算、清理复播、幂等与 Preview 边界。当前仅静态编译通过，未启动 Unity、未执行测试 XML 或用户视觉签署；机器结果不得将其标为 accepted。
