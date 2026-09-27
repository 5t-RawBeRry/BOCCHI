# 4.2.0.15

### Illegal Mode
- Path planning failures no longer freeze Illegal Mode until you emergency-stop — it retries, then briefly skips that FATE/CE and picks another
- Critical Encounter arrival is less likely to fail right after you walk into the wait area
- Walking to an aetheryte for teleport is less likely to cancel/replan in a loop when you are already a step from the pad (also fixes stopping a step short of the knowledge crystal for buffs)

### Magic Pot timers
- If Eureka Linker is installed and showing pot timers, Illegal Mode uses those times (your own live pot still wins when you see one)

### Share chest locations
- Shared maps can now **fix wrong** built-in treasure chest and carrot spots, not only add missing ones
- Magic Pot chest spots are shared the same way: uploaded when you open one, downloaded for farming — wrong ones get corrected, missing ones get added
- Renamed from “Share maps” so it’s clear this is about chest and carrot places, not where you are standing

### Fixes
- Farm spot name field no longer loses focus while typing

### Clearer wording everywhere
- Config, status, chat, and logs use plainer language
- Status and Details show FATE and Critical Encounter names, and simple travel steps (walking / teleport / return to camp)
- Treasure Hunt, nearby chests, and farm spots no longer show raw IDs or world coordinates
- FATEs & CEs list shows plain status (in progress, registering)
- Clearer labels for combat rotation, travel plugin, path map, waiting status, and copying logs
