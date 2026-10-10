using HarmonyLib;
using RimWorld;

namespace ResearchInflation.Patches
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ReapplyAllMods))]
    public static class Patch_ResearchManager_ReapplyAllMods
    {
        public static void Prefix()
        {
            ResearchInflationHelper.SnapFinishedProgress();
        }
    }
}
