# Cohort F preregistered exact prompts

Each isolated Recipe agent receives the byte-frozen `contract-snapshot.md`, followed by exactly one prompt below and this unchanged instruction: `Return exactly one raw JSON Recipe object and nothing else.` It receives no acceptance specification, prior output, worktree, or tool.

- F1: Create a 2D mobile-medium stylized projectile named `f1_cinder`. Use a compact travel core, a short-lifetime small launch flash, a narrow short-time trail, sparse short-lived embers, and a compact impact burst with a compact shockwave.
- F2: Create a 2D PC-editor stylized projectile named `f2_lantern`. Use a large travel core, a large launch flash, a broad long-time trail, abundant long-lived embers, and a large fast impact burst with a large shockwave.
- F3: Create a 2D mobile-medium stylized projectile named `f3_needle`. Use a small travel core and a small short-lifetime launch flash. Do not include a trail or embers. Use a compact impact burst and a compact shockwave.
- F4: Create a 2D PC-editor stylized projectile named `f4_whisper`. Use a compact travel core, a narrow trail, no embers, and a restrained impact: compact burst, slower-than-default burst speed, and compact shockwave.
- F5: Create a 2D mobile-medium stylized projectile named `f5_flare`. Use a medium-or-larger travel core, a normal-or-larger launch flash, a broad trail, normal-or-more embers, and a large burst plus shockwave impact.

Patch agents receive the same frozen contract, then the exact isolated baseline Recipe at revision 1, then the indicated prompt and this unchanged instruction: `Return exactly one raw JSON Patch operation array and nothing else.`

- P1: In the supplied isolated Recipe at expected revision 1, replace travel embers rate with 9.
- P2: In the supplied isolated Recipe at expected revision 1, disable travel embers without removing it.
- P3: In the supplied isolated Recipe at expected revision 1, add a travel module with ID `lighter_embers`, attached to `core`, using the formal Embers template and all required parameters.

For every repair, the persisted `repairN.prompt.md` is sent byte-for-byte. It consists only of the fixed instruction `Your previous JSON was rejected. Return a corrected raw JSON object only; do not explain it. Here is the complete raw Validator/Build report:` followed by the complete immediately preceding `.report.json` text.
