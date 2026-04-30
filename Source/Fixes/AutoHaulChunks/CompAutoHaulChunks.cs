using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ModFixesPack.AutoHaulChunks
{
    /// <summary>
    /// Per-building auto-haul state + gizmo. The actual chunk-flagging
    /// happens in AutoHaulChunks_Patch (Harmony postfix on Thing.SpawnSetup);
    /// this comp just stores the toggle state and exposes the gizmo button.
    /// </summary>
    public class CompAutoHaulChunks : ThingComp
    {
        public bool autoHaul = true;
        private bool initialized;

        public CompProperties_AutoHaulChunks Props => (CompProperties_AutoHaulChunks)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoHaul, "autoHaul", Props.defaultEnabled);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!initialized && !respawningAfterLoad)
            {
                autoHaul = Props.defaultEnabled;
                initialized = true;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "Auto-haul chunks",
                defaultDesc = "When enabled, stone chunks dropped near this building are automatically flagged for hauling.",
                isActive = () => autoHaul,
                toggleAction = () => autoHaul = !autoHaul,
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Haul", false)
            };
        }
    }
}
