# 4.1.0.10

### Fixes
- **Illegal Mode + auto treasure hunt:** starting a hunt while idle at camp no longer loops pathfinding in place — it leaves camp toward the coffers.
- **Illegal Mode travel:** after a FATE or CE, Return to camp actually runs before the aethernet hop (a pending treasure hunt no longer skips it). Walks into the Lifestream ring instead of stopping on the edge, which made the hop fail.
- **Dark Artistry** (North Horn): activity area is a square, so pathing and “in CE” checks match the real zone.
- **Pot chests (North Horn south pot):** the center-island chest no longer loops under the island. Pathing stays on the island floor, and it uses elixir once it is standing on the mesh point instead of re-pathing in place (#201).
- **Pot chests:** turning Illegal Mode on after you already used the elixir uses that pot’s chest list (or the nearest chest pad), not whichever pot FATE you happen to be standing closer to.
- **Carrot Hunt (North Horn):** the west Suspended Masonry carrot (~2.4, 35.9) approaches across the island instead of taking the long way around a jump.
- **Treasure Hunt:** no longer marks a pad empty while still walking up to it, so a coffer that appears late is opened instead of skipped. If one was already skipped by mistake, the hunt turns back when that coffer shows up. After Return to camp, it waits for pathing to finish before heading to the first chest.
- **Pot chests:** compass hints after Magical Elixir use the place you drank from, not the pad you arrive at later — so a hint that landed mid-walk no longer sends you the wrong way past the next chest.
- **Eternal Watch** (South Horn): travel aims at the ground join ring near the beach instead of following a bad elevated area or stopping outside the real blue circle.
- **Treasure Hunt:** finishing the last coffer while still in combat no longer ends the hunt immediately — it walks toward camp until you can Return.
