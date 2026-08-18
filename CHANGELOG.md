# 4.0.2.29

### New
- Mount, sprint, and unstuck options now live on a Movement page. They apply to Illegal Mode, Treasure Hunt, Carrot Hunt, and Mob Farmer.
- Jump when stuck: if you stop moving on rocks, ledges, or stairs while pathing, BOCCHI jumps to get free. On by default; timing is on the Movement page. (#185)
- Walking to a FATE no longer instantly switches to a Critical Encounter. It waits until registration is almost up (90 seconds left by default; 0 = old behaviour). If you are already in the FATE or fighting it, it finishes first. Prefer pot FATEs still puts Magic Pots ahead of CEs. (#187)
- Illegal Mode → Combat only lists autorotation plugins you have installed.
- Mob Farmer uses your Illegal Mode combat choice only while fighting, not while pulling. When the pack is gathered, autorotation and BOCCHI AI take over movement so you dodge. Tanks can Shield Lob (or the job equivalent), Provoke, and gap-close, and they walk toward the next enemy while the current one is still in range. (#145)
- Mob Farmer pull buffs: Battle Bell, optional Phantom Dancer Quickstep, optional Geomancer Ringing Respite. (#145)
- Mob Farmer farm spots: named origins, optional stack/stop points for caves and SW Tower, and leave a camp if someone else has claimed it. (#145, #155)
- Mob Farmer can pause for Magic Pots, a timed Treasure Hunt when Sight counts are high enough, and knowledge-crystal buffs that are about to expire. (#145)

### Fixes
- Critical Encounter registration uses the real in-game ring size, so it no longer pulls you inward while you are already on the blue ring.
- Aethernet hops skip shards you have not unlocked yet.
- Treasure Hunt can reach the Wanderer's Haven west-coast coffer again. Radar no longer peels onto the Unhallowed Hamlet basement coffer, so the stairs stay intact. (#185)
- Pot chest farming fights and dodges if something aggroes you. After a reroll, the next chest searches the far pads instead of walking the original pot spots again. (#188)
- Experience, gold, and silver per hour are this visit to Occult Crescent. One FATE or CE no longer gets treated as if you could do that for a full hour.
- Dalamud log is quieter during farming, treasure hunt, and pathing. Enable Debug in Dalamud if you need the old traces.