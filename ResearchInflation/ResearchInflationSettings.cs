using Verse;

namespace ResearchInflation
{
    public enum EraCostMode
    {
        Additive,
        Replace
    }

    public class ResearchInflationSettings : ModSettings
    {
        public float inflationRate = 1.0f;
        public float maxMultiplier = 0f;
        public bool useCustomEraCosts = false;
        public EraCostMode eraCostMode = EraCostMode.Additive;
        public float ignoreThreshold = 0f;

        public float valNeolithic = 10f;
        public float valMedieval = 10f;
        public float valIndustrial = 10f;
        public float valSpacer = 10f;
        public float valUltra = 10f;

        public void ResetToDefaults()
        {
            this.inflationRate = 1.0f;
            this.maxMultiplier = 0f;
            this.useCustomEraCosts = false;
            this.eraCostMode = EraCostMode.Additive;
            this.ignoreThreshold = 0f;
            this.valNeolithic = 10f;
            this.valMedieval = 10f;
            this.valIndustrial = 10f;
            this.valSpacer = 10f;
            this.valUltra = 10f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.inflationRate, "inflationRate", 1.0f);
            Scribe_Values.Look(ref this.maxMultiplier, "maxMultiplier", 0f);
            Scribe_Values.Look(ref this.useCustomEraCosts, "useCustomEraCosts", false);
            Scribe_Values.Look(ref this.eraCostMode, "eraCostMode", EraCostMode.Additive);
            Scribe_Values.Look(ref this.ignoreThreshold, "ignoreThreshold", 0f);
            Scribe_Values.Look(ref this.valNeolithic, "valNeolithic", 10f);
            Scribe_Values.Look(ref this.valMedieval, "valMedieval", 10f);
            Scribe_Values.Look(ref this.valIndustrial, "valIndustrial", 10f);
            Scribe_Values.Look(ref this.valSpacer, "valSpacer", 10f);
            Scribe_Values.Look(ref this.valUltra, "valUltra", 10f);
        }
    }
}
