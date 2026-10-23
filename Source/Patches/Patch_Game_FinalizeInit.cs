using HarmonyLib;
using Verse;

namespace ResearchInflation.Patches
{
    // Runs before ReapplyAllMods and every GameComponent.FinalizeInit
    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Patch_Game_FinalizeInit
    {
        public static void Prefix(Game __instance)
        {
            ResearchInflationHelper.InitializeForGame(__instance.GetComponent<GameComponent_ResearchInflation>());
        }
    }
}
