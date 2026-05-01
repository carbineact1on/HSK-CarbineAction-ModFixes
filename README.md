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
- **Walks category parent chain** so it catches chunk subcategories from any mod (e.g. Minerals' Soft / Hard / UltraHard stone chunks), not just vanilla `StoneChunks`.

### 💎 Minerals Mod Fixes
Two patches for the **Minerals** mod (`zacharyfoster.minerals`):

- **Chunks** — Minerals' chunk subcategories (`SoftStoneChunks` / `HardStoneChunks` / `UltraHardStoneChunks`) used `Inherit="False"`, dropping the inherited `StoneChunks` tag. Restored, so Claystone / Mudstone / Pegmatite / Dunite chunks now appear in vanilla "Stone Chunks" storage filters AND their hardness subcategories.
- **Clay blocks** — `ZF_BlocksClay` had `<thingCategories IsNull="true"/>` which stripped its inherited `StoneBlocks` category, leaving clay blocks with no storage destination at all. Restored, so clay blocks now appear under Resources → StoneBlocks alongside every other stone block. No more "no place to store: clay blocks" errors.
- Auto-skips if Minerals isn't installed.

### 🏛 KCSG Symbol Fixes
KCSG (inside Vanilla Expanded Framework) spams "mod X contains N missing symbols" warnings on boot when enemy-base layouts reference symbol names nobody defined — causing gaps in furniture when imperial bases / aerodromes / nobles' manors / deep-tribal camps generate.

Ships **1,613 auto-generated SymbolDefs** covering every missing reference across the modlist. Since KCSG reads SymbolDefs globally, one file covers every KCSG-using mod.

Verified in-game reductions:
- Vanilla Base Generation Expanded: 765 → 5
- VFE Deserters (HSK): 283 → 1
- Ancient Mining Industry: 89 → 1
- Alpha Memes: 33 → 0
- Alpha Genes: 28 → 0
- VFE Empire: 11 → 0
- VFE Insectoids 2 (HSK): 3 → 0
- **Total: ~1,200 → 7 missing symbols (99.4% reduction).** Enemy bases now spawn with intended furniture instead of gap-toothed rooms.

### 🌐 Dynamic Diplomacy Fixes
Patches and tuning for the **Dynamic Diplomacy (Continued)** mod to better fit HSK's faction balance and timings — terrain validation (no battles on impassable/cave/ocean/extreme-temp tiles), shambler/infection deadlock fix, 7-day battle timeout failsafe, HSK-tuned event rates.

### 🎨 MultiLang Colorful Traits — Auto-Classification
Auto-classifies every loaded trait into MultiLang's color tiers (Legendary / Good / Neutral / Bad / Terrible / Nightmare / Unique / Restrictive) based on heuristics. New traits from any mod get coloured correctly without manual tagging. Roughly 30% of HSK traits were colored before → ~100% after.

### 🏚 Real Ruins — HSK-Style Tuning
Re-tunes Real Ruins encounters for HSK difficulty: more hostiles, harder loot tables, raid-tier content matching HSK's progression curve. Also relocates POI icons off Mountainous/Impassable tiles (no more Factory/Military Base/Camp icons stuck unreachable in mountains).

### 🌍 Geological Landforms Fixes
Compat patches for Geological Landforms — terrain topology checks used by Dynamic Diplomacy validation.

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
