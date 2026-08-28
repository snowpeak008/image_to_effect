# Exact E natural-language prompt suffixes

E1: Create a 2D PC-editor stylized projectile named `e1_comet`: small travel core, short-lifetime launch flash, slim short-time trail, sparse embers, compact impact.

E2: Create a 2D mobile-medium stylized projectile named `e2_bolt`: large travel core, broad long-time trail, abundant long-lived embers, strong impact burst.

E3: Create a 2D PC-editor stylized projectile named `e3_spark`: small travel core, no trail, compact burst and shockwave impact.

E4: Create a 2D mobile-medium stylized projectile named `e4_ribbon`: medium travel core, narrow trail, no embers, compact impact.

E5: Create a 2D PC-editor stylized projectile named `e5_nova`: medium-or-larger travel core, broad trail, normal-or-more embers, large burst and shockwave impact.

Each wrapper is the immutable contract snapshot followed by one suffix, then exactly: `Return exactly one raw JSON Recipe object and nothing else.`

P1: Using the supplied isolated baseline Recipe at expected revision 1, replace travel embers rate with 9. Return exactly one raw JSON operation array and nothing else.

P2: Using the supplied isolated baseline Recipe at expected revision 1, disable travel embers without removing it. Return exactly one raw JSON operation array and nothing else.

P3: Using the supplied isolated baseline Recipe at expected revision 1, add module ID `lighter_embers` to travel, attached to `core`, using the formal Embers template. Return exactly one raw JSON operation array and nothing else.
