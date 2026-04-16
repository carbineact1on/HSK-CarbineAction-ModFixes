using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ModFixesPack
{
    /// <summary>
    /// Entry point for the patch pack. Applies all Harmony patches on startup.
    /// Individual fixes use Harmony's Prepare() method to conditionally activate
    /// only if their target mod is loaded — no errors or overhead if a mod is missing.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Core
    {
        public const string HarmonyId = "ModFixesPack";

        static Core()
        {
            Log.Message("[Mod Fixes Pack] Loading patches...");
            ModState.DetectLoadedMods();

            try
            {
                var harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[Mod Fixes Pack] Patches applied successfully.");
            }
            catch (Exception e)
            {
                Log.Error("[Mod Fixes Pack] Failed to apply patches: " + e);
            }
        }
    }
}
