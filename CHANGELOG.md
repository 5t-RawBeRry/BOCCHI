# 4.0.2.15

### Changes
- Dependencies: Optional section now groups **BOCCHI AI** (BossMod or BossMod Reborn) and **Autorotation** (Wrath Combo or Rotation Solver Reborn)
- Illegal Mode: optional **Triage Mode** — after FATE/CE, Chemist Revive nearby (skips raise-pending); before Return / Treasure Sight

### Fixes
- Triage Mode: only swap to Chemist when a raisable corpse is nearby; settle/throttle job changes (avoid “unable to change phantom jobs”)
- Apply Buffs button / `/bocchi buff`: cast in place only (no pathfinding); must already stand in the crystal buff circle
- Pot chests: farm until **Cache Me If You Can** clears (not on first open / silver / leftover elixir); abort when pot dies with no buff
- Pot chests: do not start between FATE waves — wait until the pot FATE is gone
- Pot timer: clear predicted next-pot when leaving OC or hopping to a new instance
- A Beast Unleashed (#56): treat join area as an **axis-aligned square** (debug + wait/arrival); prefer live LGB center so overlays match the blue zone
- Illegal Mode: do not opportunistic Return while committed to a CE (wait latch / SuspendTravel / live CE goal) — fixes Familiar Tactics / Unbridled Idle→camp with Goal still CE
- Illegal Mode: keep Waiting/In CE once latched even if join-area geometry flickers; keep CE goals while still pathing in when Battle starts
- Pot chests: Magical Elixir Far/redirect hints switch compass groups (e.g. NW → West) instead of walking the next spot in the old group
