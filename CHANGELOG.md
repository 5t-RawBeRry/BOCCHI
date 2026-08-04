# 4.0.2.6

### Fixes
- BOCCHI AI (BossMod / BMR): rebuild preset on base job change (melee OnHitbox vs ranged 15) without retoggling Illegal Mode
- BOCCHI AI: use base ClassJob (not Occult Crescent phantom job) for melee / range; always include GoToPositional
- BOCCHI AI: create preset via BossMod IPC directly (no silent no-op through DynamicRotation); delete+recreate so BMR/VBM pick up updates
