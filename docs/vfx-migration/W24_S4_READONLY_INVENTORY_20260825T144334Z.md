# W24 S4 既有资产只读审计快照

- 审计模式：`READ_ONLY`；本工具不会写入 Unity Project。
- 视觉结论：`NOT_EVALUATED`；全部条目保持 `VISUAL_PENDING`，不产生 L0–L4 或视觉通过。
- 迁移 Apply：`false`；ADR-001 仍冻结 M3 时不得迁移。
- Inventory hash: `sha256:93b3e9406e0095b70ef911fed259f7c02e977a0b5232a83f61aa2311c8dab233`
- ADR-001: `Proposed`；M3 frozen: `True`

## 汇总

| 指标 | 数量 |
|---|---:|
| 正式条目并集 | 220 |
| Generated 目录 | 208 |
| 可解析 Manifest | 220 |
| LegacyRetain | 3 |
| QuarantineReview | 12 |
| RebuildCandidate | 205 |

## 条目路由

| EffectId | 风险 | 路由 | 批次 | 所有权 | 合同/Trace | 阻塞项 |
|---|---:|---|---|---|---|---|
| `acid_lob_projectile_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ambient_dust_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `anime_charge_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `anime_smear_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `arc_lightning_beam_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `arcane_missile_projectile_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `arcane_rune_spawn_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `astral_aura_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ball_lightning_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `blade_tempest_ultimate_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `blizzard_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `blood_ritual_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `boulder_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `boulder_projectile_3d_lowpoly` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `bubble_shield_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `burning_status_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `button_confirm_burst_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `button_press_fx_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `candy_pop_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_accel_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_arclink_beam_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_boomerang_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_bounce_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_chainhop_proj_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_chainseq_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_channel_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_charge_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_charge_release_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_converge_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_delayfuse_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_demo_charge_occlude_holo_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_demo_fan_wave_cartoon_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_demo_telegraph_nova_holy_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_expand_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_growth_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_hexflash_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_hitscan_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_homing_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_implode_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_linear_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_movingzone_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_occlude_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_orbit_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_parabola_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_pierce_proj_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_reflect_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_residue_trail_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_split_proj_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_sustained_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_sweep_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_telegraph_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_tickpulse_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_volley_proj_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `cap_wave_proj_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `card_flip_reveal_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `card_merge_fx_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `chain_arc_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `chain_blast_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `chain_grapple_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `channel_tether_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `chest_open_burst_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `combo_surge_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `comet_motion_trail_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `crate_break_destruction_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `critical_strike_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `crystal_shatter_destruction_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `crystal_shield_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `crystal_shield_3d_crystal` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `curse_mark_status_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `daily_check_stamp_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `damage_warning_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `death_dissolve_lifecycle_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `demon_eruption_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `demon_gate_boss_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `dissolve_transform_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `divine_smite_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `dragon_breath_ultimate_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `earth_spike_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `electro_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `elemental_reaction_burst_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ember_rain_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `emp_nova_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `energy_whip_trail_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `falling_leaves_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fire_nova_burst_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fire_nova_burst_3d_semireal` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fire_shield_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_2d` | 51 (MEDIUM) | LegacyRetain | B1-legacy-preservation | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_2d_cartoon` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_2d_neon` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_2d_pixel` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_2d_s8test` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `fireball_3d` | 51 (MEDIUM) | LegacyRetain | B1-legacy-preservation | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `fireball_3d_s10test` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `fireflies_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flame_blade_samurai_kit_showcase_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flame_dash_slash_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flame_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flame_slash_2d_neon` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flamethrower_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `flash_freeze_transform_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `focus_charge_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `freeze_status_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_breath_beam_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_creep_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_decal_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_impact_2d_dark` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frost_nova_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `frozen_domain_ultimate_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `gacha_single_reveal_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `gacha_ten_sequence_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `gale_dash_trail_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `gem_lance_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ghost_curse_shrine_kit_showcase_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `glitch_blink_transform_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `guardian_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `heal_glow_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `healing_bloom_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `healing_bloom_aura_2d_candy` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `hero_entrance_lifecycle_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `hex_guard_shield_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `hit_flash_status_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `holo_barrier_shield_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `holo_scan_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `holy_halo_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `hundred_ghosts_ultimate_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `i1_river_comet` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `i2_glass_spark` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `i3_brazier_bead` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `i4_rail_flare` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `ice_moon_mage_kit_showcase_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ice_shard_projectile_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ice_shard_projectile_2d_inkwash` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ice_spike_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ice_spike_spawn_3d_dark` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `inferno_vortex_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ink_dragon_trail_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ink_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ink_splash_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `judgement_ray_ultimate_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `katana_sheath_ember_idle_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `katana_trail_weapon_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `knockup_launcher_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `levelup_burst_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `lifesteal_link_beam_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `loot_beam_pickup_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `mechanical_hunter_kit_showcase_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `meteor_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `meteor_shower_ultimate_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `mist_fog_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `moonwheel_boomerang_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `muzzle_flash_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `nebula_orb_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `overheat_vent_idle_aura_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `parry_spark_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `phantom_wail_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `phase_dash_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `phoenix_dart_projectile_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `pixel_burst_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `pixel_heal_aura_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `pixel_sword_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `plasma_link_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `poison_veil_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `poly_burst_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `poof_smoke_spawn_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `prismatic_shield_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `prismatic_shield_3d_holo` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `progress_charge_fx_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `quake_stomp_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `rain_weather_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `real_explosion_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `resurrection_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `reward_fly_collect_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `rift_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `s11_a5` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `s11_a6` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `s9_canonical_patch_export_base` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `s9_cohort_k_final_k1` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `s9_cohort_k_final_k2` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `s9_cohort_k_final_k3` | 100 (HIGH) | QuarantineReview | B0-quarantine-review | no | no/no | missing_declared_recipe, missing_four_route_evidence, missing_implementation_trace, missing_or_invalid_runtime_entry, missing_preview_artifact, missing_w24_contract |
| `sandstorm_weather_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `scorch_decal_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `screen_shatter_transition_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `seeker_orb_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `shadow_claw_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `shadow_grasp_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `skill_ready_flash_ui` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `slash_3d_stylized` | 48 (MEDIUM) | LegacyRetain | B1-legacy-preservation | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_w24_contract |
| `smoke_plume_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `snow_weather_volume` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `soul_drain_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `spectral_trail_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `spectral_trail_3d_ghost` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `splash_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `spore_burst_impact_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `staff_charge_idle_aura_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `static_field_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `steam_vent_burst_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `storm_charge_aura_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `summoning_portal_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `summoning_portal_2d_cosmic` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `talisman_barrier_shield_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `thorn_snare_area_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `thunder_strike_impact_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `tidal_wave_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `tornado_area_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `toxic_field_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `twin_portal_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `ultimate_sequence_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `vine_whip_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `void_orb_projectile_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `volt_shield_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `volt_shield_3d_steampunk` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `warning_telegraph_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `water_jet_beam_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `waterfall_env_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `weapon_enchant_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `whirlpool_spawn_3d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |
| `wind_blade_slash_2d` | 35 (MEDIUM) | RebuildCandidate | B3-rebuild-candidates | yes | no/no | missing_four_route_evidence, missing_implementation_trace, missing_preview_artifact, missing_w24_contract |

## 约束

本快照仅提供用户后续抽样和迁移裁决的输入。它不改写 Recipe、Manifest、Generated、Preview 或证据；不执行 Apply；不把共享载体复用解释为视觉同质化。ADR-001 只有 Accepted 且决策人具名后，仍需重新盘点、所有权核验、显式用户 token 和可回滚事务。
