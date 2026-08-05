# 4.0.2.7

### Fixes
- Illegal Mode: path into CE registration reliably (e.g. A Beast Unleashed) — CE-specific approach ring, inner-circle arrival, no Return replans when near the CE, waiting state beats pathfinding at the edge (#132)
- Illegal Mode: while waiting for a CE, stop vnav once inside registration and hold position instead of pulling back to the arrival spot
- BOCCHI AI: bake automatic movement settings (pathfind destination, no overdodge buffer, go directly to destination, drop casts for movement, no delay)
- Pathing: start vnav movement first, then cast mount while running (no mount-before-walk wait)
- Illegal Mode: while waiting inside a CE blue box, hold position instead of re-pathing to the walk-in spot
- Illegal Mode: optional auto treasure hunt when idle and survey counts meet thresholds (unified set-and-forget filler)
- Illegal Mode: AOCC-style post-activity Treasure Sight latch — Return to camp, survey, hunt on thresholds, defer rescans when below
- Treasure: opt-in coffer observation submission
- UI: option to show/hide the [BOCCHI] chat prefix
