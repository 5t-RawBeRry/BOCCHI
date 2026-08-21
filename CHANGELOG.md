# 4.1.0.6

### Features
- Treasure Hunt: choose how close to walk before skipping an empty coffer pad (default 25 yalms).

### Fixes
- Wrath no longer spams Invalid lease by re-arming the job every combat frame.
- Illegal Mode auto treasure hunt only starts when bronze/silver fill meets the Treasure page thresholds (same as Mob Farmer). Without Treasure Sight it still uses the built-in map.
- Illegal Mode no longer stops early on the way to Eternal Watch (and similar CEs) thinking it already arrived.
- Clearer Illegal Mode / Treasure / Mob Farmer labels and tooltips (path map status, auto-hunt thresholds, BossMod distance, and related copy).
- BOCCHI AI presets use 5y for Sage and 10y for Dancer instead of melee OnHitbox when distance-by-role is on.
- Loop Carrot Hunt (Treasure Hunter): after each find, recheck every pad; keep going on empty passes until Stop or out of carrots.
