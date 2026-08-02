# 4.0.1.10

### Fixes
- Automator Idle↔Pathfinding flicker: live FATE/CE path goals keep graph node Id so teleport routes resolve (#92)
- Treasure hunt: path CloseTo interact range, 2D distances, skip empties ~10y, don't freeze pathing at 5y (#93)
- Mob Farmer: stop Geo↔combat job flip-flop after Battle Bell (#94)
- Gold/silver tracker: don't count shop spend dip→recover as gains (#96)

### Features
- Refresh Pathfinding button on Automator — replan from current position (#95)
- Treasure hunt Pause / Resume — keeps the planned route (#98)

# 4.0.1.9

### Automator / navigation
- Stop teleport/nav retry spam after a mid-route cancel; toggle Illegal Mode to resume
- Don't Return-to-camp while already near / inside the active FATE (#84)
- Prefer Moldering Outskirts for A Rotten Affair (#88)
- Prefer Crown of Karnak for Forbidden Folios (CE #52)
- Fix Choosing Activity softlock when a CE is up but pot cutoff / Warmup left nothing startable (CE #52)
- Refresh CE positions when layout was missing; use authored fallback
- Illegal Mode On/Off chat message on toggle
- Pathfind / aethernet retry counts reduced; clearer cancel handling
- FATE approach uses live position and larger arrival radius
- Treasure Sight Freelancer loop fix (#86)
- Pot FATE / pot-chest farm gating and CE stickiness (#85 / #87)
- Party invite no longer steals Return Yes/No
- Treasure hunt: path to live chests, don't skip unstreamed coffers (#90)

### Mob Farmer
- Battle Bell restores combat job after Geomancer swap
- Stay in Fighting until out of combat (no mid-pack gather top-up)
- Mounted return to start (uses auto-mount / preferred mount)
- Option: specials don't count toward min pack (default off)
- Option: only start a new loop out of combat (default off)

### UI / quality of life
- Searchable preferred mount picker (Roulette + unlocked mounts)
- Buff reapply threshold label corrected to minutes
- Green carrot tethers restored (config toggle)
- Bronze / silver tethers already colored
- Ko-fi link updated to https://ko-fi.com/kagekazu
