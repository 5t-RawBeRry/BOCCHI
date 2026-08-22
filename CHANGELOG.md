# 4.1.0.8

### Features

### Fixes
- Company of Stone (and other camp-adjacent CEs): mount before leaving base camp when the destination is far enough — no more walking halfway on foot first (#198).
- Eternal Watch (and similar CEs): travel no longer aims a huge stand-off from a bad MapRange size, arrives north/south of staging, then Returns and retries. Final walk now targets the wait ring centre (not staging when they differ). Eternal Watch staging moved to the ground ring (YuYu report).
- `/bocchi debug ce` (optional id, e.g. `ce 46`): stand where travel stops and paste output — shows authored staging, LGB, BOCCHI wait boundary, and whether you are inside.
- Pot chests: when an authored pad is a bit off, still recognize and walk to a live coffer near the pad instead of standing on the wrong spot forever.
- Treasure Hunt **Empty pad check distance** default is now **60** yalms (was 25). Existing configs keep their saved value.
- Treasure Hunt skips Unhallowed Hamlet coffer **2072** (basement stairs) — unreliable pathing and high death risk.
