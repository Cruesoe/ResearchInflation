using System.Collections.Generic;
using Verse;

namespace ResearchInflation
{
    public class GameComponent_ResearchInflation : GameComponent
    {
        public int progressModelVersion;
        public List<ResearchProjectDef> finishedProjects = new List<ResearchProjectDef>();

        public GameComponent_ResearchInflation(Game game)
        {
            ResearchInflationHelper.DetachFromGame();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                this.progressModelVersion = ResearchInflationHelper.CurrentSaveVersion;
                this.finishedProjects = ResearchInflationHelper.CopyFinishedProjects();
            }

            Scribe_Values.Look(ref this.progressModelVersion, "progressModelVersion", 0);
            Scribe_Collections.Look(ref this.finishedProjects, "finishedProjects", LookMode.Def);

            if (this.finishedProjects == null)
            {
                this.finishedProjects = new List<ResearchProjectDef>();
            }

            this.finishedProjects.RemoveAll(proj => proj == null);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && this.progressModelVersion >= ResearchInflationHelper.CurrentSaveVersion)
            {
                ResearchInflationHelper.LoadFinishedProjects(this.finishedProjects);
            }
        }

        public override void FinalizeInit()
        {
            ResearchInflationHelper.InitializeForGame(this);
        }
    }
}
