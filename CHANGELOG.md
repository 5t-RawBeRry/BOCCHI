# 4.1.0.7

### Features
- Mob Farmer uses dedicated BossMod presets **BOCCHI AR MOB** / **BOCCHI AI MOB** (open-world Everything AutoTarget) instead of reusing FATE AR, so pack farming can be tuned without changing FATE/CE.
- BossMod full AR presets (**BOCCHI AR FATE/CE/MOB**) enable AOE on every stock xan job module, and bake optimized Veyn WAR tracks (ForceAOE, Spend burst, no auto potion, Infuriate on overcap, NoReserve Onslaught, opener Tomahawk). Recreate presets or use Update presets under Combat.
- Mob Farmer pull buffs can cast **Counterstance** (Phantom Monk → Fleetfooted) late in the buff sequence so the short mitigation covers the start of the pull.

### Fixes
- Dying in a CE no longer drops the CE goal / resumes travel before Accept Raise — commitment is kept until you leave for a non-Dead state.
- Mob Farmer auto-target is only used during the pull; once Fighting starts with combat AI on, targeting is left to BossMod / Wrath / RSR.
- Mob Farmer waits for Ringing Respite after Quickstep when they share a cooldown, instead of skipping Respite as soon as Geomancer is equipped.
- Pot chest second-chance / long hops use Return + aethernet instead of walking across the map when the destination is not on the path map.
- After opening a pot chest, the farm only searches second-chance pads (not the pot FATE spots again).
- Wrath Auto-Rotation: after a job change (or Wrath suspending leases), recreate the lease instead of spamming Invalid lease and leaving combat autorotation off.
- Wrath phantom jobs: only force-disable Occult Elixir (Wrath's "VERY costly" warning). Potion, Ether, Zeninage, and other "costly" options stay enabled.
- Wrath Auto-Rotation: only lock Healer Targeting Mode; HP% heal thresholds, Only Raise Raisers, and Heal Friendly NPCs stay editable.
- Illegal Mode auto treasure hunt without Treasure Sight (Freelancer &lt; 10): pauses the map route when a FATE/CE is available, then resumes the same hunt afterward instead of locking out activities for a full map pass.
- Eternal Watch-class CEs: reject LGB MapRange volumes that are far from staging or absurdly large (YuYu’s CE 46 had combat radius ~560 with a fine centre). Fall back to a normal registration size so approach / Waiting cannot latch on the Lost Citadel wall.
- Pot cycle sync only uploads when the pot or spawn time changes (not on every FATE fingerprint rotate), to cut Worker request use.
- Triage / phantom job swaps wait ~4s after combat ends before changing jobs, so the game no longer rejects Chemist / White Mage with "unable to change phantom jobs".
