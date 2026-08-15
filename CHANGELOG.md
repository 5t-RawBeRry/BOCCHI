# 4.0.2.28

### New
- Illegal Mode combat can now use Wrath Combo or Rotation Solver together with BOCCHI AI, or a full BossMod / BossMod Reborn autorotation. Pick one under General → Combat. It turns off while traveling.

### Fixes
- Treasure Hunt is more reliable: Sight counts show up after casting, silvers are less likely to be skipped, the Wanderer's Haven west-coast coffer is included, North Horn no longer bounces between areas, and a bad path near Suspended Masonry is avoided.
- Treasure Hunt routes walk a lot less. Coffer order was rebuilt from measured walk distances, and a Return picks the nearest shard to the next area rather than walking from camp. Hunt and Carrot Hunt no longer ask for a new path while one is still calculating.
- South Horn is now divided into seven area routes (base camp, Wanderer's Haven, Crystallized Caverns, Eldergrowth, Stonemarsh) instead of the red and blue halves, which criss-crossed the whole map. Areas are ordered by coffer level, so a lower "Max level" setting stops at a nearby area instead of zig-zagging past it — roughly a quarter less walking at level 15, and 5% less on a full run.
- Each South Horn hunt now opens on the next area in turn rather than alternating red/blue, so consecutive runs never start in the same place and every area gets a turn at being cleared first.
- The Return and aethernet hop between areas no longer goes missing. It was attached to the last coffer of an area, so it was skipped whenever that coffer was already looted or above your "Max level" — at level 25 that silently dropped four of North Horn's five area hops and made the hunt walk between areas instead. The hop now belongs to the area boundary itself.
- Areas that are cheaper to hop to are no longer walked to. North Horn walked base camp to the Crown of Karnak the long way round when the aethernet was less than half the distance.
- South Horn picks up coffers that appear next to the route again. It previously followed the route coffer-by-coffer with no detours at all; detours are now allowed inside the current area, the same as North Horn.
- Pot chest farming paths between floors, finds revealed coffers again, includes reroll chests, uses Magical Elixir more reliably, and stays after a Magic Pot FATE instead of leaving early. (#181)
- Prepositioning for the next Magic Pot no longer gives up early when “Minutes before predicted pot spawn” is above 5.
- Dancer and Sage now walk up to melee range in Illegal Mode / Completionist so dance Steps / Finish and Sage’s 6-yalm skills connect.
- Illegal Mode AI turns on for FATEs and Critical Encounters, and off when they end. Dodging in FATEs works without turning the mode off. (#182)
- Illegal Mode no longer Returns to camp when walking to a FATE/CE is quicker, and no longer picks a route it cannot walk.
- Mob Farmer only auto-targets when “Handle targeting” is enabled, and no longer hitch-cancels movement on every pull skill.
- Ninja Hide uses your Knowledge offset and yalm range (mounted Hide no longer starts a long way early).
- Carrot Hunt no longer mount-spams on tall climbs, and skips a pad if it stays stuck after a few recoveries. North Horn now clears the center first, then northwest, then northeast, so the death-zone stretch is one block.
- Occult silver/gold per hour should no longer spike just from entering an instance.

### Performance
- Treasure Hunt should no longer hitch between coffers. Route data loads once per zone, and the bundled files are much smaller.
- Treasure Hunt, pot chest farming, and Illegal Mode do less work each frame. Outside Occult Crescent, BOCCHI now does almost nothing.
