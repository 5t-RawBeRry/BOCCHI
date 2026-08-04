# 4.0.2.5

### Features
- Dedicated **Pots & Treasure** mode (#114): pot FATEs (+ pot chests) when up / near spawn; soft-pauses Treasure Hunt between windows and resumes mid-map (covers Discord pause/resume for pot interrupts)
- Treasure Hunt / Pots & Treasure: show last-checked and resume coffer IDs, plus a flag button to mark the resume point on the map
- UX: cleanup
- Config: cleanup

### Fixes
- Illegal Mode: keep pathing into CE registration until clearly inside the blue box (was stopping on the edge, e.g. A Beast Unleashed)
- Treasure Hunt: open coffers while mounted (no dismount-per-chest)
- Treasure Hunt: fixed zone route — resume from nearest remaining coffer by location; no mid-route Return to camp (aethernet/walk only); early-abort only after Treasure Sight
