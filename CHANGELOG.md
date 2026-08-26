# 4.1.0.11

### Fixes
- **Illegal Mode map hunt** (Freelancer below Treasure Sight): after a FATE or CE far from where you paused, continues from nearby remaining coffers instead of walking back across the zone.
- **Base camp aetheryte**: stand-off uses the footpad height (not crystal Y), Lifestream-ready includes a short pathfind slack, and approach no longer re-paths every tick when already at the ring.
- **Treasure Hunt empty pads**: empty-skip respects **Empty pad check distance** again while walking in (a recent change only skipped once you were ~2y away).
- **Eternal Watch (South Horn CE)**: pathfind no longer targets random off-mesh points on the low platform after Eldergrowth — snaps the wait ring onto navmesh, keeps a stable approach per CE, and walks the ramp via before the final leg.
- **Ninja Hide (Treasure / Carrot Hunt)**: no longer treats South Horn sync level as your Knowledge when actual Knowledge is unavailable; skips Hide instead of dismounting for mobs that would not aggro you; restores your gearset before opening a coffer; no longer stalls forever when Hide is on but gearset is 0 and you are not on Ninja.
- **Mob Farmer**: farm Sight tooltip now correctly says Freelancer **10+** for Treasure Sight.

### Enhancements
- **Mob Farmer**: **Cast Treasure Sight at the farm spot** — pause on a timer at your pull spot, cast Sight, and resume without leaving for a full Treasure Hunt (uses the same bronze/silver fill % as Treasure Hunter).
- **Settings**: clearer hover help on Mob Farmer, Treasure Hunter, Movement, Buffs, and Pot timing.