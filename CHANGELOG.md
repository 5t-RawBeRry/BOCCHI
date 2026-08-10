# 4.0.2.22

### New
- Treasure Hunt option to visit silver coffers only (#163).
- Treasure Hunt uses clearer North and South Horn routes: walks each area in order, and uses Return or aethernet between distant areas. On South Horn it Returns to base camp at start (if needed), casts Treasure Sight before pathing when that option is on, alternates which colored half it starts so you don’t open on the same half twice in a row, then finishes the other half after Return.

### Fixes
- Less hitching in Occult Crescent: carrot pad and pot-timer sync no longer freeze the game while talking to the server (#165).
- Removed leftover debug rainbow rings around critical encounters and camp aetherytes.
- Treasure Hunt peels off for live Nearby coffers more reliably — including when several are close, and when a pad was wrongly marked empty — then continues the route (closest first).
- South Horn Treasure Hunt stays on the red or blue half after empty skips instead of jumping to the other half mid-route.
- South Horn Treasure Hunt sticks to one colored half at a time (walks that half in order; Return only when switching halves). Nearby peel-off stays on the active half.
- Treasure Hunt walks up to empty pads (~100 yalms, when coffers are loaded) before skipping them — it no longer marks distant pads empty just because another chest was loaded nearby.
- Treasure Hunt tries to open coffers that stay non-targetable until you interact.
- Radar lines no longer stick on coffers you already opened.
- Carrot Hunt panel stays hidden while Treasure Hunt, Pots & Treasure, or Illegal Mode filler is running.
- Illegal Mode no longer Returns to camp just because you were standing near a FATE/CE — it prefers a nearby aethernet when that is cheaper.
