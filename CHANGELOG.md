# 4.0.2.28

### Fixes
- After casting Treasure Sight, Active bronze / Active silver on the Treasure tab should show up reliably (including the optional % display).
- Pot chest farming should path correctly when the chest is on a different floor than you.
- Pot chest farming should use Magical Elixir more reliably (dismounts first, respects the elixir cooldown, and moves on if a spot fails). (#181)
- After a Magic Pot FATE, Illegal Mode should stay to open pot chests instead of Returning or teleporting away too early.
- Dancer now walks up to melee range in Illegal Mode / Completionist so dance Steps and Finish are in range.
- Illegal Mode AI Mode should turn on for FATEs and Critical Encounters, and turn off when they end. (#182)
- Illegal Mode no longer forces its own enemy target in FATEs/CEs — BossMod AI Mode handles targeting so the two don’t fight each other.
- Treasure Hunt should no longer try to walk through a bad point near Suspended Masonry that made pathfinding fail.
- Treasure Hunt is less likely to skip or turn away from silver coffers before they finish appearing.
- Occult silver/gold per hour should no longer spike hugely just from entering an instance.
- Pot chest farming now finds revealed coffers again — the game reports some of them at a bogus height, which was putting them outside the search range everywhere except near sea level.
- Pot chest farming falls back to the full sweep correctly: reroll pot chests are now included (the "Farm reroll pot chests" setting had no effect in practice), and a few South Horn compass-group positions were up to 39y off.
- The South Horn coffer near the west coast of The Wanderer's Haven is now included in Treasure Hunt routes (it had a bad level and was silently skipped every run).
- Prepositioning for the next Magic Pot no longer gives up early when "Minutes before predicted pot spawn" is set above 5.
