# 4.0.2.15

### New
- **Triage Mode** (Illegal Mode setting, off by default): after a FATE or CE, if someone nearby is dead and needs a raise, BOCCHI briefly switches to Phantom Chemist, raises them (skips people who already have a raise pending), switches back, then continues as usual. If nobody needs a raise, nothing extra happens — no job swap and no waiting around.
- **Dependencies** screen: optional plugins are grouped more clearly — **BOCCHI AI** (BossMod *or* BossMod Reborn) and **Autorotation** (Wrath Combo *or* Rotation Solver Reborn).

### Fixes
- **Mob Farmer:** with Use Battle Bell on, Bell is refreshed before every pull (not skipped while the buff is still up), and Sprint is used more reliably right after Bell.
- **Apply Buffs** / `/bocchi buff`: casts where you stand — you must already be in the knowledge crystal circle (no auto-walk to the crystal).
- **Pot chests:** keeps farming while you have **Cache Me If You Can**; stops when that buff is gone (including if the pot dies and you never got the buff). No longer starts chest runs between pot FATE waves, and the next-pot timer resets when you leave Occult Crescent or change instance.
- **Pot elixir hints:** if the compass redirects you (e.g. Far → another direction), BOCCHI switches to that area instead of walking the old group’s next spot.
- **A Beast Unleashed:** join area is treated as a square (matches the blue zone better for waiting and overlays).
- **Critical Encounters:** less likely to Return to camp early while you’re waiting in or committed to a CE (e.g. Familiar Tactics / Unbridled) when buffs are low or the join area flickers.
