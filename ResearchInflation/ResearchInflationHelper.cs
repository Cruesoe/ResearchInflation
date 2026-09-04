using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    public static class ResearchInflationHelper
    {
        public const int CurrentSaveVersion = 2;

        private static readonly FieldInfo ProgressField = AccessTools.Field(typeof(ResearchManager), "progress");

        private static readonly HashSet<ResearchProjectDef> finishedProjects = new HashSet<ResearchProjectDef>();

        private static bool cacheDirty = true;
        private static float cachedMultiplier = 1f;
        private static int cachedFinishedCount = 0;
        private static bool snappingProgress;
        private static bool ready;

        public static void InvalidateCache()
        {
            cacheDirty = true;
        }

        public static void NotifyProjectFinished(ResearchProjectDef proj)
        {
            if (proj == null || proj.baseCost <= 0f)
            {
                return;
            }

            if (finishedProjects.Add(proj))
            {
                cacheDirty = true;
            }
        }

        public static void NotifyAllProjectsFinished()
        {
            foreach (ResearchProjectDef proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (proj.baseCost > 0f)
                {
                    finishedProjects.Add(proj);
                }
            }

            cacheDirty = true;
        }

        public static void NotifyReset()
        {
            finishedProjects.Clear();
            cacheDirty = true;
        }

        public static void DetachFromGame()
        {
            NotifyReset();
            ready = false;
        }

        public static List<ResearchProjectDef> CopyFinishedProjects()
        {
            List<ResearchProjectDef> list = new List<ResearchProjectDef>(finishedProjects.Count);
            foreach (ResearchProjectDef proj in finishedProjects)
            {
                if (proj != null)
                {
                    list.Add(proj);
                }
            }

            return list;
        }

        public static void LoadFinishedProjects(IEnumerable<ResearchProjectDef> projects)
        {
            finishedProjects.Clear();
            if (projects != null)
            {
                foreach (ResearchProjectDef proj in projects)
                {
                    if (proj != null && proj.baseCost > 0f)
                    {
                        finishedProjects.Add(proj);
                    }
                }
            }

            cacheDirty = true;
        }

        public static float GetMultiplier()
        {
            if (Current.Game == null)
            {
                ResearchInflationSettings settings = ResearchInflationMod.settings;
                return settings != null ? MultiplierFromCount(0, settings) : 1f;
            }

            RefreshCacheIfNeeded();
            return cachedMultiplier;
        }

        public static int GetFinishedCount()
        {
            if (Current.Game == null)
            {
                return 0;
            }

            RefreshCacheIfNeeded();
            return cachedFinishedCount;
        }

        public static bool IsTrackedFinished(ResearchProjectDef proj)
        {
            return proj != null && finishedProjects.Contains(proj);
        }

        public static float GetInflatedCost(ResearchProjectDef proj, float originalCost)
        {
            return GetInflatedCost(proj, originalCost, GetMultiplier());
        }

        public static float GetInflatedCost(ResearchProjectDef proj, float originalCost, float multiplier)
        {
            if (proj == null || originalCost <= 0f)
            {
                return originalCost;
            }

            ResearchInflationSettings settings = ResearchInflationMod.settings;
            if (settings == null)
            {
                return originalCost;
            }

            if (proj.baseCost <= 0f || originalCost < settings.ignoreThreshold)
            {
                return originalCost;
            }

            float baseVal = originalCost;
            if (settings.useCustomEraCosts)
            {
                float adj = GetEraValue(proj, settings);
                if (settings.eraCostMode == EraCostMode.Replace)
                {
                    baseVal = adj;
                }
                else
                {
                    baseVal = baseVal + adj;
                }

                if (baseVal < 0f)
                {
                    baseVal = 0f;
                }
            }

            float inflated = baseVal * multiplier;
            if (inflated < 0f)
            {
                inflated = 0f;
            }

            return Mathf.Ceil(inflated);
        }

        public static float GetMultiplierForCount(int finishedCount)
        {
            ResearchInflationSettings settings = ResearchInflationMod.settings;
            if (settings == null)
            {
                return 1f;
            }

            return MultiplierFromCount(finishedCount, settings);
        }

        public static void SnapFinishedProgress()
        {
            if (!ready || snappingProgress || Find.ResearchManager == null)
            {
                return;
            }

            Dictionary<ResearchProjectDef, float> progress = GetProgressDict();
            if (progress == null)
            {
                return;
            }

            snappingProgress = true;
            try
            {
                foreach (ResearchProjectDef proj in finishedProjects)
                {
                    if (proj == null || proj.baseCost <= 0f)
                    {
                        continue;
                    }

                    progress[proj] = proj.Cost;
                }
            }
            finally
            {
                snappingProgress = false;
            }
        }

        public static void InitializeForGame(GameComponent_ResearchInflation comp)
        {
            if (comp == null)
            {
                return;
            }

            if (comp.progressModelVersion < CurrentSaveVersion)
            {
                MigrateLegacyProgress();
            }
            else
            {
                LoadFinishedProjects(comp.finishedProjects);
            }

            ready = true;
            SnapFinishedProgress();

            comp.progressModelVersion = CurrentSaveVersion;
            comp.finishedProjects = CopyFinishedProjects();
        }

        private static void MigrateLegacyProgress()
        {
            finishedProjects.Clear();

            ResearchManager manager = Find.ResearchManager;
            Dictionary<ResearchProjectDef, float> progress = GetProgressDict();
            if (manager == null || progress == null)
            {
                cacheDirty = true;
                return;
            }

            foreach (ResearchProjectDef proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (proj.baseCost <= 0f)
                {
                    continue;
                }

                if (manager.GetProgress(proj) >= proj.baseCost - 0.01f)
                {
                    finishedProjects.Add(proj);
                }
            }

            cacheDirty = true;

            foreach (ResearchProjectDef proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (proj.baseCost <= 0f || finishedProjects.Contains(proj))
                {
                    continue;
                }

                if (!progress.TryGetValue(proj, out float stored) || stored <= 0f)
                {
                    continue;
                }

                float inflated = GetInflatedCost(proj, proj.baseCost);
                float scaled = stored * (inflated / proj.baseCost);
                progress[proj] = Mathf.Min(scaled, Mathf.Max(0f, inflated - 0.01f));
            }
        }

        private static void RefreshCacheIfNeeded()
        {
            if (!cacheDirty)
            {
                return;
            }

            ResearchInflationSettings settings = ResearchInflationMod.settings;
            int count = 0;
            float threshold = settings != null ? settings.ignoreThreshold : 0f;

            foreach (ResearchProjectDef proj in finishedProjects)
            {
                if (proj != null && proj.baseCost > 0f && proj.baseCost >= threshold)
                {
                    count++;
                }
            }

            cachedFinishedCount = count;
            cachedMultiplier = settings != null ? MultiplierFromCount(count, settings) : 1f;
            cacheDirty = false;
        }

        private static float MultiplierFromCount(int finishedCount, ResearchInflationSettings settings)
        {
            if (finishedCount <= 0 || settings.inflationRate <= 0f)
            {
                return 1f;
            }

            float multiplier = Mathf.Pow(1f + (settings.inflationRate / 100f), finishedCount);
            if (settings.maxMultiplier > 0f)
            {
                multiplier = Mathf.Min(multiplier, settings.maxMultiplier);
            }

            return multiplier;
        }

        private static float GetEraValue(ResearchProjectDef proj, ResearchInflationSettings settings)
        {
            switch (proj.techLevel)
            {
                case TechLevel.Animal:
                case TechLevel.Neolithic:
                    return settings.valNeolithic;
                case TechLevel.Medieval:
                    return settings.valMedieval;
                case TechLevel.Industrial:
                    return settings.valIndustrial;
                case TechLevel.Spacer:
                    return settings.valSpacer;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return settings.valUltra;
                default:
                    return 0f;
            }
        }

        private static Dictionary<ResearchProjectDef, float> GetProgressDict()
        {
            if (Find.ResearchManager == null || ProgressField == null)
            {
                return null;
            }

            return ProgressField.GetValue(Find.ResearchManager) as Dictionary<ResearchProjectDef, float>;
        }
    }
}
