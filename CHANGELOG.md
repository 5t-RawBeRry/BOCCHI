# 4.0.2.5

### Features
- Treasure Hunt: Saucy-style hunt-complete MP3s (bundled Moogle / Game Over / Time Up) plus a Sounds folder for custom clips
- Dedicated **Pots & Treasure** mode (#114): pot FATEs (+ pot chests) when up / near spawn; soft-pauses Treasure Hunt between windows and resumes mid-map (covers Discord pause/resume for pot interrupts)
- Treasure Hunt / Pots & Treasure: show last-checked and resume coffer IDs, plus a flag button to mark the resume point on the map
- Sprint to aetherytes (#129) — toggleable (on by default)
- Treasure section renamed to Treasure Hunter; hunt controls always available (removed Show Treasure Hunt button)
- Commands unified under `/bocchi <subcommand>` (e.g. `/bocchi config`); legacy `/bocchi-*` slash commands removed
- BOCCHI AI (melee): include Misc AI Goes to specified positional (Any)
- UX: cleanup
- Config: cleanup

### Fixes
- Illegal Mode: mount when walking to mid-map aetherytes (Pathfind before Teleport again)
- BOCCHI AI: Activate/Deactivate only that preset so travel no longer wipes the user's other BossMod presets
- Treasure Hunt: defer Treasure Sight while in combat (no mid-fight PJ swap / stuck on foot) (#128)
- Illegal Mode: start pathing toward Fate/CE while mount animation is in progress (#130)
- Illegal Mode: keep pathing into CE registration until clearly inside the blue box (was stopping on the edge, e.g. A Beast Unleashed)
- Treasure Hunt: open coffers while mounted (no dismount-per-chest)
- Treasure Hunt: fixed zone route — resume from nearest remaining coffer by location; no mid-route Return to camp (aethernet/walk only); early-abort only after Treasure Sight
