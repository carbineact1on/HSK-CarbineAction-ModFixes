using System;
using HarmonyLib;
using Verse;

namespace ModFixesPack.Fixes
{
    /// <summary>
    /// HSK-style rebalance for Real Ruins: more hostiles, harsher enemies, much
    /// harder to find good loot. Matches HSK's survival-first tone — ruins
    /// become actual dangerous expeditions instead of free loot caches.
    ///
    /// Only activates when BOTH Real Ruins AND HSK are loaded. For non-HSK
    /// modpacks, Real Ruins keeps its own defaults.
    ///
    /// Runs once at startup via [StaticConstructorOnStartup] after Real Ruins
    /// has finished loading its ModSettings (load order is enforced by
    /// About.xml loadAfter). We overwrite the runtime in-memory values on the
    /// static settings object — values remain settable via Real Ruins' own UI
    /// afterwards, but next game launch we apply the tuning again as baseline.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RealRuinsHSKTuning
    {
        static RealRuinsHSKTuning()
        {
            if (!ModState.RealRuins || !ModState.HSK) return;

            try
            {
                Apply();
            }
            catch (Exception e)
            {
                Log.Warning("[Mod Fixes Pack] Real Ruins HSK tuning failed: " + e.Message);
            }
        }

        private static void Apply()
        {
            var modSettingsType = AccessTools.TypeByName("RealRuins.RealRuins_ModSettings");
            if (modSettingsType == null)
            {
                Log.Warning("[Mod Fixes Pack] Could not find RealRuins.RealRuins_ModSettings — HSK tuning skipped.");
                return;
            }

            var scatterOpts = AccessTools.Field(modSettingsType, "defaultScatterOptions")?.GetValue(null);
            if (scatterOpts == null)
            {
                Log.Warning("[Mod Fixes Pack] RealRuins defaultScatterOptions null — HSK tuning skipped.");
                return;
            }

            var optsType = scatterOpts.GetType();

            // --- Hostiles: more common, harder to deal with ---
            SetField(optsType, scatterOpts, "hostileChance", 0.35f);        // 10% → 35%
            SetField(optsType, scatterOpts, "trapChance",    0.004f);       // 0.1% → 0.4%

            // --- Loot: much harder to get anything valuable ---
            // Scavenging bumped so most high-value items are already gone before you arrive.
            // deteriorationMultiplier deliberately NOT tuned — stays at Real Ruins' default (0.0).
            // Per-tile integrity decay (edge vs center) still applies naturally; double-hitting
            // with an extra decay multiplier would make the few leftover items useless.
            SetField(optsType, scatterOpts, "scavengingMultiplier", 1.75f);  // 1.0 → 1.75
            SetField(optsType, scatterOpts, "itemCostLimit",        400);    // 1000 → 400

            // --- Forces get 50% stronger ---
            SetField(modSettingsType, null, "forceMultiplier", 1.5f);           // 1.0 → 1.5

            // --- Hard ceiling on ruin value (no billionaire loot) ---
            SetField(modSettingsType, null, "ruinsCostCap",    300000f);        // 1e9 → 300k

            // --- More POIs are truly abandoned (no faction hesitance) ---
            var planetaryOpts = AccessTools.Field(modSettingsType, "planetaryRuinsOptions")?.GetValue(null);
            if (planetaryOpts != null)
            {
                SetField(planetaryOpts.GetType(), planetaryOpts, "abandonedLocations", 0.5f); // 0.2 → 0.5
            }

            Log.Message("[Mod Fixes Pack] Real Ruins: applied HSK-style tuning — more hostiles, harder loot.");
        }

        private static void SetField(Type type, object target, string name, object value)
        {
            var field = AccessTools.Field(type, name);
            if (field == null)
            {
                Log.Warning($"[Mod Fixes Pack] RealRuins HSK tuning: field '{name}' not found on {type.Name}.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
