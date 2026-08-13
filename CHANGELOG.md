# 4.0.2.28

### Fixes
- After casting Treasure Sight, Active bronze / Active silver on the Treasure tab should show up reliably (including the optional % display).
- Pot chest farming should path correctly when the chest is on a different floor than you.
- Pot chest farming should use Magical Elixir more reliably (dismounts first, respects the elixir cooldown, and moves on if a spot fails). (#181)
- After a Magic Pot FATE, Illegal Mode should stay to open pot chests instead of Returning or teleporting away too early.
- Dancer now walks up to melee range in Illegal Mode / Completionist so dance Steps and Finish are in range.
- Illegal Mode AI Mode should turn on for FATEs and Critical Encounters, and turn off when they end. (#182)
- Illegal Mode no longer forces its own enemy target in FATEs/CEs — BossMod AI Mode handles targeting so the two don’t fight each other.
- Mob Farmer only auto-targets enemies when “Handle targeting” is enabled (including while pulling packs).
- Treasure Hunt should no longer try to walk through a bad point near Suspended Masonry that made pathfinding fail.
- Treasure Hunt is less likely to skip or turn away from silver coffers before they finish appearing.
- Occult silver/gold per hour should no longer spike hugely just from entering an instance.
- Pot chest farming now finds revealed coffers again — the game reports some of them at a bogus height, which was putting them outside the search range everywhere except near sea level.
- Pot chest farming falls back to the full sweep correctly: reroll pot chests are now included (the "Farm reroll pot chests" setting had no effect in practice), and a few South Horn compass-group positions were up to 39y off.
- The South Horn coffer near the west coast of The Wanderer's Haven is now included in Treasure Hunt routes (it had a bad level and was silently skipped every run).
- Prepositioning for the next Magic Pot no longer gives up early when "Minutes before predicted pot spawn" is set above 5.
- North Horn Treasure Hunt now clears each area in one go instead of bouncing between them — it was re-picking where to resume after every coffer, which re-entered some areas three times per run.
- Treasure Hunt routes reordered against real measured walk distances: about 26% less walking on South Horn and 14% on North Horn. Each area is still visited in the same order and entered at the same pad — only the order of coffers within an area changed.
- Illegal Mode no longer Returns to base camp when you step off the route to a FATE/CE. Walking was only ever considered within 80y of the goal, so once you were further out the only options left were teleport-based and Return won regardless of how close you already were. It now compares walking properly and picks it when it is genuinely quicker.
- Illegal Mode no longer picks a route it cannot walk. Paths vnav fails to build report a distance of 0, which made unreachable routes look free and beat every real option.
- Treasure Hunt and Carrot Hunt no longer spam vnav with repeated path requests while it is still working one out (most noticeable on long Returns to camp).

### Performance
- Treasure Hunt no longer stutters between coffers. It was re-reading and re-parsing a ~6.9 MB route data file from disk every time it recalculated (after every coffer, empty pad, and skip); that data is now loaded once per zone.
- The bundled Treasure Hunt route data shrank from ~6.9 MB to ~175 KB per zone by dropping walk paths that nothing ever read.
- Treasure Hunt does much less work per frame: the "is there a closer coffer" check no longer rescans every object in the zone on every frame, and coffer lookups no longer scan the full pad list.
- Pot chest farming does much less work per frame — it was rescanning every object in the zone up to ten times per frame while hunting chests.
- Illegal Mode is lighter: the FATE and Critical Encounter lists are built in a single pass per frame instead of rescanning themselves for every entry, and reading those lists no longer copies them a dozen times per frame.
- BOCCHI now does almost nothing outside Occult Crescent. The coffer tracker, carrot tracker, mob scanner and FATE list were all scanning every frame everywhere in the game, including cities — the mob scanner ran even with Mob Farmer switched off.
- Critical Encounter lists are built once per frame instead of being rebuilt and copied on every read (the CE handlers alone were doing it twice per frame).
w