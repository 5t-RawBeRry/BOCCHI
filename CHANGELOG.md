# 4.0.2.28

### New
- Illegal Mode combat can use Wrath Combo or Rotation Solver Reborn together with BOCCHI AI, or a full BossMod / BossMod Reborn autorotation. Pick one under Illegal Mode → General → Combat. It turns off while traveling.

### Fixes
- Prefer pot FATEs always does Magic Pots and picks them first. On the FATEs tab those pots stay checked and show as required. It no longer turns on Wait near pots or Farm pot chests, and Farm pot chests only runs pots that are on your list.
- Treasure Hunt is more reliable: Sight counts show up after casting, silvers are less likely to be skipped, the Wanderer's Haven west-coast coffer is included, North Horn no longer bounces between areas, and a bad path near Suspended Masonry is avoided.
- Treasure Hunt walks less. Routes use measured distances, Returns and aethernet hops between areas actually fire, and a Return hops to the nearest shard instead of walking from camp. Hunt and Carrot Hunt no longer ask for a new path while one is still calculating.
- South Horn Treasure Hunt uses area routes instead of the old red and blue halves. Consecutive runs start in a different area, nearby coffers can be picked up, and a lower “Max level” stops at a nearby area instead of crossing the map.
- Pot chest farming paths between floors, finds revealed coffers again, includes reroll chests, uses Magical Elixir more reliably, and stays after a Magic Pot FATE. Waiting near the next pot no longer gives up early when “Minutes before predicted pot spawn” is above 5. (#181)
- Dancer and Sage now walk up to melee range in Illegal Mode / Completionist so dance Steps / Finish and Sage’s 6-yalm skills connect.
- Illegal Mode AI turns on for FATEs and Critical Encounters, and off when they end. Dodging in FATEs works without turning the mode off. (#182)
- Illegal Mode no longer Returns to camp when walking to a FATE/CE is quicker, and no longer picks a route it cannot walk.
- Mob Farmer only auto-targets when “Handle targeting” is enabled, and no longer hitch-cancels movement on every pull skill.
- Ninja Hide uses your Knowledge offset and yalm range (mounted Hide no longer starts a long way early).
- Carrot Hunt no longer mount-spams on tall climbs, and skips a pad if it stays stuck after a few recoveries. North Horn now clears the center first, then the west ridge, then northwest and northeast, so the death-zone stretch is one block.
- Occult silver/gold per hour should no longer spike just from entering an instance.
- Setting and main-window descriptions are shorter and easier to read.
- The Dependencies page now says Ready / Not enabled / Not installed instead of Loaded and Linked, and shows which combat plugins your General → Combat pick uses.
- Stopping Illegal Mode no longer errors when Combat is set to Wrath Combo.

### Performance
- Treasure Hunt should no longer hitch between coffers. Route data loads once per zone, and the bundled files are much smaller.
- Treasure Hunt, pot chest farming, and Illegal Mode do less work each frame. Outside Occult Crescent, BOCCHI now does almost nothing.
