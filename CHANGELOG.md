# 4.2.0.16

### Illegal Mode
- Path planning failures no longer freeze Illegal Mode until you emergency-stop - it retries, then briefly skips that FATE/CE and picks another
- Critical Encounter arrival is less likely to fail right after you walk into the wait area
- Walking to an aetheryte for teleport is less likely to cancel/replan in a loop when you are already a step from the pad (also fixes stopping a step short of the knowledge crystal for buffs)

### Magic Pot timers
- If Eureka Linker is installed and showing pot timers, Illegal Mode uses those times (your own live pot still wins when you see one)

### Fixes
- Farm spot name field no longer loses focus while typing
