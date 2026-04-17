using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ModFixesPack.Fixes
{
    /// <summary>
    /// Auto-extends MultiLang Colorful Traits to cover every TraitDef in the modlist
    /// — not just the ones on its hand-curated XML lists (Core / HSK / RJW / AlienRaces / CE).
    ///
    /// MultiLang's workflow requires the player to press "Dump", open ColorfulTraits_Dump.txt,
    /// manually assign uncolored traits to one of 5 tiers in XML, and restart. For big modlists
    /// with many custom trait mods this is hours of tedious work.
    ///
    /// This fix runs after MultiLang has done its hand-curated coloring and fills the gaps:
    ///   - Skips any TraitDegreeData whose label is already wrapped in a color tag (MultiLang
    ///     handled it — its manual classification always wins).
    ///   - Scores every other degree by its mechanical data (skillGains, statOffsets/Factors,
    ///     the parent TraitDef's disabledWorkTypes/Tags, and per-degree random mental states).
    ///   - Maps the score to one of MultiLang's 5 tiers and wraps the label in the matching
    ///     hex color so the visual style stays consistent with MultiLang's manual entries.
    ///
    /// Gated on ModState.MultiLangColorfulTraits — inactive if the mod isn't loaded. Non-HSK
    /// modpacks also get the benefit automatically.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MultiLangColorfulTraitsFixes
    {
        // MultiLang's full palette (from its ColorBase.xml) — all 8 colors. The 5 "main"
        // ones (Unique/Good/Neutral/Bad/Terrible) are what MultiLang's own lists use. The
        // 3 "extras" (Red/Green/Blue, pure RGB) are defined in ColorBase.xml but unused
        // by MultiLang's curated defs — we appropriate them for the upper Legendary /
        // lower Nightmare / orthogonal Restrictive tiers to cover the full range of trait
        // intensities auto-detection surfaces.
        private const string ColorUnique      = "0000FF"; // blue         — flavor / quirk mechanic (from ColorBlue)
        private const string ColorGood        = "9AB973"; // muted green  — solid positive
        private const string ColorNeutral     = "FFFF99"; // pale yellow  — defined but unused (we skip)
        private const string ColorBad         = "FF9966"; // salmon       — noticeable downside
        private const string ColorTerrible    = "ED2939"; // red          — serious downside
        private const string ColorLegendary   = "00FF00"; // pure green   — exceptional positive (from ColorGreen)
        private const string ColorNightmare   = "FF0000"; // pure red     — worst of the worst (from ColorRed)
        private const string ColorRestrictive = "9370DB"; // purple       — disables work / has hard requirements (from ColorUnique)

        // Score thresholds. Tuned against vanilla traits so known-good (Hard worker, Strong,
        // Tough) land in Good and known-bad (Lazy, Slowpoke, Pyromaniac) land in Bad/Terrible.
        private const int LegendaryThreshold =  7;
        private const int GoodThreshold      =  3;
        private const int BadThreshold       = -3;
        private const int TerribleThreshold  = -5;
        private const int NightmareThreshold = -7;

        // Stats where a POSITIVE offset/factor is beneficial to the pawn.
        private static readonly HashSet<string> BeneficialStats = new HashSet<string>
        {
            "WorkSpeedGlobal", "MoveSpeed", "MeleeHitChance", "MeleeDodgeChance",
            "ShootingAccuracyPawn", "ShootingAccuracy", "GlobalLearningFactor",
            "PsychicSensitivity", "MentalBreakThreshold_Inverse",
            "SocialImpact", "NegotiationAbility", "TradePriceImprovement",
            "SurgerySuccessChanceFactor", "MedicalTendQuality", "ConstructionSpeed",
            "PlantWorkSpeed", "MiningSpeed", "AnimalGatherYield", "AnimalGatherSpeed",
            "TameAnimalChance", "TrainAnimalChance", "HuntingStealth",
            "RestRateMultiplier", "ImmunityGainSpeed",
            "MaxHitPoints", "ComfyTemperatureMax",
            "Beauty", "Comfort", "PawnBeauty", "WorkSpeed", "ResearchSpeed",
            "HackingSpeed", "PsyfocusGainMultiplier", "MeleeDamageFactor"
        };

        // Stats where a POSITIVE offset is harmful to the pawn (so negative = beneficial).
        private static readonly HashSet<string> HarmfulIfPositiveStats = new HashSet<string>
        {
            "HungerRateMultiplier", "AimingDelayFactor", "MentalBreakThreshold",
            "PainShockThreshold_Inverse"
        };

        static MultiLangColorfulTraitsFixes()
        {
            if (!ModState.MultiLangColorfulTraits) return;

            int legendary = 0, good = 0, neutral = 0, bad = 0, terrible = 0, nightmare = 0, unique = 0, restrictive = 0;
            int skippedAlready = 0;

            try
            {
                foreach (TraitDef trait in DefDatabase<TraitDef>.AllDefs)
                {
                    if (trait.degreeDatas == null) continue;

                    foreach (TraitDegreeData degree in trait.degreeDatas)
                    {
                        if (string.IsNullOrEmpty(degree.label)) continue;

                        // MultiLang's hand-curated coloring always wins.
                        if (degree.label.StartsWith("<color=", StringComparison.Ordinal))
                        {
                            skippedAlready++;
                            continue;
                        }

                        var tier = ClassifyTier(trait, degree);

                        // Neutral tier: leave the label alone. Letting RimWorld render it in
                        // its default white reduces visual noise — the eye now catches only
                        // traits that are meaningfully good or bad, which is the whole point
                        // of tier-based coloring. (ColorNeutral hex is kept in the constants
                        // in case we ever want to bring yellow neutral back.)
                        if (tier == Tier.Neutral)
                        {
                            neutral++;
                            continue;
                        }

                        string hex;
                        switch (tier)
                        {
                            case Tier.Legendary:   hex = ColorLegendary;   legendary++;   break;
                            case Tier.Good:        hex = ColorGood;        good++;        break;
                            case Tier.Bad:         hex = ColorBad;         bad++;         break;
                            case Tier.Terrible:    hex = ColorTerrible;    terrible++;    break;
                            case Tier.Nightmare:   hex = ColorNightmare;   nightmare++;   break;
                            case Tier.Unique:      hex = ColorUnique;      unique++;      break;
                            case Tier.Restrictive: hex = ColorRestrictive; restrictive++; break;
                            default:               hex = ColorNeutral;     neutral++;     break;
                        }

                        degree.label = "<color=#" + hex + ">" + degree.label + "</color>";
                    }
                }

                Log.Message(
                    $"[Mod Fixes Pack] MultiLang: auto-classified traits " +
                    $"(Legendary:{legendary}, Good:{good}, Neutral:{neutral}, " +
                    $"Bad:{bad}, Terrible:{terrible}, Nightmare:{nightmare}, " +
                    $"Unique:{unique}, Restrictive:{restrictive}, " +
                    $"skipped {skippedAlready} already-colored).");
            }
            catch (Exception ex)
            {
                Log.Warning("[Mod Fixes Pack] MultiLang auto-classify failed: " + ex.Message);
            }
        }

        private enum Tier { Legendary, Good, Neutral, Bad, Terrible, Nightmare, Unique, Restrictive }

        private static Tier ClassifyTier(TraitDef trait, TraitDegreeData degree)
        {
            // Unique overrides come first — traits with flavorful quirk mechanics that don't
            // fit a clean good/bad axis (e.g. strong psychic sensitivity either direction).
            if (IsUniqueMechanic(trait, degree)) return Tier.Unique;

            // Restrictive: traits that disable work categories or require specific work tags.
            // These reshape a pawn's role regardless of whether the stat effects net positive
            // or negative (Brawler: good in melee, useless at ranged; Nudist: can't wear stuff).
            // Flagged so the player instantly sees "this trait comes with strings attached."
            if (IsRestrictive(trait)) return Tier.Restrictive;

            int score = ComputeScore(trait, degree);

            if (score <= NightmareThreshold) return Tier.Nightmare;
            if (score <= TerribleThreshold)  return Tier.Terrible;
            if (score <= BadThreshold)       return Tier.Bad;
            if (score >= LegendaryThreshold) return Tier.Legendary;
            if (score >= GoodThreshold)      return Tier.Good;
            return Tier.Neutral;
        }

        /// <summary>
        /// A trait is Restrictive if it disables whole work categories or has hard work-tag
        /// requirements. These are build-shaping — they limit which roles the pawn can fill.
        /// Blue signals the restriction at a glance regardless of the rest of the stat picture.
        /// </summary>
        private static bool IsRestrictive(TraitDef trait)
        {
            if (trait.disabledWorkTypes != null && trait.disabledWorkTypes.Count > 0) return true;
            if (trait.disabledWorkTags != WorkTags.None) return true;
            if (trait.requiredWorkTags != WorkTags.None) return true;
            if (trait.requiredWorkTypes != null && trait.requiredWorkTypes.Count > 0) return true;
            return false;
        }

        /// <summary>
        /// Traits with strongly flavored mechanics that don't fit the good/bad axis.
        /// Flagged as Unique so the player visually recognises the niche effect.
        /// </summary>
        private static bool IsUniqueMechanic(TraitDef trait, TraitDegreeData degree)
        {
            // Very large psychic sensitivity swing (either direction) is flavor, not quality.
            if (degree.statOffsets != null)
            {
                foreach (var off in degree.statOffsets)
                {
                    if (off.stat == null) continue;
                    if (off.stat.defName == "PsychicSensitivity" && Math.Abs(off.value) >= 0.4f)
                        return true;
                }
            }
            if (degree.statFactors != null)
            {
                foreach (var fac in degree.statFactors)
                {
                    if (fac.stat == null) continue;
                    if (fac.stat.defName == "PsychicSensitivity" &&
                        (fac.value >= 1.4f || fac.value <= 0.6f))
                        return true;
                }
            }
            return false;
        }

        private static int ComputeScore(TraitDef trait, TraitDegreeData degree)
        {
            int score = 0;

            // Skill gains — half-point per skill level granted.
            if (degree.skillGains != null)
            {
                foreach (var g in degree.skillGains)
                    score += g.amount / 2;
            }

            // Stat offsets — +/-1 per beneficial/harmful offset (magnitude ignored for
            // simplicity; the direction of the offset is what matters for quality tier).
            if (degree.statOffsets != null)
            {
                foreach (var off in degree.statOffsets)
                {
                    if (off.stat == null) continue;
                    string name = off.stat.defName;
                    if (BeneficialStats.Contains(name))
                        score += off.value > 0 ? 1 : (off.value < 0 ? -1 : 0);
                    else if (HarmfulIfPositiveStats.Contains(name))
                        score += off.value > 0 ? -1 : (off.value < 0 ? 1 : 0);
                }
            }

            // Stat factors — multiplier above 1 is positive if the stat is beneficial.
            if (degree.statFactors != null)
            {
                foreach (var fac in degree.statFactors)
                {
                    if (fac.stat == null) continue;
                    string name = fac.stat.defName;
                    bool above = fac.value > 1f;
                    bool below = fac.value < 1f;
                    if (BeneficialStats.Contains(name))
                        score += above ? 1 : (below ? -1 : 0);
                    else if (HarmfulIfPositiveStats.Contains(name))
                        score += above ? -1 : (below ? 1 : 0);
                }
            }

            // Disabled work types/tags live on TraitDef (apply to every degree).
            if (trait.disabledWorkTypes != null)
                score -= trait.disabledWorkTypes.Count * 2;
            if (trait.disabledWorkTags != WorkTags.None)
                score -= CountSetBits((int)trait.disabledWorkTags) * 2;

            // Random mental states (Pyromaniac, random insulting spree, etc.) are hefty
            // downsides — fires, mood hits, social fallout.
            if (degree.randomMentalState != null) score -= 4;
            if (degree.forcedMentalState != null) score -= 4;

            // Random disease MTB — a short MTB means frequent sickness (bad).
            if (degree.randomDiseaseMtbDays > 0f && degree.randomDiseaseMtbDays < 60f)
                score -= 2;

            // Increased hunger rate = bad; reduced = good.
            if (degree.hungerRateFactor > 1.05f) score -= 1;
            else if (degree.hungerRateFactor > 0f && degree.hungerRateFactor < 0.95f) score += 1;

            // Pain modifiers.
            if (degree.painOffset > 0.05f) score -= 1;
            else if (degree.painOffset < -0.05f) score += 1;
            if (degree.painFactor > 1.05f) score -= 1;
            else if (degree.painFactor > 0f && degree.painFactor < 0.95f) score += 1;

            // Required work tags restrict which pawns can have the trait — mild penalty
            // since it limits pawn flexibility even though the trait itself may be positive.
            if (trait.requiredWorkTags != WorkTags.None)
                score -= 1;

            return score;
        }

        private static int CountSetBits(int n)
        {
            int count = 0;
            while (n != 0) { n &= n - 1; count++; }
            return count;
        }
    }
}
