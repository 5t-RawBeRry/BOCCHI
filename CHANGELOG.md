# 4.0.2.4

### Fixes
- Treasure Hunt: stop re-running the same aetheryte teleport forever after a hop (#123, #125)
- Illegal Mode: path to FATE/CE centers instead of treating “near combat radius” as arrived (stuck at aetheryte / outside CE gates) (#122, #124)
- Illegal Mode: with BOCCHI AI on, still approach and dismount to the FATE boss before handing movement to BossMod (#123)

### Features
- Illegal Mode: role-based combat positioning — melee/tank to hitbox edge, ranged/healer/caster hold 15y (BOCCHI AI StayCloseToTarget)
- Illegal Mode: BOCCHI AI BossMod preset is ephemeral — created when mode starts, deleted when it stops
