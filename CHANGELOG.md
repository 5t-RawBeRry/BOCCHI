# 4.1.0.4

### New
- Under Combat you can rebuild the stock BOCCHI AI presets, turn on automatic updates, and set stay-close range, overdodge, movement delay, and optional separate dodge delay.

### Fixes
- Rotation Solver Reborn is detected when it is actually running. Illegal Mode no longer claims it is not loaded if Dalamud also lists an old or disabled copy of Rotation Solver.
- Illegal Mode keeps walking into a FATE until it is close enough for combat AI to take over, instead of stopping on the registration rim.
- Combat AI stays off while traveling to a Critical Encounter, so it no longer pulls trash on the road or at the registration edge.
- Illegal Mode no longer stays In CE from open-world trash combat or a stuck travel latch when you never joined the encounter (#196).
- Illegal Mode no longer pathfinds you back to the CE stand ring while you are still inside the registration area waiting.
- With Auto Treasure Hunt in Illegal Mode off, starting Treasure Hunt turns Illegal Mode off instead of letting both run.
- BOCCHI AI FATE / BOCCHI AI CE (and the AR pair) are kept if you already have them. Illegal Mode no longer deletes those BossMod presets when you stop.
- North Horn pot chest farming skips pads vnav cannot reach instead of retrying them forever (east Daylight Pottery, #194).
- Treasure Hunt takes authored stairs into the Unhallowed Hamlet basement coffer (2072) instead of getting stuck west of it (#195).
- Treasure Sight “no coffers in the area” clears the active bronze/silver counts instead of leaving a stale tally.
- Treasure Hunt Ninja Hide “Knowledge this much higher” uses player Knowledge + offset with no clamp to 40 (e.g. offset 6 at Knowledge 40 only hides from 46+).
