# 4.0.2.22

### New
- Treasure Hunt option to visit silver coffers only (#163).
- Clearer North and South Horn treasure routes — each area in order, with Return or aethernet between distant spots.
- South Horn Treasure Hunt: Return to camp at the start if needed, Treasure Sight when enabled, finish one color half then Return for the other, and alternate which half you start on each run.
- Carrot spots are fully built in for South and North Horn.

### Fixes
- Occult Crescent should hitch less while pot timers sync in the background (#165).
- Leftover colorful debug rings around critical encounters and camp aetherytes no longer show in normal builds.
- South Horn Treasure Hunt sticks to the active color, walks up before skipping empty pads, and no longer switches halves early when distant pads aren’t loaded yet.
- South Horn blue route order around Lost Citadel (outside southern pad before the interior).
- North Horn Treasure Hunt no longer walks toward a live coffer and then gives up around ~50 yalms.
- Treasure Hunt opens coffers more reliably, with fewer “Too far away” errors — you can stay mounted.
- Pots & Treasure: when a pot FATE is up, hunt pauses and paths to the pot; after pots (and pot chests), hunt resumes where it left off.
- Map lines no longer stick on coffers you already opened.
- Carrot Hunt panel stays hidden while Treasure Hunt, Pots & Treasure, or Illegal Mode filler is running.
- Illegal Mode no longer Returns to camp just because you’re near a FATE or CE — it prefers a nearby aethernet when that’s cheaper.
