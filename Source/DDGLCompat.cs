using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DDGLCompat
{
    [StaticConstructorOnStartup]
    public static class DDGLCompat
    {
        public static bool GeologicalLandformsLoaded { get; private set; }
        public static bool DynamicDiplomacyLoaded { get; private set; }

        static DDGLCompat()
        {
            DynamicDiplomacyLoaded = ModsConfig.IsActive("nilchei.dynamicdiplomacycontinued");
            GeologicalLandformsLoaded = ModsConfig.IsActive("m00nl1ght.GeologicalLandforms");

            if (!DynamicDiplomacyLoaded)
            {
                Log.Message("[DD-GL Compat] Dynamic Diplomacy not loaded, patch inactive.");
                return;
            }

            if (!GeologicalLandformsLoaded)
            {
                Log.Message("[DD-GL Compat] Geological Landforms not loaded, using vanilla-only checks.");
            }

            try
            {
                var harmony = new Harmony("CarbineAction.DDGLCompat");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[DD-GL Compat] Harmony patches applied successfully.");
            }
            catch (Exception e)
            {
                Log.Error("[DD-GL Compat] Failed to apply Harmony patches: " + e);
            }
        }

        /// <summary>
        /// Checks if a tile is unsuitable for NPC battle simulation due to pathing issues.
        /// </summary>
        public static bool IsTileProblematic(int tileId)
        {
            if (tileId < 0 || Find.World == null) return true;

            var tile = Find.World.grid[tileId];
            if (tile == null) return true;

            // Impassable = mountain map, battles can't happen
            if (tile.hilliness == Hilliness.Impassable) return true;

            // Ocean/lake tiles also can't host battles
            if (tile.WaterCovered) return true;

            if (GeologicalLandformsLoaded)
            {
                return IsGLTileProblematic(tileId);
            }

            return false;
        }

        /// <summary>
        /// Separated so JIT doesn't try to load GL types if GL isn't present.
        /// </summary>
        private static bool IsGLTileProblematic(int tileId)
        {
            try
            {
                var tileInfo = GeologicalLandforms.WorldTileInfo.Get(tileId);
                if (tileInfo == null) return false;

                switch (tileInfo.Topology)
                {
                    case GeologicalLandforms.Topology.CliffAllSides:
                    case GeologicalLandforms.Topology.CliffValley:
                    case GeologicalLandforms.Topology.CliffThreeSides:
                    case GeologicalLandforms.Topology.CaveEntrance:
                    case GeologicalLandforms.Topology.CaveTunnel:
                    case GeologicalLandforms.Topology.Ocean:
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                Log.Warning("[DD-GL Compat] Error checking GL tile: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Checks if a settlement's tile is unsuitable for DD conquest events.
        /// Prevents conquest on terrain where NPC battle sim would fail.
        /// </summary>
        public static bool IsSettlementProblematic(Settlement settlement)
        {
            if (settlement == null || settlement.Faction == null) return true;
            return IsTileProblematic(settlement.Tile);
        }
    }

    /// <summary>
    /// Postfix DD's FindSuitableTile to reject tiles with problematic landforms.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_FindSuitableTile
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("DynamicDiplomacy.UtilsTileCellFinder");
            if (type == null)
            {
                Log.Warning("[DD-GL Compat] Could not find DynamicDiplomacy.UtilsTileCellFinder type");
                return null;
            }
            return AccessTools.Method(type, "FindSuitableTile");
        }

        static bool Prepare() => DDGLCompat.DynamicDiplomacyLoaded;

        static void Postfix(ref int __result, int nearTile)
        {
            if (__result < 0) return;

            if (DDGLCompat.IsTileProblematic(__result))
            {
                Log.Message($"[DD-GL Compat] Rejecting tile {__result} (unsuitable terrain), retrying...");

                var type = AccessTools.TypeByName("DynamicDiplomacy.UtilsTileCellFinder");
                var fallbackMethod = AccessTools.Method(type, "FindSuitableTileFixedModerateTempFirst");

                if (fallbackMethod != null)
                {
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        int minDist = 4 + attempt * 4;
                        int maxDist = 10 + attempt * 8;
                        var args = new object[] { nearTile, minDist, maxDist, true };
                        int newTile = (int)fallbackMethod.Invoke(null, args);

                        if (newTile > 0 && !DDGLCompat.IsTileProblematic(newTile))
                        {
                            Log.Message($"[DD-GL Compat] Found replacement tile {newTile} (attempt {attempt + 1})");
                            __result = newTile;
                            return;
                        }
                    }
                }

                Log.Message("[DD-GL Compat] No suitable tile found after retries, event will be dropped");
                __result = -1;
            }
        }
    }

    /// <summary>
    /// Postfix DD's RandomSettlement to filter out orbital/space settlements.
    /// Fixes the Odyssey orbital trader bug where orbital stations get grounded after conquest.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RandomSettlement
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("DynamicDiplomacy.IncidentWorker_NPCConquest");
            if (type == null) return null;
            return AccessTools.Method(type, "RandomSettlement");
        }

        static bool Prepare() => DDGLCompat.DynamicDiplomacyLoaded;

        static void Postfix(ref Settlement __result)
        {
            if (__result == null) return;

            if (DDGLCompat.IsSettlementProblematic(__result))
            {
                Log.Message($"[DD-GL Compat] Rejecting settlement {__result.Name} (orbital/unsuitable), retrying...");
                __result = FindValidSettlement();
            }
        }

        static Settlement FindValidSettlement()
        {
            var validSettlements = Find.WorldObjects.SettlementBases
                .Where(s => s.Faction != null
                    && !s.Faction.IsPlayer
                    && s.Faction.def.settlementGenerationWeight > 0f
                    && !s.def.defName.Equals("City_Faction")
                    && !s.def.defName.Equals("City_Abandoned")
                    && !s.def.defName.Equals("City_Ghost")
                    && !s.def.defName.Equals("City_Citadel")
                    && !DDGLCompat.IsSettlementProblematic(s))
                .ToList();

            return validSettlements.RandomElementWithFallback();
        }
    }

    /// <summary>
    /// Patch MapParentNPCArena.Tick to fix the shambler/infection bug where battles never end.
    /// Add proper faction check — pawns that switched factions (e.g., became shamblers)
    /// should count as "gone" from their original faction.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ArenaFactionCheck
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("DynamicDiplomacy.MapParentNPCArena");
            if (type == null) return null;
            return AccessTools.Method(type, "Tick");
        }

        static bool Prepare() => DDGLCompat.DynamicDiplomacyLoaded;

        // We don't modify Tick directly - too complex. Instead add a Prefix that
        // pre-cleans the lhs/rhs lists of pawns that are no longer in their factions.
        static void Prefix(object __instance)
        {
            try
            {
                var type = __instance.GetType();
                var lhsField = type.GetField("lhs");
                var rhsField = type.GetField("rhs");
                var attackerField = type.GetField("attackerFaction");
                var defenderField = type.GetField("defenderFaction");
                var tickCreatedField = type.GetField("tickCreated");
                var isCombatEndedField = type.GetField("isCombatEnded");

                if (lhsField == null || rhsField == null || attackerField == null || defenderField == null) return;

                var lhs = lhsField.GetValue(__instance) as List<Pawn>;
                var rhs = rhsField.GetValue(__instance) as List<Pawn>;
                var attacker = attackerField.GetValue(__instance) as Faction;
                var defender = defenderField.GetValue(__instance) as Faction;

                if (lhs == null || rhs == null) return;

                // Hard timeout safeguard: if battle has been going for 7 game days, force it to end
                if (tickCreatedField != null && isCombatEndedField != null)
                {
                    int tickCreated = (int)tickCreatedField.GetValue(__instance);
                    int ticksElapsed = Find.TickManager.TicksGame - tickCreated;
                    // 7 days = 420000 ticks — longer than DD's 120000 timeout
                    // This is a hard failsafe in case the normal timeout doesn't trigger
                    if (ticksElapsed > 420000)
                    {
                        bool isCombatEnded = (bool)isCombatEndedField.GetValue(__instance);
                        if (!isCombatEnded)
                        {
                            Log.Warning($"[DD-GL Compat] Battle hard timeout after 7 days — forcing end");
                            isCombatEndedField.SetValue(__instance, true);
                            // Let DD's existing OnTimeOut logic handle it next tick
                        }
                    }
                }

                // Clean dead/defected pawns from faction lists so DD's win check works correctly
                // This prevents the shambler bug where infected pawns keep the battle "active"
                if (attacker != null)
                    lhs.RemoveAll(p => p == null || p.Destroyed || (p.Faction != null && p.Faction != attacker));
                if (defender != null)
                    rhs.RemoveAll(p => p == null || p.Destroyed || (p.Faction != null && p.Faction != defender));
            }
            catch (Exception e)
            {
                Log.Warning("[DD-GL Compat] Error in arena faction check: " + e.Message);
            }
        }
    }
}
