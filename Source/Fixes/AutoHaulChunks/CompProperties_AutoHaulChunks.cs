using Verse;

namespace ModFixesPack.AutoHaulChunks
{
    /// <summary>
    /// Comp properties for auto-haul behavior on drills/extractors.
    ///
    /// Attach this comp to any building's <comps> list (via XML) to make
    /// stone chunks dropped on or near the building automatically receive
    /// a Haul designation, so pawns pick them up on their next haul cycle.
    /// Mirrors Quarry mod's "Hauling Mode" behavior, but as a reusable Comp
    /// any modder can attach via PatchOperation — no DLL needed.
    ///
    /// XML usage:
    ///     &lt;comps&gt;
    ///         &lt;li Class="ModFixesPack.AutoHaulChunks.CompProperties_AutoHaulChunks" /&gt;
    ///     &lt;/comps&gt;
    /// </summary>
    public class CompProperties_AutoHaulChunks : CompProperties
    {
        /// <summary>Default state of the per-building toggle. Defaults to enabled.</summary>
        public bool defaultEnabled = true;

        /// <summary>
        /// Radius (in cells) around the building to flag chunks dropped within.
        /// Default 2 covers most drill/extractor drop patterns.
        /// </summary>
        public int searchRadius = 2;

        public CompProperties_AutoHaulChunks()
        {
            compClass = typeof(CompAutoHaulChunks);
        }
    }
}
