using HarmonyLib;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    public class ResearchInflationMod : Mod
    {
        public static ResearchInflationSettings Settings { get; private set; } = new ResearchInflationSettings();

        private readonly SettingsPage page = new SettingsPage();

        public ResearchInflationMod(ModContentPack content) : base(content)
        {
            Settings = this.GetSettings<ResearchInflationSettings>();
            new Harmony("com.researchinflation.core").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "ResearchInflation_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            this.page.Draw(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ResearchInflationHelper.InvalidateCache();
            ResearchInflationHelper.ReconcileProgress();
        }
    }
}
