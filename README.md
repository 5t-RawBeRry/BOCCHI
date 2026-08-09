# BOCCHI

**Better Occult Crescent & Chest Helper Interface**

A Dalamud plugin for Occult Crescent (South Horn / North Horn). It helps you see what’s up, get there, open coffers, and — if you want — run the zone on autopilot.

Repo install URL:

```text
https://raw.githubusercontent.com/OhKannaDuh/plugins/refs/heads/master/manifest.json
```

<a href='https://ko-fi.com/kagekazu' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi5.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

---

## What it can do

### World overview
- Live **FATE** and **Critical Encounter** lists with rewards
- **Path** (aethernet + walk) or **Teleport** (aethernet hop only) to an activity
- Flag activities on the map

### Illegal Mode (full autopilot)
- Pick and travel to FATEs and/or Critical Encounters
- Prefer / wait for **Magic Pot** FATEs and farm **pot chests** (Magical Elixir + compass hints)
- Optional **BOCCHI AI** combat (BossMod / BossMod Reborn) for FATE and CE fights
- Auto-mount, Return to camp, repair, Treasure Sight at camp
- Optional auto treasure hunt between activities
- Phantom job leveling helper

### Pots & Treasure
- Dedicated loop: pot windows ↔ treasure hunting without full Illegal Mode

### Treasure Hunter
- Planned coffer routes (bronze / silver) with aethernet-aware pathing
- Treasure Sight during the hunt; pause / resume / recalculate
- Radar lines to nearby coffers and carrots
- **Carrot Hunt:** authored map tour with nearest-next routing and aethernet hops; Fortune Carrot → bunny gold chest

### Mob Farmer
- Pull and clear selected mob packs in a farm area (separate from Illegal Mode)

### Buffs
- Apply knowledge-crystal style buffs (Apply / Stop from the main window)

### Trackers
- Experience / gold / silver per hour style session stats

### Background helpers (always on in Occult Crescent)
- Anonymous pot spawn sync with other BOCCHI users on the same instance
- Anonymous chewed-carrot location sync (helps keep the carrot map accurate)

### Languages
- English, Japanese, Korean, Simplified Chinese

---

## Requirements

| Plugin | Needed for |
|---|---|
| **vnavmesh** | Pathfinding / walking |
| **Lifestream** | Aethernet teleports |
| **BossMod** or **BossMod Reborn** | BOCCHI AI combat (optional) |
| **Wrath Combo** or **Rotation Solver Reborn** | Autorotation (optional) |

---

## Quick start

1. Install from the repo URL above in Dalamud.
2. Enter South or North Horn.
3. Open the window with `/bocchi` (alias `/och`).
4. Use the **World** panel for manual path/teleport, or start **Illegal Mode** / **Treasure Hunt** / **Pots & Treasure** from their sections.
5. Configure behavior under the plugin config (gear icon).

Optional: enable **Open BOCCHI on Occult Crescent entry** in UI settings.

---

## Commands

Prefix: `/bocchi` (aliases: `/och`, `/occultcrescenthelper`)

| Command | What it does |
|---|---|
| `/bocchi` | Open main window |
| `/bocchi config` | Open config (`cfg`, `c`) |
| `/bocchi buff` | Start/stop a buff run |
| `/bocchi tp [fate\|ce\|pot]` | Teleport toward nearest matching activity |
| `/bocchi illegal [on\|off\|toggle]` | Control Illegal Mode (no arg opens the window) |
| `/bocchi cmd flag-active-ce` | Flag a registering CE |
| `/bocchi cmd flag-active-fate` | Flag a FATE |
| `/bocchi reload-translations` | Reload translations (`rt`) |

---

## Notes

- Automation modes are **exclusive** — starting one stops the others (Emergency Stop is available in the UI).
- Treasure and Carrot Hunt use the built-in authored maps (no optional live-route sharing).
- Pot spawn sync and carrot-location sync run automatically while you are in Occult Crescent.
