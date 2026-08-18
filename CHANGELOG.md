# 4.0.2.29

### New
- Mount, sprint, and unstuck options now live on a Movement page. They apply to Illegal Mode, Treasure Hunt, Carrot Hunt, and Mob Farmer.
- Jump when stuck: if you stop moving on rocks, ledges, or stairs while pathing, BOCCHI jumps to get free. On by default; timing is on the Movement page. (#185)
- Walking to a FATE no longer instantly switches to a Critical Encounter. It waits until registration is almost up (90 seconds left by default; 0 = old behaviour). If you are already in the FATE or fighting it, it finishes first. Prefer pot FATEs still puts Magic Pots ahead of CEs. (#187)
- Illegal Mode → Combat only lists autorotation plugins you have installed.

### Fixes
- Critical Encounter registration uses the real in-game ring size, so it no longer pulls you inward while you are already on the blue ring.
- Aethernet hops skip shards you have not unlocked yet.
- Treasure Hunt can reach the Wanderer's Haven west-coast coffer again. Radar no longer peels onto the Unhallowed Hamlet basement coffer, so the stairs stay intact. (#185)
- Pot chest farming fights and dodges if something aggroes you. After a reroll, the next chest searches the far pads instead of walking the original pot spots again. (#188)
- Auto treasure hunt after FATEs/CEs is on the Illegal Mode page next to Treasure Sight.