# 4.0.2.21

### New
- Carrot Hunt routes like Treasure Hunt: aethernet and Return when cheaper, replan after each pad, and peel off for a chewed carrot next to you.
- Carrot Hunt can Return to base camp when the route finishes (same setting as Treasure Hunt).
- North Horn Carrot Hunt has a full 25-pad map.
- `/bocchi debug` toggles the debug window.

### Fixes
- Carrot Hunt walks in close enough to use Fortune Carrot / open bunny chests, dismounts at awkward pads, and stays for double spawns.
- Carrot Hunt no longer Returns when a chewed carrot is right next to you.
- Treasure Hunt peels off for a live coffer within ~120 yalms even mid Return or aethernet (same idea as Carrot Hunt).
- Treasure Hunt no longer walks past a bronze the Nearby list already shows (live-first divert; was stuck on via / far pad distance).
- Treasure Hunt no longer spams Treasure Sight or freezes with cast-during-hunt on.
- Treasure Hunt skips coffers it cannot open and continues (#162).
- Treasure Hunt progress counts up as coffers are checked (it looked like a countdown).
- Treasure Hunt / radar: every live coffer shows again; empty pads skip sooner when the area is loaded; less likely to skip a pad that still has a chest.
- Treasure Hunter UI hides Start Hunt while Carrot Hunt is running, and hides Carrot Hunt while a coffer hunt is running.
- Aetheryte approach stays on your side and stops on the teleport ring (#158); camp idle waits inside the ring again.
- Long same-shard trips use the aetheryte instead of a fake cross-map walk (e.g. Waved Away).
- Travel canceled mid-route replans instead of soft-pausing Illegal Mode.
- Illegal Mode switches to a critical encounter that opens while traveling to a non-pot FATE (and finishes a FATE you’re already in).
- FATEs dismount on enter, same as critical encounters.
- Leaving Occult Crescent turns all automation modes fully off (no auto-resume on return).
- Reloading mid-CE and turning Illegal Mode back on re-enables BOCCHI AI for that fight.
- A Beast Unleashed uses the correct registration area and arena center.
- Pots & Treasure mounts when a treasure hunt starts from base camp.
- No more “Invalid target” flash when mounting right after an aethernet teleport.
- Automation no longer tries to open coffers while you are dead.
- Treasure Nearby list no longer crashes on odd coffer ids that aren’t in the game sheet.
