using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    public class EraImpact
    {
        public string label;
        public int remaining;
        public float vanilla;
        public float inflated;
    }

    public class ResearchInflationImpact
    {
        public bool defsReady;
        public bool inColony;

        public int treeCount;
        public float treeVanilla;
        public float medianCost;
        public float averageCost;
        public ResearchProjectDef typicalProject;

        public int finishedCount;
        public int remainingCount;
        public int countedFinished;
        public float multiplier;

        public float remainingVanilla;
        public float remainingInflated;
        public float remainingWork;

        public ResearchProjectDef currentProject;
        public float currentVanilla;
        public float currentInflated;
        public float currentProgress;

        public ResearchProjectDef priciestRemaining;
        public float priciestVanilla;
        public float priciestInflated;

        public float afterNextListed;
        public float othersListedNow;
        public bool nextFinishKnown;
        public string nextFinishName;

        public List<EraImpact> eras = new List<EraImpact>();
        public List<Projection> projections = new List<Projection>();

        public struct Projection
        {
            public int finished;
            public int counted;
            public int remaining;
            public float multiplier;
            public float vanilla;
            public float inflated;
        }

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

            List<float> costs = new List<float>(tree.Count);
            for (int i = 0; i < tree.Count; i++)
            {
                impact.treeVanilla += tree[i].baseCost;
                costs.Add(tree[i].baseCost);
            }

            costs.Sort();
            impact.averageCost = impact.treeVanilla / tree.Count;
            impact.medianCost = costs[costs.Count / 2];
            impact.typicalProject = FindTypicalProject(tree, impact.medianCost);

            if (impact.inColony)
            {
                FillColony(impact, tree);
            }
            else
            {
                FillProjections(impact, tree);
            }

            return impact;
        }

        private static ResearchProjectDef FindTypicalProject(List<ResearchProjectDef> tree, float medianCost)
        {
            ResearchProjectDef best = tree[0];
            float bestDelta = Mathf.Abs(tree[0].baseCost - medianCost);
            for (int i = 1; i < tree.Count; i++)
            {
                float delta = Mathf.Abs(tree[i].baseCost - medianCost);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = tree[i];
                }
            }

            return best;
        }

        private static void FillColony(ResearchInflationImpact impact, List<ResearchProjectDef> tree)
        {
            impact.countedFinished = ResearchInflationHelper.GetFinishedCount();
            impact.multiplier = ResearchInflationHelper.GetMultiplier();
            impact.currentProject = Find.ResearchManager.GetProject(null);

            Dictionary<string, EraImpact> eraMap = CreateEraMap();
            float priciest = -1f;

            for (int i = 0; i < tree.Count; i++)
            {
                ResearchProjectDef proj = tree[i];
                if (proj.IsFinished)
                {
                    impact.finishedCount++;
                    continue;
                }

                impact.remainingCount++;
                float vanilla = proj.baseCost;
                float inflated = proj.Cost;
                impact.remainingVanilla += vanilla;
                impact.remainingInflated += inflated;
                impact.remainingWork += Mathf.Max(0f, inflated - proj.ProgressReal);

                if (inflated > priciest)
                {
                    priciest = inflated;
                    impact.priciestRemaining = proj;
                    impact.priciestVanilla = vanilla;
                    impact.priciestInflated = inflated;
                }

                AddEra(eraMap, proj, vanilla, inflated);
            }

            impact.eras = EraList(eraMap);

            if (impact.currentProject != null && impact.currentProject.baseCost > 0f && !impact.currentProject.IsFinished)
            {
                impact.currentVanilla = impact.currentProject.baseCost;
                impact.currentInflated = impact.currentProject.Cost;
                impact.currentProgress = impact.currentProject.ProgressReal;
                impact.nextFinishKnown = true;
                impact.nextFinishName = impact.currentProject.LabelCap;

                int countedAfter = impact.countedFinished;
                ResearchInflationSettings colonySettings = ResearchInflationMod.settings;
                if (colonySettings != null && impact.currentProject.baseCost >= colonySettings.ignoreThreshold)
                {
                    countedAfter++;
                }

                float nextMult = ResearchInflationHelper.GetMultiplierForCount(countedAfter);
                for (int i = 0; i < tree.Count; i++)
                {
                    ResearchProjectDef proj = tree[i];
                    if (proj.IsFinished || proj == impact.currentProject)
                    {
                        continue;
                    }

                    impact.othersListedNow += proj.Cost;
                    impact.afterNextListed += ResearchInflationHelper.GetInflatedCost(proj, proj.baseCost, nextMult);
                }
            }
            else
            {
                int countedAfter = impact.countedFinished + 1;
                float nextMult = ResearchInflationHelper.GetMultiplierForCount(countedAfter);
                for (int i = 0; i < tree.Count; i++)
                {
                    ResearchProjectDef proj = tree[i];
                    if (proj.IsFinished)
                    {
                        continue;
                    }

                    impact.othersListedNow += proj.Cost;
                    impact.afterNextListed += ResearchInflationHelper.GetInflatedCost(proj, proj.baseCost, nextMult);
                }
            }
        }

        private static void FillProjections(ResearchInflationImpact impact, List<ResearchProjectDef> tree)
        {
            List<ResearchProjectDef> sorted = new List<ResearchProjectDef>(tree);
            sorted.Sort((a, b) => a.baseCost.CompareTo(b.baseCost));

            int[] samples = { 25, 50, 100 };
            float threshold = ResearchInflationMod.settings != null ? ResearchInflationMod.settings.ignoreThreshold : 0f;

            for (int s = 0; s < samples.Length; s++)
            {
                int n = samples[s];
                if (n >= sorted.Count)
                {
                    n = sorted.Count - 1;
                }

                if (n < 0)
                {
                    continue;
                }

                int counted = 0;
                for (int i = 0; i < n; i++)
                {
                    if (sorted[i].baseCost >= threshold)
                    {
                        counted++;
                    }
                }

                Projection projection = new Projection
                {
                    finished = n,
                    counted = counted,
                    remaining = sorted.Count - n,
                    multiplier = ResearchInflationHelper.GetMultiplierForCount(counted)
                };

                for (int i = n; i < sorted.Count; i++)
                {
                    projection.vanilla += sorted[i].baseCost;
                    projection.inflated += ResearchInflationHelper.GetInflatedCost(sorted[i], sorted[i].baseCost, projection.multiplier);
                }

                impact.projections.Add(projection);
            }
        }

        private static Dictionary<string, EraImpact> CreateEraMap()
        {
            Dictionary<string, EraImpact> map = new Dictionary<string, EraImpact>();
            map.Add("Neolithic", new EraImpact { label = "Neolithic" });
            map.Add("Medieval", new EraImpact { label = "Medieval" });
            map.Add("Industrial", new EraImpact { label = "Industrial" });
            map.Add("Spacer", new EraImpact { label = "Spacer" });
            map.Add("Ultra", new EraImpact { label = "Ultra / Archotech" });
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
            era.remaining++;
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
                if (era.remaining > 0)
                {
                    list.Add(era);
                }
            }

            return list;
        }
    }
}
