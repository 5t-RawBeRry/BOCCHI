# 4.2.0.3

### Startup
- Plugin init runs in `LoadAsync` instead of the constructor, so Dalamud boot no longer hitches on DI setup and lifecycle start
- Translations load only the active language at boot; other languages load on first switch
- Shop catalog init walks the Item sheet once instead of twice

### Performance
- FATE and Critical Encounter repos refresh every 500ms instead of every frame
- Mob Farmer object scan idles when stopped and the panel is collapsed; preview counts throttle to 500ms
- Treasure and carrot object scans run only when radar lines or an active hunt need them (250ms when on)
- Pot timer UI reuses a cached cycle list; Treasure Sight log patterns are cached after first parse

### Treasure Hunt
- Empty-skip again uses Empty pad check distance (default 60y): no live coffer in range → skip; lower walks closer when chests load late
- After a navmesh snap lands short of the pad, the hunt closes the remaining gap (mounted when auto-mount is on) before giving up

### Illegal Mode
- Eternal Watch no longer cancel/replans on an under-mesh wait point (Y~1.22); stand uses the platform and off-mesh approaches keep the planned walk

### Shopping
- Auto Shop no longer bounces between the antiquarian and the knowledge crystal at North camp (buff walks yield; path targets the vendor)

### Mob Farmer
- Gathering no longer loops the same wall route: progress timeout → stop → sideways nudge → repath to mob → skip after repeated failures
- Pull paths walk toward the mob instead of a geometric offset through geometry; farm spot / return-home use the same stuck recovery
- Cast Treasure Sight at farm no longer sits on a 15s chain timeout when still mounted or job-swap is blocked; retries in 1 minute if Sight fails
