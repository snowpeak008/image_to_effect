# S15 generated texture inputs

These project-bound raster inputs were created with the built-in `image_gen` tool on 2026-08-23. The approved Slash reference was used only as a visual style/silhouette reference. No source screenshot, background, floor, or reflection is embedded in the assets.

| Asset | SHA-256 | Alpha/use |
| --- | --- | --- |
| `project/Assets/VFX/Templates/3D/Slash/Textures/S15_FieryCrescent_Main_v1.png` | `95AAA08B1851CDBF869A9C146A50960E291ABC46700F69BE155EBCC066C330FB` | RGBA, genuine 0–255 alpha; connected main crescent with no detached sparks. |
| `project/Assets/VFX/Templates/3D/Slash/Textures/S15_FieryCrescent_Base_v1.png` | `E3FE0D295AE69FB31625FACE8679D8B73D7D23488DDF38E8E8BF221700C6F0D0` | RGBA, genuine 0–255 alpha; alternate source including nearby embers. Not the default primary layer. |
| `project/Assets/VFX/Templates/3D/Slash/Textures/S15_SlashBreakupNoise_v1.png` | `6D12AEB46BF3D3FCC69299AE654FB11B03F1049642030312142877916C5D3D8A` | Opaque near-grayscale organic breakup input; shader samples a single channel. |
| `project/Assets/VFX/Templates/3D/Slash/Textures/S15_SparkAtlas_v1.png` | `1AEA9CB82C22E9EC81C185B17225E42E5E62962E17620F911016395678A8DF4A` | RGBA, genuine 0–255 alpha; four isolated spark families in a 2×2 layout. |

## Final prompt set

### Main crescent

`Create one isolated broad fiery crescent sword-slash for a Unity VFX texture, using the approved image only as style and silhouette reference: compact lower-left ignition, large curved sweep to upper-right, tapered ends, yellow-white inner edge, broad golden-orange body, deep red ragged outer flames and internal filament strokes. Transparent background; no scene, ground, reflection, character, weapon, text, watermark, card edge, black background, flat parallel stripes, or copied reference composition.`

The generated source was then edited twice with built-in image generation: first to obtain genuine RGBA transparency, then to remove detached particles, followed by a second transparency extraction. The final file was verified as RGBA `1254×1254`, alpha extrema `0–255`, transparent corner `(0,0,0,0)`.

### Breakup noise

`Create a square seamless near-grayscale Unity VFX breakup/noise texture with organic turbulent brush fibres, elongated wisps, irregular islands, strong value range and balanced large/medium/fine frequencies. Fill edge-to-edge; no focal slash, objects, colour design, transparency, text, checkerboard, or watermark.`

### Spark atlas

`Create exactly four isolated fiery spark sprites in a 2×2 transparent atlas: compact diamond, narrow four-point star, tapered flame shard and irregular angular ember chip. Yellow-white core, golden-orange middle, red-orange edge; each fully contained and centred in its quadrant; no grid, background, crescent, smoke, extra text, or watermark.`

These textures are inputs, not proof that the final Unity effect matches the reference. Only the actual serialized Game-camera playback may be visually accepted.
