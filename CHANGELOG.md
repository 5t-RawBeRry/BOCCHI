# 4.0.2.21

### New
- Carrot Hunt: full North Horn map (25 pads), aethernet / Return when cheaper, replan after each pad, and peel off for a chewed carrot next to you.
- Carrot Hunt can Return to base camp when the route finishes (same setting as Treasure Hunt).
- `/bocchi debug` toggles the debug window.

### Fixes
- Carrot Hunt walks in close enough for Fortune Carrot / bunny chests, handles double spawns, and no longer Returns past a carrot next to you.
- Treasure Hunt peels off for a live coffer in the Nearby list (~120 yalms), including mid Return or aethernet.
- Treasure Hunt / radar: every live coffer shows again; empty pads skip sooner; less likely to skip a pad that still has a chest.
- Treasure Hunt no longer spams Treasure Sight or freezes with cast-during-hunt on.
- Treasure Hunt skips coffers it cannot open and continues (#162); progress counts up as coffers are checked.
- Treasure Hunter UI: Start Hunt and Carrot Hunt hide each other while the other is running.
- Treasure Nearby list no longer crashes on odd coffer ids; no more “Invalid target” when mounting after aethernet; automation does not open coffers while dead.
- Aetheryte approach stays on your side and stops on the teleport ring (#158); camp idle waits inside the ring again.
- Long same-shard trips use the aetheryte instead of a fake cross-map walk (e.g. Waved Away).
- Travel canceled mid-route replans instead of soft-pausing Illegal Mode.
- Illegal Mode switches to a critical encounter that opens while traveling to a non-pot FATE (and finishes a FATE you’re already in).
- FATEs dismount on enter, same as critical encounters.
- Leaving Occult Crescent turns all automation modes fully off (no auto-resume on return).
- Reloading mid-CE and turning Illegal Mode back on re-enables BOCCHI AI for that fight.
- A Beast Unleashed uses the correct registration area and arena center.
- Pots & Treasure mounts when a treasure hunt starts from base camp.
