# 4.0.2.11

### Fixes
- Illegal Mode: do not start pot chest farming without the Cache Me If You Can buff (stops empty-map blind sweeps after failed/abandoned pot goals)
- Illegal Mode: Rebuild Plan also cancels an in-progress pot chest farm
- BOCCHI AI: separate FATE / CE presets (FATE→FATE targeting; CE→Everything) so CE bosses still get targeted
- Illegal Mode: seed FATE/CE hard targets from activity lists again — BossMod AutoTarget often never sees CE bosses (#133)
- Illegal Mode: stop dual-vnav at FATE/CE — hand combat movement to BOCCHI AI only (fixes edge stutter / run-out-then-back)
- Illegal Mode: wait for CEs inside the combat (red) ring again — yellow–red stand-off left players outside registration (#140)
- Pathfinding: do not stack vnavmesh SimpleMove requests (stops ERR "Pathfinding task is in progress...")
- Buffs: Inquiring Mind applies all selected crystal buffs in one cast (was Quicker Step-only — could loop / skip other buffs)
- Treasure Hunt: restore the previous gearset after the Ninja Hide flow

### Features
- Illegal Mode: `Stop after return and teleport` — return, teleport toward the next FATE/CE, mount, then stop so you walk the rest (#139)
- Illegal Mode: optional random wait at camp before teleporting to a FATE/CE (#138)
