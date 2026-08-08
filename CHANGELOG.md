# 4.0.2.16

### New
- After you update BOCCHI, a **What’s new** window shows these notes once. Open it again anytime with `/bocchi changelog`.
- **Completionist Mode** is its own automation (separate from Illegal Mode). It only goes for field notes you still need. The checklist shows notes and phantom jobs. Click a survey point to flag it on the map and travel there (teleport from an aetheryte when you can, then walk mounted). Forked Tower notes are listed for tracking only.
- **Triage Mode** can raise with **Phantom Chemist** or **Phantom White Mage** — pick which under Illegal Mode settings. If your choice isn’t unlocked, it uses the other when available.
- **Pots & Treasure** has **Pause** and **Resume**. Pause stops movement but keeps the run; **Stop** ends it.
- **Preferred mount** defaults to **Mount Roulette**. If a chosen mount can’t be used, BOCCHI falls back to roulette.

### Fixes
- **Apply Buffs** works again at knowledge crystals when you stand at them (including right next to the crystal).
- Walking to base-camp aethernet stops at **2 yalms** and hands off to Lifestream (no closer pathing into the crystal).
- Critical Encounters path into the blue zone instead of stopping on the edge (**A Beast Unleashed**, **Quarried Away**, **With Extreme Prejudice**, and similar). World panel **Path** uses the same behavior.
- **On the Hunt** approaches through Lost Citadel from base camp, instead of routing around from Eldergrowth.
- **Next pot** timer no longer resets to unknown when a FATE ends or you leave Occult Crescent.
- BOCCHI no longer uses Sprint while in base camp.
- Using a combat ability while traveling cancels pathfinding. Toggle Illegal Mode or Completionist to continue if that interrupted a run.
- **Carrot Hunt:** clearer waiting text, and a **Use Fortune Carrot** button when you need to use one by hand. Fortune Carrots are still used automatically at chewed carrots.
- Completionist shows status and current goal like other modes, with **Replan route** while it’s running.
