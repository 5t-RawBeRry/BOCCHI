# 4.0.2.28

### New
- Illegal Mode combat can use Wrath Combo or Rotation Solver Reborn with BOCCHI AI, or a full BossMod / BossMod Reborn autorotation. Pick one under Illegal Mode → General → Combat. It turns off while traveling.
- Skip FATEs at or above a progress % is on the FATEs page and applies to every FATE, not only Magic Pots. Your old pot-timing value is kept.

### Fixes
- Treasure Hunt is more reliable and walks less: Sight counts after casting, fewer skipped silvers, North Horn no longer bounces between areas, South Horn uses area routes instead of red and blue halves, and Returns / aethernet hops actually fire.
- The Wanderer's Haven west-coast coffer stays out of the South Horn route. It sits on a ledge that can only be reached with a jump, which BOCCHI cannot do, so including it just stalled the hunt for half a minute near the start of every run.
- Carrot Hunt no longer mount-spams on tall climbs, skips a pad if it stays stuck, and clears North Horn in a better order.
- Prefer pot FATEs always does Magic Pots first and no longer turns on Wait near pots or Farm pot chests. Wait near pots works on its own. Pot chest farming paths between floors, finds revealed and reroll chests, and no longer gives up early on a long wait. (#181)
- Pot chests are found and opened. The revealed coffer is a different kind of object than an ordinary treasure chest, and BOCCHI only ever looked for ordinary ones — so the chest it had just correctly located was invisible to it, and it stood there until the search timed out. It now recognises the real thing, dismounts, and opens it.
- Pot chest hunting no longer abandons the chest at the moment it finds it. Finding the chest is what removes Cache Me If You Can, and losing that buff was treated as "farm over". It now opens the coffer first, and stays long enough to pick up a reroll if one is offered.
- Pot chest hunting walks to the right spot when a new compass reading changes its mind mid-route. It used to keep walking to the old spot and then discard the new one as unreachable.
- Pot chest hunting uses Return and aethernet teleports between search spots. A single pot FATE's chest spots are spread over more than 1600 yalms, so walking between them was eating most of the Cache Me If You Can window. Short hops still just walk.
- Travel decides between walking, aethernet hops and Return using one shared set of costs. Illegal Mode travel and the treasure survey were pricing an aethernet hop at a fifth of what the treasure and carrot hunts used, so they reached for teleports on trips they could comfortably walk. A hop is now priced the same everywhere, at roughly what one actually takes.
- Pot chest hunting no longer calls a spot unreachable while it is still walking there. It now waits for the pathfinder to actually give up, which also spots genuinely unreachable spots faster.
- Illegal Mode AI turns on for FATEs and Critical Encounters, and dodges in FATEs again. BOCCHI was re-activating its BossMod preset every frame, which restarted the AI's movement decision before it could run — so it neither closed on the mobs nor evaded. The preset is activated once per activity now, and the AI walks melee into range itself. Illegal Mode also no longer Returns when walking is quicker, and Dancer and Sage close to melee range. (#182)
- Illegal Mode locks Wrath Combo to the recommended settings while it is running, and stopping no longer errors with Wrath selected.
- The random wait before Return after a FATE/CE works again. Mob Farmer only auto-targets when “Handle targeting” is on. Ninja Hide uses your Knowledge offset and range.
- Setting and Dependencies copy is shorter. Dependencies now says Ready / Not enabled / Not installed, and shows which combat plugins your Combat pick uses.
- Occult silver/gold per hour should no longer spike just from entering an instance.

### Performance
- Treasure Hunt should no longer hitch between coffers. Route data is smaller and loads once. Outside Occult Crescent, BOCCHI does almost nothing.
