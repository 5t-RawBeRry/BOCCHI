# 4.0.2.14

### Fixes
- Illegal Mode: leave **Waiting for CE** once Battle is underway (CE enemies / combat / short grace) even if player EventId is unset, so **In CE** and BOCCHI AI CE can enable
- Illegal Mode: after pot FATEs, wait for **Cache Me If You Can** / Magical Elixir instead of requiring the buff the instant the FATE ends (was skipping chest farm and running to the next FATE)
- Treasure Hunt: Ninja Hide Knowledge threshold uses the current cap of **40** (was still clamped to 28)
- Pots + Treasure: stop crashing the main window when a South Horn pot FATE appears with event-drop icons (`'124' cannot be greater than 120`)
