# 4.2.0.5

### Treasure Hunt
- Mid-route Treasure Sight no longer mount/dismount thrash: holds the route until you are on foot, then casts (auto-mount stays off until Sight finishes)
- Standing in front of a coffer no longer re-queues the same walk every tick (that cancelled movement and left you stuck)
- Sitting just over 2y from a chest now opens it instead of walking to a mesh point beside it and waiting
- Stuck sideways step is given time to start; auto-mount stays off so remount does not cancel it. If the pad is still empty after that, it is skipped. If a chest is still blocked, interact is allowed from a bit farther away

### Illegal Mode
- "Returning to camp" no longer stands forever after a FATE/CE: stops movement, waits for combat/dismount, then casts Return; if Return stays blocked it times out and continues with teleport/walk instead
- Leaving Idle during a map treasure hunt no longer stops the hunt's walk (that re-queued the same camp-to-coffer path every tick)
- New options under Auto treasure hunt: pause for FATEs and/or CEs even when Treasure Sight is available (map hunt without Sight already pauses for both). Off keeps Sight hunts from yielding to that activity type
