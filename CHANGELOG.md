# 4.2.0.13

### Illegal Mode
- Treasure hunt stops for a pot that is already up, even if Pause for FATEs is off or the shared pot timer was cleared
- Camp buffs start while you are already on Freelancer instead of waiting at the crystal forever. Inquiring Mind if you have it; otherwise each buff on its own job
- If Return is still on cooldown from outside the island, walk to a shard and teleport instead of standing still
- After a FATE or CE, raise nearby players before auto treasure hunt continues
- Stop after return and teleport no longer starts treasure hunt or Returns to camp until you toggle Illegal Mode

### Combat
- Dodging or walking out of a FATE no longer turns combat off and waits for the FATE to end

### Treasure Hunt
- Shared chest maps ignore fake underground spots so the hunt does not walk into the floor
- North Horn high islands: if the next chest is far below, Return and take the aethernet down instead of standing at the edge. A pathing stop that is still far away is no longer skipped.

### Buffs
- Camp buffs no longer spend Inquiring Mind tries while you cannot cast, so they do not fall back to applying each buff on a different job (~40 seconds)

### Pot chests
- With Use Ninja Hide on, pot pads no longer remount (which cancelled Hide) or skip the pad as stuck while Hide is going up
- North Horn — In a Pot of Bother: pad at ~4.8, 9.7 is actually ~4.8, 10.1. Added a high-island pad at ~5.9, 35.2
- South Horn — Pleading Pots: pad at ~22.9, 16.7 is the tree/house chest at ~22.4, 17.0
- Shared pot timers retry for a few minutes if nobody had uploaded yet when you zoned in
