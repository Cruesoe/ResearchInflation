using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    [HarmonyPatch(typeof(ResearchProjectDef), "get_Cost")]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_ResearchProjectDef_Cost
    {
        public static void Postfix(ResearchProjectDef __instance, ref float __result)
        {
            __result = ResearchInflationHelper.GetInflatedCost(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        public static void Prefix(ResearchProjectDef proj)
        {
            if (proj == null || proj.baseCost <= 0f)
            {
                return;
            }

            ResearchInflationHelper.NotifyProjectFinished(proj);
            ResearchInflationHelper.SnapFinishedProgress();
        }

        public static void Postfix(ResearchProjectDef proj, Dictionary<ResearchProjectDef, float> ___progress)
        {
            if (proj == null || proj.baseCost <= 0f || ___progress == null)
            {
                return;
            }

            ___progress[proj] = proj.Cost;
            ResearchInflationHelper.SnapFinishedProgress();
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ReapplyAllMods))]
    public static class Patch_ResearchManager_ReapplyAllMods
    {
        public static void Prefix()
        {
            ResearchInflationHelper.SnapFinishedProgress();
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ResetAllProgress))]
    public static class Patch_ResearchManager_ResetAllProgress
    {
        public static void Postfix()
        {
            ResearchInflationHelper.NotifyReset();
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.DebugSetAllProjectsFinished))]
    public static class Patch_ResearchManager_DebugSetAllProjectsFinished
    {
        public static void Prefix()
        {
            ResearchInflationHelper.NotifyAllProjectsFinished();
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ApplyTechprint))]
    public static class Patch_ResearchManager_ApplyTechprint
    {
        public static void Prefix(ResearchProjectDef proj, out float __state)
        {
            __state = float.NaN;
            if (proj == null || proj.baseCost <= 0f || Find.ResearchManager == null)
            {
                return;
            }

            if (proj.TechprintCount > Find.ResearchManager.GetTechprints(proj) || proj.IsFinished)
            {
                return;
            }

            __state = Find.ResearchManager.GetProgress(proj);
        }

        public static void Postfix(ResearchProjectDef proj, float __state, Dictionary<ResearchProjectDef, float> ___progress)
        {
            if (float.IsNaN(__state) || proj == null || ___progress == null)
            {
                return;
            }

            float cost = proj.Cost;
            float bonus = (cost - __state) * 0.5f;
            if (bonus < 0f)
            {
                bonus = 0f;
            }

            ___progress[proj] = Mathf.Min(__state + bonus, cost);
        }
    }
}
