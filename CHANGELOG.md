# 4.0.2.22

### New
- Treasure Hunt option to visit silver coffers only (#163).
- Clearer North and South Horn treasure routes (walk each area in order; Return or aethernet between distant areas).
- South Horn: Return to camp at start if needed, Treasure Sight before pathing when enabled, alternate red/blue start half each session, finish one half then Return for the other — strict pad order with no Nearby detours.
- Carrot pad positions are fully baked for South and North Horn (no more background carrot-location uploads).

### Fixes
- Less hitching in Occult Crescent: pot-timer sync no longer freezes the game while talking to the server (#165).
- Removed leftover debug rainbow rings around critical encounters and camp aetherytes (dev-only again).
- South Horn sticky half and empty-skip behavior: stays on the active color, walks up before skipping empties, doesn’t open the wrong half’s chest, and keeps the full half planned when distant pads aren’t loaded yet.
- South Horn blue route order around Lost Citadel (outside southern pad before the interior citadel pad).
- North Horn Nearby peel finishes live coffers instead of walking toward them and abandoning around ~50 yalms.
- Treasure Hunt opens coffers the same way Pandora does (within 2 yalms, only when targetable) so you should see fewer “Too far away” errors.
- Pots & Treasure: pot FATE up pauses hunt and paths to the pot; after pots (and pot chests), hunt resumes where it left off.
- Radar lines no longer stick on coffers you already opened.
- Carrot Hunt panel stays hidden while Treasure Hunt, Pots & Treasure, or Illegal Mode filler is running.
- Illegal Mode no longer Returns to camp just because you were standing near a FATE/CE — it prefers a nearby aethernet when that is cheaper.
