# 4.0.2.28

### New
- Illegal Mode combat can use Wrath Combo or Rotation Solver Reborn with BOCCHI AI, or a full BossMod / BossMod Reborn autorotation. Pick one under Illegal Mode → General → Combat. It turns off while traveling.
- Skip FATEs at or above a progress % is on the FATEs page and applies to every FATE, not only Magic Pots. Your old pot-timing value is kept.

### Fixes
- Treasure Hunt is more reliable and walks less: Sight counts after casting, fewer skipped silvers, the Wanderer's Haven west-coast coffer is included, North Horn no longer bounces between areas, South Horn uses area routes instead of red and blue halves, and Returns / aethernet hops actually fire.
- Carrot Hunt no longer mount-spams on tall climbs, skips a pad if it stays stuck, and clears North Horn in a better order.
- Prefer pot FATEs always does Magic Pots first and no longer turns on Wait near pots or Farm pot chests. Wait near pots works on its own. Pot chest farming paths between floors, finds revealed and reroll chests, and no longer gives up early on a long wait. (#181)
- Illegal Mode AI turns on for FATEs and Critical Encounters (FATE dodging included), walks up to the mobs instead of standing at the ring, and no longer Returns when walking is quicker. Dancer and Sage close to melee range. (#182)
- Illegal Mode locks Wrath Combo to the recommended settings while it is running, and stopping no longer errors with Wrath selected.
- The random wait before Return after a FATE/CE works again. Mob Farmer only auto-targets when “Handle targeting” is on. Ninja Hide uses your Knowledge offset and range.
- Setting and Dependencies copy is shorter. Dependencies now says Ready / Not enabled / Not installed, and shows which combat plugins your Combat pick uses.
- Occult silver/gold per hour should no longer spike just from entering an instance.

### Performance
- Treasure Hunt should no longer hitch between coffers. Route data is smaller and loads once. Outside Occult Crescent, BOCCHI does almost nothing.
