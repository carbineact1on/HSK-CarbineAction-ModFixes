# HSK Mod Fixes Pack

A collection of QoL fixes and compatibility patches for the **Hardcore SK** (HSK) modpack.

Each fix is self-contained and only activates if its target mod is loaded — no errors or overhead when a mod is missing.

## Requirements

- **RimWorld 1.5**
- **Harmony**
- **Hardcore SK Modpack** (most fixes target HSK content)

## What's Inside

### 🛠 Auto-Haul Chunks for Drills & Extractors
Stone chunks dropped by drills and extractors are automatically flagged for hauling, so pawns pick them up on their next haul cycle without manual clicking — same behavior as Quarry mod's "Hauling Mode".

- **Per-building toggle gizmo** (Quarry-style) lets you disable it on specific drills if you want
- **Auto-detects** all drills and extractors at startup — works on every mod's drill (current or future) without manual patches. Vanilla `DeepDrill`, HSK `Extractor` / `RareExtractor` / `OilExtractor`, Alpha Biomes core sample drill, Project RimFactory + VFE Mechanoids extractors are all picked up automatically.

### 🌐 Dynamic Diplomacy Fixes
Patches and tuning for the **Dynamic Diplomacy (Continued)** mod to better fit HSK's faction balance and timings.

### 🎨 MultiLang Colorful Traits — Auto-Classification
Auto-classifies every loaded trait into MultiLang's color tiers (Legendary / Good / Neutral / Bad / Terrible / Nightmare / Unique / Restrictive) based on heuristics. New traits from any mod get coloured correctly without manual tagging.

### 🏚 Real Ruins — HSK-Style Tuning
Re-tunes Real Ruins encounters for HSK difficulty: more hostiles, harder loot tables, raid-tier content matching HSK's progression curve.

### 🌍 Geological Landforms Fixes
Compat patches for Geological Landforms in HSK environments.

## Installation

1. Subscribe / clone into `RimWorld/Mods/Mod-Fixes-Pack`
2. Load **after** Core SK and the mods this pack patches
3. Start RimWorld — fixes auto-detect their target mods at startup

## How It Works

Every fix uses Harmony's `Prepare()` method or runtime guards to check if its target mod is loaded. If the target mod isn't found, the fix silently skips — no errors, no log spam, no overhead.

The C# assembly uses `[StaticConstructorOnStartup]` for one-time initialization (def database scans, mod detection) and `[HarmonyPatch]` attributes for the runtime patches.

## Reporting Issues

If you find a bug, please attach your `Player.log` and a description of what mods you have loaded. Issues that don't include logs may be closed.

## Credits

- **CarbineAction** — author / maintainer
- **Hardcore SK Team** — the modpack this targets
- Credit to original mod authors of the targets we patch (Dynamic Diplomacy, Real Ruins, MultiLang Colorful Traits, Geological Landforms, Quarry)
- Special thanks to the HSK Discord community for QoL suggestions

## License

MIT — feel free to fork, adapt, and submit pull requests.
