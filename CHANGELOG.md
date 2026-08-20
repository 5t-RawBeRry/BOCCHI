# 4.1.0.5

### Fixes
- Rotation Solver Reborn shows Ready again when it is loaded, and Illegal Mode can set Henched again. 4.1.0.4’s IPC check used the wrong type and blocked the rotation.
- Pot FATE/CE cutoffs are measured to the next pot spawn, not spawn minus leave-early. Leave-early only controls when to walk to the pot, so a 5m FATE cutoff no longer blocks FATEs for 10m when leave-early is also 5.
- Treasure Hunt Ninja Hide uses your actual Knowledge, not South Horn sync, so high-Knowledge characters stop hiding from trash that will not aggro (#197).
- Illegal Mode no longer stays on Waiting for CE after the encounter starts when EventId / enemy tags lag — it enters the CE and switches the BossMod preset.
