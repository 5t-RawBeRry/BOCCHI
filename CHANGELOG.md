# 4.0.2.18

### Fixes
- Illegal Mode travel: rebuild broken cached zone routes on load (and refresh old caches) so pathfinding doesn’t stay stuck until you reset plugin data.
- Clamp out-of-range travel timing / hunt distance settings left over from older configs.
- Auto shop now actually runs when enabled (the setting existed but the shopper was never started).
- Combat no longer cancels pathfinding outside Occult Crescent (was breaking other plugins’ movement in dungeons).
- Occult Sprint no longer counts as a combat cancel while traveling.
- Illegal Mode no longer soft-locks idle after a combat cancel mid-travel (keeps the goal; Replan/toggle no longer required).
- Leaving camp for the next FATE/CE no longer walks around behind the base aetheryte when you’re already in range.
- Field aetherytes: stop pathing into/through the crystal when the interact pad sits inside the body ring.
- Treasure Hunt: walk-to-shard uses the same stand-off (was still pathing into the crystal).
- Clearing a stuck aethernet hop (so the next teleport can start) when travel is stopped or replaced.
- Critical Engagements: BOCCHI AI enables again after Preparing→Battle even when the wait-area circle doesn’t match the live zone.
