# 4.0.2.21

### Fixes
- Carrot Hunt teleports via aethernet when that is faster than walking.
- Carrot Hunt dismounts near carrots it cannot path onto (e.g. corners) and uses them from there.
- Carrot Hunt stays for a second chewed carrot at the same pad (double spawn) before moving on.
- Treasure Hunt no longer spams Treasure Sight or freezes when cast-during-hunt is on.
- Treasure Hunt skips a coffer it cannot open and continues (#162).
- Treasure Hunt progress counts up as coffers are checked (it looked like a countdown after each replan).
- Base camp aetheryte approach stays on your side and closes into teleport range (#158).
- Aetheryte approach no longer floor-snaps to the far side of the crystal.
- Aetheryte approach stops on the magenta (teleport) ring, not the outer cyan idle ring.
- Leaving camp to teleport walks into the magenta ring instead of idling outside cyan.
- Travel no longer soft-pauses Illegal Mode when a path step is canceled mid-route (it replans instead).
- Long same-shard trips go via the aetheryte pad instead of a fake “cheap” cross-map walk (e.g. Waved Away).
- A Beast Unleashed uses the authored registration area again.
- Circular CE debug: red = registration edge, cyan = stand/wait spot inside it; Accept No Imitators / Doubled Trouble radii match the rim better.
- FATEs dismount when you enter the event, same as critical encounters.
- Illegal Mode switches to a critical encounter if one opens while you are still traveling to a non-pot FATE (and finishes the FATE if you are already in it).
- Leaving Occult Crescent turns Illegal Mode (and related modes) fully off instead of keeping them on to resume when you return.
