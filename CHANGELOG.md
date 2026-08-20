# 4.1.0.5

### Fixes
- Rotation Solver Reborn turns on Henched in FATEs and CEs instead of staying Off while BOCCHI AI is already running.
- Pot FATE and CE cutoffs are minutes until the next pot spawn. Leave-early only controls when you walk there, so those sliders no longer stack.
- Treasure Hunt Ninja Hide uses your actual Knowledge, not South Horn sync, so high-Knowledge characters stop hiding from trash that will not aggro (#197).
- Illegal Mode stays in a Critical Encounter after battle starts, so Wrath / RSR keep fighting instead of dropping the CE and turning combat off.
- Pot timer sync backs off when the worker is rate-limited, instead of retrying every 20 seconds.
