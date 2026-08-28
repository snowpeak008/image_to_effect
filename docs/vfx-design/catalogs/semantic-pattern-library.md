# Semantic pattern library

| Pattern | States / causal rule | Required continuity |
|---|---|---|
| sustained | start → steady → stop or interrupt → clear | one energy/layer/parameter continues from start into steady; bounded stop cleanup |
| projectile trail | emit → move → hit → residual → recycle | trail vertices derive from emitter history; recycle clears prior path |
| fragment burst | charge → release → independent fragment flight → settle | each fragment has own motion seed/instance; no parent-only rotation |
| charge release | low charge → staged charge → release → dissipate | charge metric monotonically changes until release |
| chain | source hit → hop n → hop n+1 → terminate | each hop endpoint is the next cause |
| model attachment | attach → follow / react → detach | anchor remains tied to the declared model target across views |
| real light | off → enabled A/B → fade → off | receiver luminance changes outside additive effect mask |

If an effect does not fit a pattern, write a new pattern before implementation. Do not silently map it to a convenient sprite animation.
