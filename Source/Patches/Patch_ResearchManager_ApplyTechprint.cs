using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace ResearchInflation.Patches
{
    // Replaces vanilla's handling of a techprint for an unlocked, unfinished project: the bonus is half the remaining inflated cost
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ApplyTechprint))]
    public static class Patch_ResearchManager_ApplyTechprint
    {
        private const float BonusFraction = 0.5f;

        public static bool Prefix(ResearchManager __instance, ResearchProjectDef proj, Pawn? applyingPawn, Dictionary<ResearchProjectDef, float> ___progress)
        {
            if (!ModsConfig.RoyaltyActive || proj.baseCost <= 0f || proj.TechprintCount > __instance.GetTechprints(proj) || proj.IsFinished)
            {
                return true;
            }

            float current = __instance.GetProgress(proj);
            float cost = proj.Cost;
            float bonus = Mathf.Max((cost - current) * BonusFraction, 0f);
            ___progress[proj] = Mathf.Min(current + bonus, cost);

            StringBuilder text = new StringBuilder();
            text.AppendLine("LetterTechprintAppliedPartIntro".Translate(proj.Named("PROJECT")));
            text.AppendLine();
            text.AppendLine("LetterTechprintAppliedPartAlreadyUnlocked".Translate(bonus, proj.Named("PROJECT")));
            text.AppendLine();
            if (applyingPawn != null)
            {
                text.AppendLine("LetterTechprintAppliedPartExpAwarded".Translate(ResearchManager.IntellectualExpPerTechprint.ToString(), SkillDefOf.Intellectual.label, applyingPawn.Named("PAWN")));
                applyingPawn.skills?.Learn(SkillDefOf.Intellectual, ResearchManager.IntellectualExpPerTechprint, direct: true, ignoreLearnRate: true);
            }

            Find.LetterStack.ReceiveLetter("LetterTechprintAppliedLabel".Translate(proj.Named("PROJECT")), text.ToString().TrimEndNewlines(), LetterDefOf.PositiveEvent);
            return false;
        }
    }
}
