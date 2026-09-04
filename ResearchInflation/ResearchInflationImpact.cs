using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    public class EraImpact
    {
        public string label;
        public int techs;
        public float vanilla;
        public float inflated;
    }

    public class ResearchInflationImpact
    {
        public bool defsReady;
        public bool inColony;

        public int treeCount;
        public float treeVanilla;
        public float completeTotal;
        public int endCounted;
        public float endMultiplier;

        public int finishedCount;
        public int remainingCount;
        public int countedFinished;
        public float multiplier;

        public ResearchProjectDef currentProject;
        public float currentVanilla;
        public float currentInflated;
        public float currentProgress;

        public List<EraImpact> eras = new List<EraImpact>();

        public static ResearchInflationImpact Build()
        {
            ResearchInflationImpact impact = new ResearchInflationImpact();
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            if (all == null || all.Count == 0)
            {
                return impact;
            }

            List<ResearchProjectDef> tree = new List<ResearchProjectDef>();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (proj != null && proj.baseCost > 0f)
                {
                    tree.Add(proj);
                }
            }

            if (tree.Count == 0)
            {
                return impact;
            }

            impact.defsReady = true;
            impact.inColony = Current.ProgramState == ProgramState.Playing && Find.ResearchManager != null;
            impact.treeCount = tree.Count;

            for (int i = 0; i < tree.Count; i++)
            {
                impact.treeVanilla += tree[i].baseCost;
            }

            Dictionary<string, EraImpact> eraMap = CreateEraMap();
            WalkTree(tree, 0, null, eraMap, out impact.completeTotal, out impact.endCounted);
            impact.endMultiplier = ResearchInflationHelper.GetMultiplierForCount(impact.endCounted);
            impact.eras = EraList(eraMap);

            if (impact.inColony)
            {
                FillColony(impact, tree);
            }

            return impact;
        }

        private static void FillColony(ResearchInflationImpact impact, List<ResearchProjectDef> tree)
        {
            impact.countedFinished = ResearchInflationHelper.GetFinishedCount();
            impact.multiplier = ResearchInflationHelper.GetMultiplier();
            impact.currentProject = Find.ResearchManager.GetProject(null);

            for (int i = 0; i < tree.Count; i++)
            {
                ResearchProjectDef proj = tree[i];
                if (ResearchInflationHelper.IsTrackedFinished(proj))
                {
                    impact.finishedCount++;
                }
                else
                {
                    impact.remainingCount++;
                }
            }

            if (impact.currentProject != null && impact.currentProject.baseCost > 0f && !ResearchInflationHelper.IsTrackedFinished(impact.currentProject))
            {
                impact.currentVanilla = impact.currentProject.baseCost;
                impact.currentInflated = impact.currentProject.Cost;
                impact.currentProgress = impact.currentProject.ProgressReal;
            }
        }

        private static void WalkTree(List<ResearchProjectDef> tree, int startCounted, HashSet<ResearchProjectDef> alreadyDone, Dictionary<string, EraImpact> eraMap, out float total, out int counted)
        {
            HashSet<ResearchProjectDef> inTree = new HashSet<ResearchProjectDef>(tree);
            HashSet<ResearchProjectDef> done = alreadyDone != null ? new HashSet<ResearchProjectDef>(alreadyDone) : new HashSet<ResearchProjectDef>();
            List<ResearchProjectDef> remaining = new List<ResearchProjectDef>();
            for (int i = 0; i < tree.Count; i++)
            {
                if (!done.Contains(tree[i]))
                {
                    remaining.Add(tree[i]);
                }
            }

            counted = startCounted;
            total = 0f;
            float threshold = ResearchInflationMod.settings != null ? ResearchInflationMod.settings.ignoreThreshold : 0f;

            while (remaining.Count > 0)
            {
                int pickIndex = FindNextIndex(remaining, done, inTree);
                ResearchProjectDef proj = remaining[pickIndex];
                remaining.RemoveAt(pickIndex);

                float multiplier = ResearchInflationHelper.GetMultiplierForCount(counted);
                float inflated = ResearchInflationHelper.GetInflatedCost(proj, proj.baseCost, multiplier);
                total += inflated;
                AddEra(eraMap, proj, proj.baseCost, inflated);

                done.Add(proj);
                if (proj.baseCost >= threshold)
                {
                    counted++;
                }
            }
        }

        private static int FindNextIndex(List<ResearchProjectDef> remaining, HashSet<ResearchProjectDef> done, HashSet<ResearchProjectDef> inTree)
        {
            int best = 0;
            bool bestReady = PrereqsMet(remaining[0], done, inTree);
            for (int i = 1; i < remaining.Count; i++)
            {
                ResearchProjectDef proj = remaining[i];
                bool ready = PrereqsMet(proj, done, inTree);
                if (BetterPick(proj, ready, remaining[best], bestReady))
                {
                    best = i;
                    bestReady = ready;
                }
            }

            return best;
        }

        private static bool BetterPick(ResearchProjectDef candidate, bool candidateReady, ResearchProjectDef current, bool currentReady)
        {
            if (candidateReady != currentReady)
            {
                return candidateReady;
            }

            if (candidate.baseCost != current.baseCost)
            {
                return candidate.baseCost < current.baseCost;
            }

            return string.CompareOrdinal(candidate.defName, current.defName) < 0;
        }

        private static bool PrereqsMet(ResearchProjectDef proj, HashSet<ResearchProjectDef> done, HashSet<ResearchProjectDef> inTree)
        {
            return ListMet(proj.prerequisites, done, inTree) && ListMet(proj.hiddenPrerequisites, done, inTree);
        }

        private static bool ListMet(List<ResearchProjectDef> list, HashSet<ResearchProjectDef> done, HashSet<ResearchProjectDef> inTree)
        {
            if (list == null)
            {
                return true;
            }

            for (int i = 0; i < list.Count; i++)
            {
                ResearchProjectDef prereq = list[i];
                if (prereq == null || prereq.baseCost <= 0f || !inTree.Contains(prereq))
                {
                    continue;
                }

                if (!done.Contains(prereq))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, EraImpact> CreateEraMap()
        {
            Dictionary<string, EraImpact> map = new Dictionary<string, EraImpact>();
            map.Add("Neolithic", new EraImpact { label = "Neolithic" });
            map.Add("Medieval", new EraImpact { label = "Medieval" });
            map.Add("Industrial", new EraImpact { label = "Industrial" });
            map.Add("Spacer", new EraImpact { label = "Spacer" });
            map.Add("Ultra", new EraImpact { label = "Ultra" });
            return map;
        }

        private static void AddEra(Dictionary<string, EraImpact> map, ResearchProjectDef proj, float vanilla, float inflated)
        {
            string key;
            switch (proj.techLevel)
            {
                case TechLevel.Medieval:
                    key = "Medieval";
                    break;
                case TechLevel.Industrial:
                    key = "Industrial";
                    break;
                case TechLevel.Spacer:
                    key = "Spacer";
                    break;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    key = "Ultra";
                    break;
                default:
                    key = "Neolithic";
                    break;
            }

            EraImpact era = map[key];
            era.techs++;
            era.vanilla += vanilla;
            era.inflated += inflated;
        }

        private static List<EraImpact> EraList(Dictionary<string, EraImpact> map)
        {
            List<EraImpact> list = new List<EraImpact>();
            string[] keys = { "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra" };
            for (int i = 0; i < keys.Length; i++)
            {
                EraImpact era = map[keys[i]];
                if (era.techs > 0)
                {
                    list.Add(era);
                }
            }

            return list;
        }
    }
}
