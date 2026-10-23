using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchInflation.Patches
{
    // FinishProject always calls ReapplyAllMods, whose prefix sets the new costs as progress
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        public static void Prefix(ResearchProjectDef proj)
        {
            ResearchInflationHelper.NotifyProjectFinished(proj);
        }
    }
}
