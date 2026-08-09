# 4.0.2.19

### New
- Start Carrot Hunt is always on the Treasure panel (no separate settings page or enable toggle).
- Settings are grouped under small titles (like General, Trackers, and Hunt) so related options are easier to find.
- Treasure Hunt rebuilds your route from where you are after Treasure Sight, after each coffer, and when empty spots are skipped.
- Treasure Hunt: if you get stuck on terrain, BOCCHI steps aside before skipping that coffer (#156).
- Carrot Hunt can use aethernet shards when that is faster than walking straight.

### Fixes
- Illegal Mode can still start an auto treasure hunt without Treasure Sight (uses the built-in coffer map).
- Buffs and UI options appear in a clearer order again.
- Under Automate buffs, the individual buff choices sit underneath and stay greyed out until automation is on.
- Demiatma, field note, and soul shard icons are toggled in UI settings (Event drops is no longer a separate page).
- Experience and currency history length / graph step size are in UI settings (Tracker is no longer a separate page).
- Removed unused range settings (open range, carrot match range, prefer aethernet, crystal search range); BOCCHI uses fixed sensible distances.
- Treasure Hunt skips empty coffer spots once you are close enough that the coffer should be visible.
- South Horn’s coffer map now matches North Horn’s style, so hard-to-reach spots are included.
- Default hunt max level is 50, so North Horn’s full coffer set is included.
- Carrot Hunt on South and North Horn follows the map and picks the nearest next pad (no more waiting only for nearby carrots).
- North Horn carrot pads use knowledge levels based on nearby coffers (they were all treated as level 1 before).
