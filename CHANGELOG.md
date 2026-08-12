# 4.0.2.26

### New
- First time in Occult Crescent should start much faster — South Horn and North Horn path maps come with the plugin instead of building on your PC.
- Illegal Mode and Completionist show path map status (loading / building / ready), plus a Rebuild path map button if routes go wrong.
- Manual Apply Buffs works at the first knowledge crystal inside Forked Tower: Magic (North Horn).

### Fixes
- Pot chest farming should no longer stand still forever on unreachable chests — it skips them and keeps going (#177).
- Treasure Hunt Return to camp is more reliable (dismount, confirm dialog, and retries).
- Allowed FATEs checkboxes for Magic Pot FATEs stay how you set them — Prefer pot FATEs / Farm pot chests no longer turn them back on in the list.
- Illegal Mode should no longer freeze near the camp aetheryte when it can’t plan a route — it rebuilds a bad path map and retries; if that still fails it pauses and tells you.
- Incomplete or outdated saved path maps are replaced with the bundled map (or rebuilt) automatically.
- Leaving one island and entering another (or a new instance) no longer keeps an old pot FATE timer — pot timing resets for that island so it can sync again.
- Waiting for FATEs/CEs and idling at camp use more random stand positions so people are less stacked on one tile.
- Treasure Hunt moves on when a coffer is already open (including if another plugin opened it for you).
- Treasure Hunt should open a coffer that’s already next to you instead of walking in place.
- Starting Treasure Hunt should finish casting Treasure Sight instead of cancelling it when switching phantom jobs.
