using HarmonyLib;
using RimWorld;

namespace ResearchInflation.Patches
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.DebugSetAllProjectsFinished))]
    public static class Patch_ResearchManager_DebugSetAllProjectsFinished
    {
        public static void Prefix()
        {
            ResearchInflationHelper.NotifyAllProjectsFinished();
        }
    }
}
