# 4.0.2.24

### Fixes
- Carrot Hunt clears nearby spots (caves, citadel clusters) before hopping across the map, and is less likely to walk past a chewed carrot that’s already close.
- Carrot Hunt tries to unstick itself on ramps/walls instead of freezing until you nudge it.
- Carrot Hunt mounts up when leaving base camp for a long walk (e.g. heading south without a teleporter).
- Pots & Treasure now respects **Leave for pots this many minutes early** under Pot timing (it was stuck at 3 minutes before).
- Ninja Hide stops and dismounts earlier when you’re mounted, instead of riding through enemies before Hide can go off.
- Waiting near an aetheryte (e.g. for the next pot) no longer pathfinds into the sky and spam vnavmesh errors.
- Pot chests that show up underground (Y ≈ -500) are found and opened again — BOCCHI walks up on the real ground, dismounts, and interacts (#170).
- Long trips to CEs/FATEs (e.g. Lost on the Wind) use Return / aethernet again instead of walking the whole map when you’re already near the “closest” shard (#172).
- North Horn Treasure Hunt uses Return / aethernet between map regions again instead of walking the whole authored route (#169).
- Illegal Mode stops pathing to a CE once it leaves Preparing, unless you already registered or were waiting there.
- Next-pot timer no longer sticks at 00:00 after a missed spawn — the cycle rolls forward, and Waiting for pot stops re-arming on stale predictions.
- With Prefer pot FATEs / farm pot chests on, Illegal Mode leaves a non-pot FATE (while still traveling) when a pot is up.
