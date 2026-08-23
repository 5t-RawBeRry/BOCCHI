# 4.1.0.9

### Features
- **Repair method** (Illegal Mode → Repair): self-repair, mender NPC at base camp, or prefer mender when nearby. Mender needs no crafter levels (same flow as Artisan / AutoDuty).

### Fixes
- **BOCCHI AI presets:** with **Update presets automatically** on, changing distance / overdodge / delay now rebuilds the presets right away — no need to toggle Illegal Mode off and on.
- **Cursed Concern** (and similar CEs): riding past or leaving the CE no longer leaves RSR / BOCCHI AI CE on, targeting trash while mounted (#200).
- **Illegal Mode + auto treasure hunt:** Return to camp no longer cancels a map hunt that just started after a FATE/CE (common below Freelancer 10 without Treasure Sight).
- **Illegal Mode idle softlock:** when the map treasure hunt pauses for a FATE/CE, BOCCHI can Return / refresh buffs / pick the activity again instead of standing still.
- **Applying buffs softlock:** if knowledge crystals disappear mid-buff (shows **No Crystals Found**), Illegal Mode aborts and continues instead of freezing without mounting.
- **Buff walk to crystal:** mounts while approaching a knowledge crystal when auto-mount is on.
- **Aethernet approach:** mounts while walking to a shard for teleport (e.g. after pot chests → next CE), not sprint-only on foot.
- **Cursed Resurgence** (North Horn): activity area is a square again, so pathing and “in CE” checks match the real zone.

### Improvements
- Config tooltips cleaned up (clearer On/Off wording, fewer redundant tips). Cast Treasure Sight explains when it’s unused because auto treasure hunt is on.
- **Carrot Hunt:** **Ninja Hide near strong enemies** (and the related Ninja Hide options) now apply to Carrot Hunt the same as Treasure Hunt.
- Treasure Hunter settings grouped into **When hunt ends**, **Carrot Hunt**, **Treasure Hunt**, and **Ninja Hide**.
