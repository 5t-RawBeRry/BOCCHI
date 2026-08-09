### New
- Carrot Hunt: full South and North Horn tours on the authored map (nearest next), not nearby-only waiting.
- Carrot Hunt can use aethernet shards when that is faster than walking straight.
- Start Carrot Hunt is always on the Treasure panel (ready to run; no enable toggle).
- Treasure Hunt and Carrot Hunt start buttons sit on one row.
- Treasure Hunt rebuilds your route from where you are after Treasure Sight, after each coffer, and when empty spots are skipped.
- Treasure Hunt: if you get stuck on terrain, BOCCHI steps aside before skipping that coffer (#156).
- Settings pages use small titled sections so related options are easier to find.

### Fixes
- Pot chest farming opens Magic Pot coffers by object id, so overlapping bronze/silver coffers are not opened by mistake.
- Prefer pot FATEs / Farm pot chests actually run Magic Pot FATEs (they no longer stay skipped under Allowed FATEs after reload).
- North Horn’s A Beast Unleashed paths into the blue square instead of stopping outside (authored center; ignore far-off markers).
- “Use BOCCHI AI” and Dependencies text clarify it only turns on the BossMod / BMR autorotation preset (targeting and movement), not a job rotation or the old ai:on command.
- After a Magic Pot chest appears, BOCCHI opens it before Returning for Treasure Sight or camp.
- Illegal Mode can still start an auto treasure hunt without Treasure Sight (uses the built-in coffer map).
- Treasure Hunt skips empty coffer spots once you are close enough that the coffer should be visible.
- South Horn’s coffer map now matches North Horn’s style, so hard-to-reach spots are included.
- Default hunt max level is 50, so North Horn’s full coffer set is included.
- North Horn carrot pads use knowledge levels based on nearby coffers (they were all treated as level 1 before).
- Buffs and UI options appear in a clearer order again.
- Under Automate buffs, the individual buff choices sit underneath and stay greyed out until automation is on.
- Reward icons and tracker history options live under UI settings (fewer separate settings pages).
- Removed unused range settings; BOCCHI uses fixed sensible distances.
