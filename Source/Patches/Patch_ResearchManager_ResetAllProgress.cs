using HarmonyLib;
using RimWorld;

namespace ResearchInflation.Patches
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ResetAllProgress))]
    public static class Patch_ResearchManager_ResetAllProgress
    {
        public static void Postfix()
        {
            ResearchInflationHelper.NotifyReset();
        }
    }
}
