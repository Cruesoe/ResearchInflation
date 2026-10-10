using HarmonyLib;
using Verse;

namespace ResearchInflation.Patches
{
    [HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.Cost), MethodType.Getter)]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_ResearchProjectDef_Cost
    {
        public static void Postfix(ResearchProjectDef __instance, ref float __result)
        {
            __result = ResearchInflationHelper.GetInflatedCost(__instance, __result);
        }
    }
}
