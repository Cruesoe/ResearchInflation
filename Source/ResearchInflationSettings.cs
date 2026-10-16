using RimWorld;
using Verse;

namespace ResearchInflation
{
    public class ResearchInflationSettings : ModSettings
    {
        public const float DefaultRate = 1f;
        public const float DefaultEraValue = 10f;
        public const float MinReplaceCost = 1f;
        private const int CurrentVersion = 1;

        public float inflationRate = DefaultRate;
        public float maxMultiplier;
        public float ignoreThreshold;
        public EraCostMode eraCostMode = EraCostMode.Off;

        public float valNeolithic = DefaultEraValue;
        public float valMedieval = DefaultEraValue;
        public float valIndustrial = DefaultEraValue;
        public float valSpacer = DefaultEraValue;
        public float valUltra = DefaultEraValue;

        public void ResetToDefaults()
        {
            this.inflationRate = DefaultRate;
            this.maxMultiplier = 0f;
            this.ignoreThreshold = 0f;
            this.eraCostMode = EraCostMode.Off;
            this.valNeolithic = DefaultEraValue;
            this.valMedieval = DefaultEraValue;
            this.valIndustrial = DefaultEraValue;
            this.valSpacer = DefaultEraValue;
            this.valUltra = DefaultEraValue;
        }

        // Era value for a tech level; null for levels without an era (Undefined)
        public float? EraValue(TechLevel level)
        {
            switch (level)
            {
                case TechLevel.Animal:
                case TechLevel.Neolithic:
                    return this.valNeolithic;
                case TechLevel.Medieval:
                    return this.valMedieval;
                case TechLevel.Industrial:
                    return this.valIndustrial;
                case TechLevel.Spacer:
                    return this.valSpacer;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return this.valUltra;
                default:
                    return null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            int version = CurrentVersion;
            Scribe_Values.Look(ref version, "settingsVersion", 0);
            Scribe_Values.Look(ref this.inflationRate, "inflationRate", DefaultRate);
            Scribe_Values.Look(ref this.maxMultiplier, "maxMultiplier", 0f);
            Scribe_Values.Look(ref this.ignoreThreshold, "ignoreThreshold", 0f);
            Scribe_Values.Look(ref this.eraCostMode, "eraCostMode", EraCostMode.Off);
            Scribe_Values.Look(ref this.valNeolithic, "valNeolithic", DefaultEraValue);
            Scribe_Values.Look(ref this.valMedieval, "valMedieval", DefaultEraValue);
            Scribe_Values.Look(ref this.valIndustrial, "valIndustrial", DefaultEraValue);
            Scribe_Values.Look(ref this.valSpacer, "valSpacer", DefaultEraValue);
            Scribe_Values.Look(ref this.valUltra, "valUltra", DefaultEraValue);

            // Before version 1, era costs had a separate on/off switch and Additive was the unsaved default mode
            if (Scribe.mode == LoadSaveMode.LoadingVars && version < 1)
            {
                bool useCustomEraCosts = false;
                Scribe_Values.Look(ref useCustomEraCosts, "useCustomEraCosts", false);
                if (!useCustomEraCosts)
                {
                    this.eraCostMode = EraCostMode.Off;
                }
                else if (this.eraCostMode == EraCostMode.Off)
                {
                    this.eraCostMode = EraCostMode.Additive;
                }
            }
        }
    }
}
