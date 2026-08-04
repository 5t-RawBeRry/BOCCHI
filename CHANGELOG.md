# 4.0.2.5

### Features
- Korean localization (thanks FloweringKIM) — aligned with current UI labels (Illegal Mode, Treasure Hunter, Pots & Treasure, `/bocchi` commands)
- Treasure Hunt: hunt-complete MP3s (bundled Moogle / Game Over / Time Up) plus a Sounds folder for custom clips
- Dedicated **Pots & Treasure** mode (#114): pot FATEs (+ pot chests) when up / near spawn; soft-pauses Treasure Hunt between windows and resumes mid-map (covers Discord pause/resume for pot interrupts)
- Treasure Hunt / Pots & Treasure: show last-checked and resume coffer IDs, plus a flag button to mark the resume point on the map
- Sprint to aetherytes (#129) — toggleable (on by default)
- Treasure Hunt: configurable Treasure Sight every N coffers (default 10) — fewer Freelancer swaps / dismounts in contested areas
- Treasure section renamed to Treasure Hunter; hunt controls always available (removed Show Treasure Hunt button)
- Commands unified under `/bocchi <subcommand>` (e.g. `/bocchi config`); legacy `/bocchi-*` slash commands removed
- BOCCHI AI (melee): include Misc AI Goes to specified positional (Any)
- Config and UI cleanup

### Fixes
- Illegal Mode: cast Return while mounted when allowed (no awkward dismount-then-Return when aborting a late FATE/CE)
- Treasure Hunt: if Illegal Mode is on, soft-pause it for the hunt and auto-resume FATE/CE farming when the hunt ends (no concurrent pathing)
- Plugin installer icon: restore real logo (was a blank navy square) and point IconUrl at the BOCCHI repo
- BOCCHI AI: more reliable preset create on BossMod Reborn (retry while Illegal Mode is on; don't delete on provider flicker; BMR deactivate only clears when BOCCHI AI is active; stop eagerly injecting unused Ocelot Single Target)
- Illegal Mode: mount when walking to mid-map aetherytes (Pathfind before Teleport again)
- BOCCHI AI: Activate/Deactivate only that preset so travel no longer wipes the user's other BossMod presets
- Treasure Hunt: defer Treasure Sight while in combat (no mid-fight PJ swap / stuck on foot) (#128)
- Illegal Mode: start pathing toward Fate/CE while mount animation is in progress (#130)
- Illegal Mode: keep pathing into CE registration until clearly inside the blue box (was stopping on the edge, e.g. A Beast Unleashed)
- Treasure Hunt: open coffers while mounted (no dismount-per-chest)
- Treasure Hunt: fixed zone route — resume from nearest remaining coffer by location; no mid-route Return to camp (aethernet/walk only); early-abort only after Treasure Sight
