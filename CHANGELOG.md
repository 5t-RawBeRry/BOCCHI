# 4.0.2.28

### Fixes
- After casting Treasure Sight, Active bronze / Active silver on the Treasure tab should show up reliably (including the optional % display).
- Treasure Hunt should no longer try to walk through a bad point near Suspended Masonry that made pathfinding fail.
- Treasure Hunt is less likely to skip or turn away from silver coffers before they finish appearing.
- The South Horn coffer near the west coast of The Wanderer's Haven is now included in Treasure Hunt routes.
- North Horn Treasure Hunt now clears each area in one go instead of bouncing between them.
- Treasure Hunt routes walk less: about 26% on South Horn and 14% on North Horn. Each area is still visited in the same order and entered at the same pad — only the order of coffers within an area changed.
- Treasure Hunt and Carrot Hunt no longer keep asking for a new path while one is still being calculated (most noticeable on long Returns to camp).
- Pot chest farming should path correctly when the chest is on a different floor than you.
- Pot chest farming should use Magical Elixir more reliably (dismounts first, respects the elixir cooldown, and moves on if a spot fails). (#181)
- After a Magic Pot FATE, Illegal Mode should stay to open pot chests instead of leaving too early.
- Pot chest farming now finds revealed coffers again — the game reports some of them at a bogus height, which hid them everywhere except near sea level.
- Pot chest farming falls back to the full sweep correctly: reroll pot chests are now included (the "Farm reroll pot chests" setting had no effect), and a few South Horn compass-group positions were off.
- Prepositioning for the next Magic Pot no longer gives up early when "Minutes before predicted pot spawn" is set above 5.
- Dancer now walks up to melee range in Illegal Mode / Completionist so dance Steps and Finish are in range.
- Illegal Mode AI Mode should turn on for FATEs and Critical Encounters, and turn off when they end. (#182)
- Dodging works again in FATEs. Illegal Mode was cancelling the AI's movement every frame, so BOCCHI AI FATE could never finish a dodge — you had to switch Illegal Mode off for it to work.
- Turning Illegal Mode on mid-fight now arms the right preset instead of leaving the AI off until the fight ends.
- Using an action in a FATE or Critical Encounter no longer cancels the AI's movement. This only used to be handled for the activity Illegal Mode had picked itself, so walking into any other FATE/CE broke dodging on every action.
- Illegal Mode no longer Returns to base camp when you step off the route to a FATE/CE, if walking there is actually quicker.
- Illegal Mode no longer picks a route it cannot walk.
- Mob Farmer only auto-targets enemies when “Handle targeting” is enabled (including while pulling packs).
- Occult silver/gold per hour should no longer spike just from entering an instance.

### Performance
- Treasure Hunt should no longer hitch between coffers. Route data is loaded once per zone instead of after every coffer, and the bundled files are much smaller.
- Treasure Hunt, pot chest farming, and Illegal Mode do less work each frame.
- BOCCHI now does almost nothing outside Occult Crescent. It was still scanning in cities, including with Mob Farmer switched off.
