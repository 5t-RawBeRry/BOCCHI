# 4.0.2.2

### Fixes
- Zone graph: skip non-finite edge costs; allow named float literals in JSON (North Horn crash)
- Treasure Hunt: open coffers without Pandora; stop mid-run aetheryte stalls; reduce chest camera shake from repath spam (#110, #113)
- Illegal Mode: auto-return no longer spams / stalls on SelectYesno (#107)
- Illegal Mode: WrathCombo no longer stays locked; rotation priority is BossMod-only
- Illegal Mode: Choosing Activity no longer softlocks when pot cutoff blocks non-pot FATEs
- Illegal Mode: prefer authored CE positions over live LGB markers (Accept No Imitators / tower CEs)
- Illegal Mode / Treasure Hunt: zone-lock automation — do not Return or path outside Occult Crescent (PvP / Limsa)
- Do not auto-dismiss party invite SelectYesno (only auto-accept Return while Illegal Mode Returning is active)

### Features
- Forked Tower (South Horn): trap position overlays in-instance + registration countdown in Critical Encounters (#116)
- Illegal Mode: `BOCCHI AI` BossMod preset — AutoTarget (aggressive/always retarget, FATE prio) + NormalMovement (pathfind, 0.5y overdodge, max range, slidecast leeway)
- Illegal Mode: when BOCCHI AI is enabled, defer FATE/CE targeting and combat approach to VBM
- Illegal Mode: Stop after return — Return (+ inbound teleport), no auto-walk to FATE/CE (#109)
- Illegal Mode: Phantom jobs leveling mode — switch off maxed phantom jobs (#89)
- Illegal Mode: Preposition to predicted pot FATE with random stand-off before spawn (#112)
