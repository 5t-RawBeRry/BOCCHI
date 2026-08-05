# 4.0.2.7

### Fixes
- North Horn: Eye to Eye ports to The Crown of Karnak (not Unhallowed Hamlet) — preferred shard + walk-score aethernet pick so island gaps lose (#Discord Lee)
- Illegal Mode: path into CE registration reliably (e.g. A Beast Unleashed) — CE-specific approach ring, inner-circle arrival, no Return replans when near the CE, waiting state beats pathfinding at the edge (#132)
- Illegal Mode: while waiting for a CE, stop vnav once inside registration and hold position instead of pulling back to the arrival spot
- BOCCHI AI: bake automatic movement settings (pathfind destination, no overdodge buffer, go directly to destination, drop casts for movement, no delay)
- BOCCHI AI: prioritize everything (not FATE-only) so Critical Encounter bosses get targeted
- Pathing: start vnav movement first, then cast mount while running (no mount-before-walk wait)
- Illegal Mode: while waiting inside a CE blue box, hold position instead of re-pathing to the walk-in spot
- UI: option to show/hide BOCCHI chat messages (off silences notifications; MOTD still prints)
- UI: FATE/CE / nearby treasure / mob picker lists size to content (no large empty child boxes)
- UI: Event drops (demiatma / notes / soul shards) live under UI config instead of a separate page
- World: restore South Horn FATE/CE reward icons (demiatma / notes / soul shards; demiatma ownership tint)
- Pots & Treasure: hunt filler no longer fights mode exclusivity (StartManaged; On/Off spam fixed)
- Treasure: North Horn chest 2061 (Suspended Masonry, map ~5.4 34.1) approaches via safe spots so vnav skips the wind updraft

### Features
- Treasure: Auto treasure hunt in Illegal Mode — after CE/FATE, Return, Treasure Sight (when learned), then hunt if coffers remain
- Treasure: Opt in for live coffer routes — anonymously submit opens; hunts prefer accepted live spots (authored map fallback when empty)
- Treasure: Ninja Hide on dangerous routes — optional gearset swap + Hide near high-knowledge hostiles, finish coffer approach on foot
- Treasure: Farm pot chests uses Magical Elixir + compass hints on South and North Horn (blind sweep fallback if no buff/elixir)