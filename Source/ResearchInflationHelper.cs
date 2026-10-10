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
        private const float UnfinishedMargin = 0.01f;

        private static readonly FieldInfo ProgressField = AccessTools.Field(typeof(ResearchManager), "progress");

        private static readonly HashSet<ResearchProjectDef> finishedProjects = new HashSet<ResearchProjectDef>();

        private static bool cacheDirty = true;
        private static float cachedMultiplier = 1f;
        private static int cachedFinishedCount;
        private static bool snappingProgress;
        private static bool ready;

        public static void InvalidateCache()
        {
            cacheDirty = true;
        }

        public static void NotifyProjectFinished(ResearchProjectDef proj)
        {
            if (proj.baseCost > 0f && finishedProjects.Add(proj))
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
            return new List<ResearchProjectDef>(finishedProjects);
        }

        public static void LoadFinishedProjects(IEnumerable<ResearchProjectDef> projects)
        {
            finishedProjects.Clear();
            foreach (ResearchProjectDef proj in projects)
            {
                if (proj != null && proj.baseCost > 0f)
                {
                    finishedProjects.Add(proj);
                }
            }

            cacheDirty = true;
        }

        public static float GetMultiplier()
        {
            if (Current.Game == null)
            {
                return 1f;
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
            return finishedProjects.Contains(proj);
        }

        public static float GetInflatedCost(ResearchProjectDef proj, float originalCost)
        {
            return GetInflatedCost(proj, originalCost, GetMultiplier());
        }

        public static float GetInflatedCost(ResearchProjectDef proj, float originalCost, float multiplier)
        {
            ResearchInflationSettings settings = ResearchInflationMod.Settings;
            if (originalCost <= 0f || proj.baseCost <= 0f || proj.baseCost < settings.ignoreThreshold)
            {
                return originalCost;
            }

            float cost = originalCost;
            float? eraValue = settings.EraValue(proj.techLevel);
            if (eraValue.HasValue)
            {
                if (settings.eraCostMode == EraCostMode.Additive)
                {
                    cost += eraValue.Value;
                }
                else if (settings.eraCostMode == EraCostMode.Replace)
                {
                    cost = Mathf.Max(eraValue.Value, ResearchInflationSettings.MinReplaceCost);
                }
            }

            return Mathf.Ceil(Mathf.Max(cost * multiplier, 0f));
        }

        public static float GetMultiplierForCount(int finishedCount)
        {
            ResearchInflationSettings settings = ResearchInflationMod.Settings;
            if (finishedCount <= 0 || settings.inflationRate <= 0f)
            {
                return 1f;
            }

            float multiplier = Mathf.Pow(1f + (settings.inflationRate / 100f), finishedCount);
            return settings.maxMultiplier >= 1f ? Mathf.Min(multiplier, settings.maxMultiplier) : multiplier;
        }

        public static bool CountsTowardInflation(ResearchProjectDef proj)
        {
            return proj.baseCost > 0f && proj.baseCost >= ResearchInflationMod.Settings.ignoreThreshold;
        }

        // Sets every tracked finished project's progress to its current inflated cost
        public static void SnapFinishedProgress()
        {
            if (!ready || snappingProgress)
            {
                return;
            }

            Dictionary<ResearchProjectDef, float>? progress = GetProgressDict();
            if (progress == null)
            {
                return;
            }

            snappingProgress = true;
            try
            {
                foreach (ResearchProjectDef proj in finishedProjects)
                {
                    progress[proj] = proj.Cost;
                }
            }
            finally
            {
                snappingProgress = false;
            }
        }

        // Finishes untracked projects whose progress reached a lowered cost, and keeps the rest below their cost
        public static void ReconcileProgress()
        {
            ResearchManager? manager = Current.Game?.researchManager;
            Dictionary<ResearchProjectDef, float>? progress = GetProgressDict();
            if (!ready || manager == null || progress == null)
            {
                return;
            }

            SnapFinishedProgress();
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            // Each finish raises the multiplier, so rescan after every one
            ResearchProjectDef? next;
            do
            {
                next = null;
                for (int i = 0; i < all.Count; i++)
                {
                    ResearchProjectDef proj = all[i];
                    if (proj.baseCost > 0f && !finishedProjects.Contains(proj) && proj.IsFinished && proj.PrerequisitesCompleted)
                    {
                        next = proj;
                        break;
                    }
                }

                if (next != null)
                {
                    manager.FinishProject(next, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
                }
            }
            while (next != null);

            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (proj.baseCost > 0f && !finishedProjects.Contains(proj) && progress.TryGetValue(proj, out float stored) && stored >= proj.Cost)
                {
                    progress[proj] = Mathf.Max(0f, proj.Cost - UnfinishedMargin);
                }
            }
        }

        // Runs before other mods' GameComponent.FinalizeInit, so finished research already reads as finished
        public static void InitializeForGame(GameComponent_ResearchInflation? comp)
        {
            if (comp == null)
            {
                return;
            }

            // Saves at the current version loaded their finished list in PostLoadInit
            if (comp.progressModelVersion < CurrentSaveVersion)
            {
                MigrateLegacyProgress();
            }

            ready = true;
            SnapFinishedProgress();
            comp.progressModelVersion = CurrentSaveVersion;
        }

        private static void MigrateLegacyProgress()
        {
            finishedProjects.Clear();
            cacheDirty = true;

            ResearchManager? manager = Current.Game?.researchManager;
            Dictionary<ResearchProjectDef, float>? progress = GetProgressDict();
            if (manager == null || progress == null)
            {
                return;
            }

            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            foreach (ResearchProjectDef proj in all)
            {
                if (proj.baseCost > 0f && manager.GetProgress(proj) >= proj.baseCost - UnfinishedMargin)
                {
                    finishedProjects.Add(proj);
                }
            }

            // Scales in-progress projects so they keep the same percentage under inflation
            foreach (ResearchProjectDef proj in all)
            {
                if (proj.baseCost <= 0f || finishedProjects.Contains(proj) || !progress.TryGetValue(proj, out float stored) || stored <= 0f)
                {
                    continue;
                }

                float inflated = GetInflatedCost(proj, proj.baseCost);
                progress[proj] = Mathf.Min(stored * (inflated / proj.baseCost), Mathf.Max(0f, inflated - UnfinishedMargin));
            }
        }

        private static void RefreshCacheIfNeeded()
        {
            if (!cacheDirty)
            {
                return;
            }

            int count = 0;
            foreach (ResearchProjectDef proj in finishedProjects)
            {
                if (CountsTowardInflation(proj))
                {
                    count++;
                }
            }

            cachedFinishedCount = count;
            cachedMultiplier = GetMultiplierForCount(count);
            cacheDirty = false;
        }

        private static Dictionary<ResearchProjectDef, float>? GetProgressDict()
        {
            ResearchManager? manager = Current.Game?.researchManager;
            return manager == null ? null : ProgressField.GetValue(manager) as Dictionary<ResearchProjectDef, float>;
        }
    }
}
