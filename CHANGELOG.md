# 4.1.0.10

### Fixes
- **Illegal Mode + auto treasure hunt:** starting a hunt while idle at camp no longer loops pathfinding in place — it leaves camp toward the coffers.
- **Illegal Mode travel:** after a FATE or CE, Return to camp actually runs before the aethernet hop (a pending treasure hunt no longer skips it). Walks into the Lifestream ring instead of stopping on the edge, which made the hop fail.
