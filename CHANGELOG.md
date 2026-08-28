# 4.2.0.2

### Treasure Hunt
- Max coffer level is enforced for shared-map pads too (level 46+ areas no longer slip through when capped at 40)
- Walk pathing snaps pad targets onto the navmesh instead of pathfinding to layout coords inside the floor

### Movement
- Jump-when-stuck stops after a few failed hops and cancels pathing instead of jumping forever
- Auto-buff walk-in no longer loops pathfinding next to a knowledge crystal (vnav “Queueing move-to … within 1y” spam)

### Carrot Hunt
- Pads on a higher shelf no longer take a direct cliff walk (Return / aethernet instead)

### Mob Farmer
- Treasure Sight at the farm spot casts on its interval even with no Spots list and no prior Sight reading

### BossMod autorotation
- Phantom Chemist / WHM raises are on in BOCCHI AR presets
- Healer AI: Swiftcast raises (any dead player), heal, esuna, stay near party, and OOC tank predictive heals — Update presets if auto-update is off
