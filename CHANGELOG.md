# 4.0.2.12

### Fixes
- Treasure Hunt: visit the full remaining route (wrap the tour) instead of dropping coffers before the start node
- Treasure Hunt: divert to a live coffer next to you instead of walking past it toward a far target
- Treasure Hunt: match/path to live spawns when layout points have drifted; stuck recovery uses progress toward the chest (not absolute movement)
- Illegal Mode: once near a preparing CE (on/near blue), Waiting holds with no vnav — travel delivers you, then stop (no walk toward center / no “take to the field” into red)
- Illegal Mode: do not path into a CE that is already in Battle if you are not participating (missed registration)
- Illegal Mode: “In CE” only while participating in Battle — Register/Warmup at base no longer counts as in the CE

### Features
- Treasure: opt-in anonymous pot timer sync so other BOCCHI users on the same instance can predict the next Magic Pot
- Pots & Treasure: use the normal Treasure Hunt max-level setting for filler hunts (#141)
- Main window: Buffs control on its own row with a labeled Apply/Stop button (flask icon) instead of a cryptic wand next to status chips
