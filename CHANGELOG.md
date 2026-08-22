# 4.1.0.8

### Fixes
- **Illegal Mode:** mount right away when heading to farther FATEs or Critical Encounters (e.g. Company of Stone) instead of walking halfway from base camp first.
- **Eternal Watch** and similar CEs: travel no longer stops outside the ring, Returns, and retries. Walks to the wait area on the ground instead of an unreachable spot above it.
- **Pot chests:** if a chest spawns a short walk from the expected spot, walk to the live chest instead of standing on the wrong place.
- **Treasure Hunt:** default **Empty pad check distance** is now **60** yalms (was 25). Your saved setting is unchanged.
- **Treasure Hunt:** skips the Unhallowed Hamlet basement coffer (stairs) — bad pathing and high death risk.
- **Wrath + Phantom Black Mage:** Occult Toad stays off (was cast repeatedly for no benefit).

### Support
- `/bocchi debug ce` (optional id, e.g. `ce 46`): run where travel stops wrong and paste the output for bug reports.
