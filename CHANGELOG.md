# 4.0.2.24

### New
- Updated Korean translations.

### Fixes
- Carrot Hunt clears nearby spots (caves and citadel clusters) before crossing the map, and is less likely to walk past a chewed carrot that’s already close.
- Carrot Hunt tries to free itself on ramps and walls instead of freezing until you nudge it.
- Carrot Hunt mounts up when leaving base camp for a long walk (for example heading south without a teleporter).
- Pots & Treasure now respects **Leave for pots this many minutes early** under Pot timing (it was stuck at 3 minutes).
- Ninja Hide stops and dismounts earlier when you’re mounted, so it can actually Hide before riding past enemies.
- Waiting near an aetheryte (for example for the next pot) no longer tries to path into the sky.
- Pot chests are opened again after they’re found — including cases where the game reports them underground (#170).
- Long trips to CEs and FATEs (for example Lost on the Wind) use Return and aethernet again instead of walking the whole map (#172).
- North Horn Treasure Hunt uses Return and aethernet between map regions again instead of walking the whole route (#169).
- Illegal Mode stops running to a CE once it has already started, unless you already registered or were waiting there.
- The next-pot timer no longer sticks at 00:00 after a missed spawn, and Waiting for pot no longer loops on an old prediction.
- With Prefer pot FATEs or farm pot chests on, Illegal Mode leaves a normal FATE (while still traveling) when a pot is up.
