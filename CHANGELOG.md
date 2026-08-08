# 4.0.2.16

### New
- After you update BOCCHI, a **What’s new** window shows these notes once. Open it again anytime with `/bocchi changelog`.
- **Completionist Mode** is its own automation (not under Illegal Mode). Start it from the Completionist section to go only for field notes you still need. The checklist shows notes and phantom jobs. Click a survey point to place a map flag and path there (aethernet when at a shard, then walk with mount — same travel as Illegal Mode). Forked Tower notes are tracking-only.
- **Triage Mode** can raise with **Phantom Chemist** or **Phantom White Mage** — pick which under Illegal Mode settings. If your choice isn’t unlocked, it uses the other when available.
- **Pots & Treasure** has **Pause** and **Resume**. Pause stops movement but keeps the run; **Stop** ends it.
- **Preferred mount** defaults to **Mount Roulette**. If a chosen mount can’t be used, BOCCHI falls back to roulette.

### Fixes
- **Apply Buffs** works again at knowledge crystals when you stand at them (including right next to the crystal).
- Walking to base-camp aethernet stops at **2 yalms** and hands off to Lifestream (no closer pathing into the crystal).
- Critical Encounters no longer stop on the blue-zone edge: wait/arrival uses the inner part of the combat area (helps **A Beast Unleashed**, **Quarried Away**, **With Extreme Prejudice**, and similar).
- **On the Hunt** teleports via base camp and walks the Lost Citadel approach, instead of Eldergrowth and routing around the citadel.
- **Next pot** timer no longer resets to unknown when a FATE ends or you leave Occult Crescent (pot-cycle sync was clearing it).
- World panel **Path** on Critical Encounters uses the same inner CE stand-off as Illegal Mode.
- BOCCHI no longer uses Sprint while in base camp.
- Using a combat ability while traveling cancels pathfinding. If that interrupts Illegal Mode or Completionist, toggle the mode to continue.
- **Carrot Hunt:** clearer waiting text, and a **Use Fortune Carrot** button when you need to use one by hand. Fortune Carrots are still used automatically at chewed carrots.
- Completionist status and current goal show in the main window like other modes; **Replan route** is available while Completionist is running.
