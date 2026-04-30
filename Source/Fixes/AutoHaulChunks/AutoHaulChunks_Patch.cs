using HarmonyLib;
using RimWorld;
using Verse;

namespace ModFixesPack.AutoHaulChunks
{
    /// <summary>
    /// Harmony postfix on Thing.SpawnSetup. When a stone chunk spawns,
    /// scan a small radius for any building with CompAutoHaulChunks
    /// (with the toggle on). If found, add a Haul designation to the chunk.
    ///
    /// Only fires for fresh spawns (skips respawningAfterLoad), and bails
    /// early on non-chunks for performance — most spawns are not chunks.
    /// </summary>
    [HarmonyPatch(typeof(Thing), "SpawnSetup")]
    public static class AutoHaulChunks_Patch
    {
        public static void Postfix(Thing __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad) return;
            if (__instance == null || map == null) return;
            if (!IsStoneChunk(__instance.def)) return;

            // Already designated? Done.
            if (map.designationManager.DesignationOn(__instance, DesignationDefOf.Haul) != null)
                return;

            // Search around the chunk's spawn cell for a building with our comp.
            // Default radius 2 catches most drill/extractor drop patterns.
            int radius = 2;
            IntVec3 origin = __instance.Position;
            CellRect rect = CellRect.CenteredOn(origin, radius).ClipInsideMap(map);

            foreach (IntVec3 cell in rect)
            {
                Building building = cell.GetFirstBuilding(map);
                if (building == null) continue;

                CompAutoHaulChunks comp = building.GetComp<CompAutoHaulChunks>();
                if (comp == null) continue;

                if (!comp.autoHaul) continue;

                // Building's comp may have a custom search radius; if it's larger
                // than our default, this loop already covered. If smaller, check
                // distance.
                int compRadius = comp.Props?.searchRadius ?? radius;
                if (compRadius < radius)
                {
                    int dx = cell.x - origin.x;
                    int dz = cell.z - origin.z;
                    if (dx * dx + dz * dz > compRadius * compRadius) continue;
                }

                map.designationManager.AddDesignation(new Designation(__instance, DesignationDefOf.Haul));
                return;
            }
        }

        private static bool IsStoneChunk(ThingDef def)
        {
            if (def == null) return false;
            if (def.thingCategories == null) return false;

            for (int i = 0; i < def.thingCategories.Count; i++)
            {
                string n = def.thingCategories[i].defName;
                if (n == "StoneChunks" || n == "Chunks") return true;
            }
            return false;
        }
    }
}
