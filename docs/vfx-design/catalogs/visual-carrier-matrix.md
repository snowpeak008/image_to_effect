# Visual carrier matrix

| Intended meaning | Preferred carrier | Required trace fact | Disallowed shortcut |
|---|---|---|---|
| Sustained fire | Independent particle systems + procedural/atlas shader + smoke/embers + bounded Light | start/steady/stop/interrupt states, particle and Light cleanup | looping one full-image flame |
| Independent fragments | Fragment Motion System or individual particle instances | unique instances with non-correlated angular motion | common parent / whole texture rotation |
| Moving projectile trail | Moving emitter + TrailRenderer / particle trails | emitter position history and motion-derived vertices | static LineRenderer or pre-painted trail |
| Model attachment | Transform/socket/bone/mesh adapter | binding target and anchor error | camera-facing card pretending to attach |
| Real illumination | Light/Light2D + receiver surface | Light telemetry and receiver linear-luminance A/B | additive material only |
| Ground residual | Decal / mesh / controlled particle residual | cleanup exemption and timed removal | undeclared permanent sprite |
| Beam | LineRenderer / mesh ribbon / particle ribbon | endpoint protocol and occlusion trace | stretched static image |
| Screen UI | UI/Canvas carrier | UI hierarchy and coordinate-space trace | world-space billboard under a UI name |

The matrix selects a carrier for semantic intent; it is not a style catalog and never replaces human visual review.
