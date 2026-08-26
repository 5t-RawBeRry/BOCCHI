# 4.1.0.10

### Fixes
- **Illegal Mode:** starting a treasure hunt while idle at camp no longer pathfinds in place. After a FATE or CE, Return runs before the aethernet hop and walks into the Lifestream ring (edge stops were failing the hop).
- **Dark Artistry** (North Horn): activity area is a square, so pathing and “in CE” checks match the real zone.
- **Pot chests:** North Horn south pot no longer loops under the island (#201). Illegal Mode after a manual elixir uses that pot’s pads. Compass hints use where you drank Magical Elixir, not a later pad.
- **Carrot Hunt (North Horn):** the west Suspended Masonry carrot (~2.4, 35.9) approaches across the island instead of the long way around a jump.
- **Treasure Hunt:** does not mark pads empty while still walking up (and can turn back if a coffer was skipped too early). After Return to camp, waits for pathing before the first chest. Finishing the last coffer in combat walks toward camp until Return works.
- **Eternal Watch** (South Horn): travel aims at the ground join ring instead of a bad elevated area or stopping outside the blue circle.
