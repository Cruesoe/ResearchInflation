using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ResearchInflation
{
    // Projected cost of the whole research tree under the current settings, plus the loaded colony's progress
    public class ResearchInflationImpact
    {
        private static readonly TechLevel[] EraLevels = { TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra };

        public int projectCount;
        public float totalVanilla;
        public float totalInflated;
        public float endMultiplier = 1f;
        public List<EraImpact> eras = new List<EraImpact>();

        public bool inColony;
        public int finishedCount;
        public int remainingCount;
        public float multiplier = 1f;
        public ResearchProjectDef? currentProject;
        public float currentProgress;
        public float currentCost;

        public static ResearchInflationImpact Build()
        {
            ResearchInflationImpact impact = new ResearchInflationImpact();
            List<ResearchProjectDef> tree = new List<ResearchProjectDef>();
            foreach (ResearchProjectDef proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (proj.baseCost > 0f)
                {
                    tree.Add(proj);
                    impact.totalVanilla += proj.baseCost;
                }
            }

            impact.projectCount = tree.Count;
            Dictionary<TechLevel, EraImpact> eraMap = new Dictionary<TechLevel, EraImpact>();
            foreach (TechLevel level in EraLevels)
            {
                eraMap[level] = new EraImpact(level.ToStringHuman().CapitalizeFirst());
            }

            int counted = impact.WalkTree(tree, eraMap);
            impact.endMultiplier = ResearchInflationHelper.GetMultiplierForCount(counted);
            foreach (TechLevel level in EraLevels)
            {
                if (eraMap[level].projects > 0)
                {
                    impact.eras.Add(eraMap[level]);
                }
            }

            ResearchManager? manager = Current.Game?.researchManager;
            if (Current.ProgramState == ProgramState.Playing && manager != null)
            {
                impact.FillColony(tree, manager);
            }

            return impact;
        }

        private void FillColony(List<ResearchProjectDef> tree, ResearchManager manager)
        {
            this.inColony = true;
            this.multiplier = ResearchInflationHelper.GetMultiplier();
            foreach (ResearchProjectDef proj in tree)
            {
                if (ResearchInflationHelper.IsTrackedFinished(proj))
                {
                    this.finishedCount++;
                }
                else
                {
                    this.remainingCount++;
                }
            }

            ResearchProjectDef? current = manager.GetProject();
            if (current != null && current.baseCost > 0f)
            {
                this.currentProject = current;
                this.currentProgress = current.ProgressReal;
                this.currentCost = current.Cost;
            }
        }

        // Researches the tree cheapest-available first, prerequisites before dependents, and sums the inflated costs; returns the projects counted toward inflation
        private int WalkTree(List<ResearchProjectDef> tree, Dictionary<TechLevel, EraImpact> eraMap)
        {
            HashSet<ResearchProjectDef> inTree = new HashSet<ResearchProjectDef>(tree);
            Dictionary<ResearchProjectDef, int> waiting = new Dictionary<ResearchProjectDef, int>();
            Dictionary<ResearchProjectDef, List<ResearchProjectDef>> dependents = new Dictionary<ResearchProjectDef, List<ResearchProjectDef>>();
            SortedSet<ResearchProjectDef> available = new SortedSet<ResearchProjectDef>(CheapestFirst.Instance);
            SortedSet<ResearchProjectDef> remaining = new SortedSet<ResearchProjectDef>(tree, CheapestFirst.Instance);

            foreach (ResearchProjectDef proj in tree)
            {
                HashSet<ResearchProjectDef> prereqs = new HashSet<ResearchProjectDef>();
                AddPrerequisites(proj.prerequisites, inTree, prereqs);
                AddPrerequisites(proj.hiddenPrerequisites, inTree, prereqs);
                prereqs.Remove(proj);
                waiting[proj] = prereqs.Count;
                foreach (ResearchProjectDef prereq in prereqs)
                {
                    if (!dependents.TryGetValue(prereq, out List<ResearchProjectDef> list))
                    {
                        list = new List<ResearchProjectDef>();
                        dependents[prereq] = list;
                    }

                    list.Add(proj);
                }

                if (prereqs.Count == 0)
                {
                    available.Add(proj);
                }
            }

            int counted = 0;
            while (remaining.Count > 0)
            {
                // A prerequisite loop leaves nothing available; take the cheapest remaining project to break it
                ResearchProjectDef proj = available.Count > 0 ? available.Min : remaining.Min;
                available.Remove(proj);
                remaining.Remove(proj);

                float inflated = ResearchInflationHelper.GetInflatedCost(proj, proj.baseCost, ResearchInflationHelper.GetMultiplierForCount(counted));
                this.totalInflated += inflated;
                EraImpact era = eraMap[EraOf(proj.techLevel)];
                era.projects++;
                era.vanilla += proj.baseCost;
                era.inflated += inflated;

                if (ResearchInflationHelper.CountsTowardInflation(proj))
                {
                    counted++;
                }

                if (dependents.TryGetValue(proj, out List<ResearchProjectDef> unlocked))
                {
                    foreach (ResearchProjectDef dependent in unlocked)
                    {
                        if (--waiting[dependent] == 0 && remaining.Contains(dependent))
                        {
                            available.Add(dependent);
                        }
                    }
                }
            }

            return counted;
        }

        private static void AddPrerequisites(List<ResearchProjectDef>? list, HashSet<ResearchProjectDef> inTree, HashSet<ResearchProjectDef> into)
        {
            if (list == null)
            {
                return;
            }

            foreach (ResearchProjectDef prereq in list)
            {
                if (prereq != null && inTree.Contains(prereq))
                {
                    into.Add(prereq);
                }
            }
        }

        private static TechLevel EraOf(TechLevel level)
        {
            switch (level)
            {
                case TechLevel.Medieval:
                case TechLevel.Industrial:
                case TechLevel.Spacer:
                case TechLevel.Ultra:
                    return level;
                case TechLevel.Archotech:
                    return TechLevel.Ultra;
                default:
                    return TechLevel.Neolithic;
            }
        }

        private class CheapestFirst : IComparer<ResearchProjectDef>
        {
            public static readonly CheapestFirst Instance = new CheapestFirst();

            public int Compare(ResearchProjectDef x, ResearchProjectDef y)
            {
                int byCost = x.baseCost.CompareTo(y.baseCost);
                return byCost != 0 ? byCost : string.CompareOrdinal(x.defName, y.defName);
            }
        }
    }
}
