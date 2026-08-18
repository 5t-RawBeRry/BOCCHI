# 4.0.2.29

### New
- Mount, sprint, and unstuck options now live on a Movement page. They apply to Illegal Mode, Treasure Hunt, Carrot Hunt, and Mob Farmer — they used to sit under Illegal Mode where they were easy to miss.
- Jump when stuck: if you stop moving on rocks, ledges, or stairs while pathing, BOCCHI jumps to get free. On by default; timing is on the Movement page. (#185)
- Illegal Mode → Combat only lists autorotation plugins you have installed.

### Fixes
- Illegal Mode uses the real Critical Encounter registration size from the zone instead of a flat 20y / 15y, so it no longer pulls you inward while you are already on the blue ring.
- Illegal Mode, World Path, Treasure Hunt, and Carrot Hunt only aethernet to shards you have unlocked. Locked pads are skipped instead of trying Lifestream there.
- Treasure Hunt can reach the Wanderer's Haven west-coast coffer again (the jump clears that ledge). Radar no longer peels onto the Unhallowed Hamlet basement coffer and cuts off the stairs — that pad is still visited in route order. (#185)
- Pot chest farming fights and dodges if something aggroes you, so the magic pot is less likely to die. A second-chance chest now searches the far reroll pads instead of walking the original pot spots again. (#188)
- Auto treasure hunt after FATEs/CEs is on the Illegal Mode page next to Treasure Sight, not the Treasure Hunter page.
