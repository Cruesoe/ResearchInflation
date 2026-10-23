using System.Collections.Generic;
using Verse;

namespace ResearchInflation
{
    // Saved by class name; do not rename
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.finishedProjects ??= new List<ResearchProjectDef>();
                this.finishedProjects.RemoveAll(proj => proj == null);
                if (this.progressModelVersion >= ResearchInflationHelper.CurrentSaveVersion)
                {
                    ResearchInflationHelper.LoadFinishedProjects(this.finishedProjects);
                }
            }
        }

        // Setup runs earlier, from the Game.FinalizeInit prefix; this catches cost changes made while the game was closed
        public override void FinalizeInit()
        {
            ResearchInflationHelper.ReconcileProgress();
        }
    }
}
