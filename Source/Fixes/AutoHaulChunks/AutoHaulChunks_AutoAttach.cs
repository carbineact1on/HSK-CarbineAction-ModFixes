using System.Linq;
using RimWorld;
using Verse;

namespace ModFixesPack.AutoHaulChunks
{
    /// <summary>
    /// Auto-detect drills and extractors at startup and inject
    /// CompProperties_AutoHaulChunks into their comps list, so the
    /// auto-haul behavior works on any drill mod without requiring
    /// an explicit XML patch per def.
    ///
    /// Detection heuristics (any one matches):
    ///   - def already has a CompProperties whose compClass is
    ///     CompDeepDrill (vanilla — used by DeepDrill etc.)
    ///   - def.thingClass.FullName contains "Extractor"
    ///   - def.thingClass.FullName contains "DeepDrill"
    ///   - def.defName contains "Drill" or "Extractor" (loose fallback)
    ///
    /// Defs that already have the comp (via XML pre-attach in
    /// AutoHaulChunks_AttachComp.xml) are skipped — no double-attach.
    ///
    /// Note: only affects buildings spawned AFTER game load. Drills
    /// already placed on an existing save won't have the comp until
    /// they're uninstalled + reinstalled. Standard behavior for any
    /// mod that adds comps post-load.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AutoHaulChunks_AutoAttach
    {
        static AutoHaulChunks_AutoAttach()
        {
            int attached = 0;
            int alreadyHad = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.category != ThingCategory.Building) continue;
                if (!IsDrillOrExtractor(def)) continue;

                if (def.comps == null) def.comps = new System.Collections.Generic.List<CompProperties>();

                if (def.comps.Any(c => c is CompProperties_AutoHaulChunks))
                {
                    alreadyHad++;
                    continue;
                }

                def.comps.Add(new CompProperties_AutoHaulChunks());
                attached++;
            }

            Log.Message($"[Mod Fixes Pack] AutoHaulChunks: auto-attached to {attached} drill/extractor def(s)" +
                        (alreadyHad > 0 ? $" ({alreadyHad} already had it via XML)." : "."));
        }

        private static bool IsDrillOrExtractor(ThingDef def)
        {
            // 1) Has a CompDeepDrill (vanilla DeepDrill — uses default Building thingClass)
            if (def.comps != null)
            {
                foreach (CompProperties cp in def.comps)
                {
                    if (cp == null || cp.compClass == null) continue;
                    string cn = cp.compClass.FullName ?? string.Empty;
                    if (cn == "RimWorld.CompDeepDrill") return true;
                }
            }

            // 2) thingClass FullName signal — catches custom drill/extractor classes
            //    (e.g. HSK SK.Building_Extractor, SK.Building_AdvancedExtractor)
            if (def.thingClass != null)
            {
                string tn = def.thingClass.FullName ?? string.Empty;
                if (tn.IndexOf("Extractor", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (tn.IndexOf("DeepDrill", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            // 3) defName fallback — loose pattern catches mods that name their
            //    drill/extractor defs predictably (e.g. "AB_CoreSampleDrill")
            string dn = def.defName ?? string.Empty;
            if (dn.IndexOf("Drill", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (dn.IndexOf("Extractor", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }
    }
}
