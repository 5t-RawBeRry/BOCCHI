# 4.0.2.12

### Fixes
- Treasure Hunt: visit the full remaining route (wrap the tour) instead of dropping coffers before the start node
- Treasure Hunt: divert to a live coffer next to you instead of walking past it toward a far target
- Treasure Hunt: match/path to live spawns when layout points have drifted; stuck recovery uses progress toward the chest (not absolute movement)
- Illegal Mode: after a CE goes live, keep walking into the combat ring if still outside (was freezing with no targets yet — “take to the field” / #140 follow-up)
- Illegal Mode: tighter wait/approach for A Beast Unleashed so we enter the blue registration box (other CEs keep the normal red-ring wait)

### Features
- Treasure: opt-in anonymous pot timer sync so other BOCCHI users on the same instance can predict the next Magic Pot
- Pots & Treasure: use the normal Treasure Hunt max-level setting for filler hunts (#141)
