# 4.0.2.12

### Fixes
- Treasure Hunt: visit the full remaining route (wrap the tour) instead of dropping coffers before the start node
- Treasure Hunt: divert to a live coffer next to you instead of walking past it toward a far target
- Treasure Hunt: match/path to live spawns when layout points have drifted; stuck recovery uses progress toward the chest (not absolute movement)
- Illegal Mode: pathfind better in blue area for CE's. (if it still misbehaves, let me know WHICH CE)
- World panel: Path uses best aethernet then walks; Teleport only hops aethernet (no walk)

### Features
- Treasure: opt-in anonymous pot timer sync so other BOCCHI users on the same instance can predict the next Magic Pot
- Pots & Treasure: use the normal Treasure Hunt max-level setting for filler hunts (#141)
- Main window: Buffs control on its own row with a labeled Apply/Stop button (flask icon) instead of a cryptic wand next to status chips
