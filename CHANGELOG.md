# 4.2.0.3

### Startup & performance
- BOCCHI loads faster when Dalamud starts
- Other UI languages load the first time you pick them, not at boot
- Less background work in Occult Crescent when automation is idle
- Mob Farmer mob counts update less often when the farmer is stopped and the panel is collapsed
- Treasure radar and nearby lists only scan when radar lines or an active hunt need them

### Treasure Hunt
- Empty pad skip again respects **Empty pad check distance** (default 60y): walks closer before moving on; lower the slider if coffers often load late
- When pathing stops short of a pad, the hunt walks the rest of the way in (mounted when auto-mount is on) instead of skipping early

### Illegal Mode
- Eternal Watch no longer cancels and replans when a wait point sits under the platform; the route keeps walking to the stand as planned

### Shopping
- Auto Shop at North camp no longer bounces between the antiquarian and the knowledge crystal

### Mob Farmer
- Gathering no longer loops into the same wall: if progress stalls, BOCCHI sidesteps and repaths, then skips the mob after repeated failures
- Pull paths go to the mob instead of through walls; return home and farm spot use the same recovery
- Cast Treasure Sight at the farm no longer hangs for 15s when still mounted or a job change is blocked; retries in 1 minute if Sight fails
