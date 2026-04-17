using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;

namespace ModFixesPack.Fixes
{
    /// <summary>
    /// Fix: Real Ruins mountain avoidance + relocation for ALL ruin types.
    ///
    /// Prefixes <c>RealRuins.RuinsScatterer.Scatter</c>, which is the common
    /// funnel for every ruin placement (Small/Background, Medium, Large, POI).
    /// For each call:
    /// 1. Figure out where the ruin would land (via <c>options.overridePosition</c>
    ///    if set, otherwise <c>rp.rect.CenterCell</c>).
    /// 2. Score that cell by mountain fraction (thick roof + natural rock) in a
    ///    15-cell radius.
    /// 3. If already low-mountain, pass through. If not, actively sample up to
    ///    <see cref="SearchAttempts"/> candidate cells and pick the one with the
    ///    least mountain overlap.
    /// 4. To relocate, zero <c>overridePosition</c> and set <c>rp.rect</c> to
    ///    a 1x1 rect at the chosen cell. <c>BlueprintTransferUtility</c> will
    ///    center the blueprint on that cell. Map-edge clamping in the mod
    ///    handles cases where the blueprint would spill off the map.
    /// 5. If no candidate beats the reject threshold, skip the spawn silently.
    /// </summary>
    public static class RealRuinsFixes
    {
        private const int CheckRadius = 15;
        private const float GoodThreshold = 0.15f;
        private const float MaxMountainFraction = 0.4f;
        private const int SearchAttempts = 30;

        // Cached reflection handle for ScatterOptions.overridePosition
        // (ScatterOptions is internal to RealRuins, so we can't bind the type
        // statically — we look it up once on first use and reuse thereafter).
        private static FieldInfo _overridePositionField;

        [HarmonyPatch]
        public static class Patch_RuinsScatterer_Scatter
        {
            static bool Prepare() => ModState.RealRuins;

            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("RealRuins.RuinsScatterer");
                if (type == null)
                {
                    Log.Warning("[Mod Fixes Pack] Could not find RealRuins.RuinsScatterer type");
                    return null;
                }
                return AccessTools.Method(type, "Scatter");
            }

            // Second parameter of Scatter is the internal ScatterOptions. Since we can't
            // reference that type directly, we access it positionally via __1.
            static bool Prefix(ref ResolveParams rp, object __1)
            {
                var map = BaseGen.globalSettings.map;
                if (map == null) return true;

                var options = __1;
                var currentCenter = GetPlacementCenter(rp, options);
                var currentScore = MountainFraction(currentCenter, map);

                // Already in a clean area — let the mod do its thing.
                if (currentScore <= GoodThreshold) return true;

                // Search for a better spot.
                var best = currentCenter;
                var bestScore = currentScore;

                for (int i = 0; i < SearchAttempts; i++)
                {
                    var candidate = CellFinder.RandomNotEdgeCell(10, map);
                    var score = MountainFraction(candidate, map);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        if (score <= GoodThreshold) break;
                    }
                }

                if (bestScore > MaxMountainFraction)
                {
                    Log.Message($"[Mod Fixes Pack] RealRuins: skipping ruin spawn (best candidate was {bestScore:P0} mountain).");
                    return false;
                }

                if (best != currentCenter)
                {
                    // Zero overridePosition so BlueprintTransferUtility falls back to
                    // the rect-based centering logic, then point the rect at our chosen cell.
                    SetOverridePosition(options, IntVec3.Zero);
                    rp.rect = new CellRect(best.x, best.z, 1, 1);
                    Log.Message($"[Mod Fixes Pack] RealRuins: relocated ruin from {currentCenter} ({currentScore:P0} mountain) to {best} ({bestScore:P0} mountain).");
                }

                return true;
            }
        }

        /// <summary>
        /// Where would the ruin currently land? Respects overridePosition if set
        /// (POI case), otherwise uses the rect's center (scatter case).
        /// </summary>
        private static IntVec3 GetPlacementCenter(ResolveParams rp, object options)
        {
            var overridePos = GetOverridePosition(options);
            if (overridePos != IntVec3.Zero) return overridePos;
            return rp.rect.CenterCell;
        }

        private static IntVec3 GetOverridePosition(object options)
        {
            if (options == null) return IntVec3.Zero;
            var field = GetOverridePositionField(options);
            if (field == null) return IntVec3.Zero;
            try { return (IntVec3)field.GetValue(options); }
            catch { return IntVec3.Zero; }
        }

        private static void SetOverridePosition(object options, IntVec3 value)
        {
            if (options == null) return;
            var field = GetOverridePositionField(options);
            if (field == null) return;
            try { field.SetValue(options, value); }
            catch { /* non-fatal — relocation still applies via rp.rect */ }
        }

        private static FieldInfo GetOverridePositionField(object options)
        {
            if (_overridePositionField != null) return _overridePositionField;
            _overridePositionField = AccessTools.Field(options.GetType(), "overridePosition");
            if (_overridePositionField == null)
                Log.Warning("[Mod Fixes Pack] ScatterOptions.overridePosition field not found — POI relocation disabled.");
            return _overridePositionField;
        }

        private static float MountainFraction(IntVec3 center, Map map)
        {
            int total = 0;
            int mountain = 0;
            foreach (var cell in GenRadial.RadialCellsAround(center, CheckRadius, true))
            {
                if (!cell.InBounds(map)) continue;
                total++;
                if (IsMountainCell(cell, map)) mountain++;
            }
            return total == 0 ? 0f : (float)mountain / total;
        }

        private static bool IsMountainCell(IntVec3 cell, Map map)
        {
            var roof = map.roofGrid.RoofAt(cell);
            if (roof != null && roof.isThickRoof) return true;
            var edifice = cell.GetEdifice(map);
            if (edifice?.def?.building?.isNaturalRock == true) return true;
            return false;
        }

        /// <summary>
        /// World-tile-level filter + relocation: when Real Ruins tries to place a POI icon
        /// (Factory, MilitaryBaseSmall, City, Camp, etc.) on a Mountainous or Impassable
        /// world tile, this patch searches neighbor tiles in concentric rings for a valid
        /// lower-hilliness spot and rewrites the POI's target tile to that neighbor before
        /// letting the original placement run. Only skips if no suitable neighbor exists
        /// within <see cref="MaxRelocateRings"/> steps.
        /// </summary>
        [HarmonyPatch]
        public static class Patch_RealRuinsPOIFactory_CreatePOI
        {
            // How many BFS rings to search when looking for a replacement tile.
            // 10 rings ≈ ~10 tile travel distance on the world map — usually plenty.
            private const int MaxRelocateRings = 10;

            static bool Prepare() => ModState.RealRuins;

            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("RealRuins.RealRuinsPOIFactory");
                if (type == null)
                {
                    Log.Warning("[Mod Fixes Pack] Could not find RealRuins.RealRuinsPOIFactory type");
                    return null;
                }
                return AccessTools.Method(type, "CreatePOI");
            }

            // First parameter is PlanetTileInfo (internal). We only need its public `tile` int
            // — read/write via reflection since we can't bind the type statically.
            static bool Prefix(object __0, ref bool __result)
            {
                if (__0 == null) return true;

                var tileField = AccessTools.Field(__0.GetType(), "tile");
                if (tileField == null) return true;

                int tile;
                try { tile = (int)tileField.GetValue(__0); }
                catch { return true; }

                if (tile < 0 || tile >= Find.WorldGrid.TilesCount) return true;

                var hilliness = Find.WorldGrid[tile].hilliness;
                if (hilliness != Hilliness.Mountainous && hilliness != Hilliness.Impassable)
                    return true; // already a sensible tile — original runs unchanged

                // Try to find a nearby valid tile to relocate to.
                var replacement = FindNearbyValidTile(tile, MaxRelocateRings);
                if (replacement >= 0)
                {
                    try
                    {
                        tileField.SetValue(__0, replacement);
                        Log.Message($"[Mod Fixes Pack] RealRuins: relocated POI from tile {tile} ({hilliness}) to tile {replacement} (hilliness: {Find.WorldGrid[replacement].hilliness}).");
                        return true; // let the original CreatePOI run with the new tile
                    }
                    catch
                    {
                        // Fall through to the skip path if write fails for any reason.
                    }
                }

                Log.Message($"[Mod Fixes Pack] RealRuins: rejecting POI on tile {tile} ({hilliness}) — no suitable neighbor within {MaxRelocateRings} rings.");
                __result = false;
                return false;
            }
        }

        /// <summary>
        /// BFS outward from <paramref name="origin"/> over the world hex grid. Returns
        /// the first tile that:
        ///  - Has hilliness less than Mountainous (Flat / SmallHills / LargeHills)
        ///  - Passes RimWorld's <see cref="TileFinder.IsValidTileForNewSettlement"/> check
        ///    (not water / not impassable / not already occupied by another world object)
        /// Returns -1 if none found within <paramref name="maxRings"/>.
        /// Mountain tiles are traversable during the search (we can pass through them)
        /// but are never chosen as the destination.
        /// </summary>
        private static int FindNearbyValidTile(int origin, int maxRings)
        {
            var visited = new HashSet<int> { origin };
            var queue = new Queue<int>();
            var distances = new Dictionary<int, int> { [origin] = 0 };
            queue.Enqueue(origin);

            var neighbors = new List<int>();
            var worldGrid = Find.WorldGrid;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int dist = distances[current];
                if (dist >= maxRings) continue;

                neighbors.Clear();
                worldGrid.GetTileNeighbors(current, neighbors);
                foreach (var n in neighbors)
                {
                    if (!visited.Add(n)) continue;
                    distances[n] = dist + 1;

                    var h = worldGrid[n].hilliness;
                    if (h == Hilliness.Mountainous || h == Hilliness.Impassable)
                    {
                        // Pass through, don't pick — mountain neighbors can still lead
                        // us toward a non-mountain tile on the far side.
                        queue.Enqueue(n);
                        continue;
                    }

                    // Candidate — validate via RimWorld's own settlement rules.
                    if (!TileFinder.IsValidTileForNewSettlement(n, null)) continue;
                    if (Find.WorldObjects.AnyWorldObjectAt(n)) continue;

                    return n;
                }
            }

            return -1;
        }
    }
}
