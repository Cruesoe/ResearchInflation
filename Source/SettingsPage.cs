using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    // Draws the mod settings: inflation controls, era costs and the projected cost of the tree
    public class SettingsPage
    {
        private const float RowHeight = 30f;
        private const float TableRowHeight = 24f;
        private const float LabelWidthPct = 0.45f;
        private const float ValueWidth = 70f;
        private const float Gap = 8f;
        private const float ResetWidth = 180f;
        private const float MaxRate = 10f;
        private const float RateStep = 0.1f;
        private const float MaxCap = 50f;
        private const float CapStep = 0.5f;
        private const float MaxThreshold = 100000f;
        private const float MaxEraValue = 1000000f;
        private static readonly float[] ColumnWidths = { 0.24f, 0.14f, 0.2f, 0.22f, 0.2f };
        private static readonly Color DisabledColor = new Color(1f, 1f, 1f, 0.4f);

        private string? thresholdBuffer;
        private readonly string?[] eraBuffers = new string?[5];
        private object? lastState;
        private ResearchInflationImpact? impact;

        public void Draw(Rect inRect)
        {
            ResearchInflationSettings settings = ResearchInflationMod.Settings;
            Rect resetRect = new Rect(inRect.xMax - ResetWidth, inRect.yMax - RowHeight, ResetWidth, RowHeight);
            Listing_Standard list = new Listing_Standard();
            list.Begin(new Rect(inRect.x, inRect.y, inRect.width, resetRect.y - Gap - inRect.y));

            Rect rate = LabeledRow(list, "ResearchInflation_Rate".Translate(), "ResearchInflation_RateTip".Translate());
            settings.inflationRate = Slider(rate, settings.inflationRate, 0f, MaxRate, RateStep, settings.inflationRate.ToString("0.0") + "%");

            Rect cap = LabeledRow(list, "ResearchInflation_Cap".Translate(), "ResearchInflation_CapTip".Translate());
            string capText = settings.maxMultiplier >= 1f ? settings.maxMultiplier.ToString("0.0") + "x" : "ResearchInflation_CapNone".Translate().ToString();
            settings.maxMultiplier = Slider(cap, settings.maxMultiplier, 0f, MaxCap, CapStep, capText);
            if (settings.maxMultiplier < 1f)
            {
                settings.maxMultiplier = 0f;
            }

            Rect threshold = LabeledRow(list, "ResearchInflation_Threshold".Translate(), "ResearchInflation_ThresholdTip".Translate());
            Widgets.TextFieldNumeric(threshold.LeftPartPixels(ValueWidth * 1.5f), ref settings.ignoreThreshold, ref this.thresholdBuffer, 0f, MaxThreshold);

            Rect mode = LabeledRow(list, "ResearchInflation_EraMode".Translate(), "ResearchInflation_EraModeTip".Translate());
            if (Widgets.ButtonText(mode.LeftPartPixels(ResetWidth), ModeLabel(settings.eraCostMode)))
            {
                Find.WindowStack.Add(new FloatMenu(ModeOptions(settings)));
            }

            this.DrawEraValues(list.GetRect(RowHeight), settings);

            this.RefreshImpact(settings);
            if (this.impact != null)
            {
                list.GapLine();
                this.DrawImpact(list, this.impact);
            }

            list.End();

            if (Widgets.ButtonText(resetRect, "ResearchInflation_Reset".Translate()))
            {
                settings.ResetToDefaults();
                this.thresholdBuffer = null;
                for (int i = 0; i < this.eraBuffers.Length; i++)
                {
                    this.eraBuffers[i] = null;
                }
            }
        }

        private void DrawEraValues(Rect row, ResearchInflationSettings settings)
        {
            float width = row.width / 5f;
            this.EraField(new Rect(row.x, row.y, width - Gap, row.height), TechLevel.Neolithic, ref settings.valNeolithic, 0, settings.eraCostMode);
            this.EraField(new Rect(row.x + width, row.y, width - Gap, row.height), TechLevel.Medieval, ref settings.valMedieval, 1, settings.eraCostMode);
            this.EraField(new Rect(row.x + width * 2f, row.y, width - Gap, row.height), TechLevel.Industrial, ref settings.valIndustrial, 2, settings.eraCostMode);
            this.EraField(new Rect(row.x + width * 3f, row.y, width - Gap, row.height), TechLevel.Spacer, ref settings.valSpacer, 3, settings.eraCostMode);
            this.EraField(new Rect(row.x + width * 4f, row.y, width - Gap, row.height), TechLevel.Ultra, ref settings.valUltra, 4, settings.eraCostMode);
        }

        // Inert and greyed out while era costs are off
        private void EraField(Rect rect, TechLevel level, ref float value, int index, EraCostMode mode)
        {
            string label = level.ToStringHuman().CapitalizeFirst();
            Rect labelRect = rect.LeftPart(0.55f);
            Rect fieldRect = rect.RightPart(0.45f).ContractedBy(0f, 2f);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (mode == EraCostMode.Off)
            {
                GUI.color = DisabledColor;
                Widgets.Label(labelRect, label);
                Widgets.Label(fieldRect, value.ToString("0"));
                GUI.color = Color.white;
                TooltipHandler.TipRegion(rect, "ResearchInflation_EraOffTip".Translate());
            }
            else
            {
                Widgets.Label(labelRect, label);
                float min = mode == EraCostMode.Replace ? ResearchInflationSettings.MinReplaceCost : 0f;
                Widgets.TextFieldNumeric(fieldRect, ref value, ref this.eraBuffers[index], min, MaxEraValue);
                string tip = mode == EraCostMode.Replace ? "ResearchInflation_EraReplaceTip" : "ResearchInflation_EraAddTip";
                TooltipHandler.TipRegion(rect, tip.Translate(label));
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Rebuilds the projection and resets finished progress whenever a setting or the colony's finished count changes
        private void RefreshImpact(ResearchInflationSettings settings)
        {
            object state = (settings.inflationRate, settings.maxMultiplier, settings.ignoreThreshold, settings.eraCostMode, settings.valNeolithic, settings.valMedieval, settings.valIndustrial, settings.valSpacer, settings.valUltra, ResearchInflationHelper.GetFinishedCount());
            if (this.impact != null && state.Equals(this.lastState))
            {
                return;
            }

            this.lastState = state;
            ResearchInflationHelper.InvalidateCache();
            ResearchInflationHelper.SnapFinishedProgress();
            this.impact = DefDatabase<ResearchProjectDef>.DefCount > 0 ? ResearchInflationImpact.Build() : null;
        }

        private void DrawImpact(Listing_Standard list, ResearchInflationImpact impact)
        {
            list.Label("ResearchInflation_TreeHeading".Translate(impact.projectCount));
            TableRow(list.GetRect(TableRowHeight), RowStyle.Header, "ResearchInflation_ColEra".Translate(), "ResearchInflation_ColProjects".Translate(), "ResearchInflation_ColVanilla".Translate(), "ResearchInflation_ColInflated".Translate(), "ResearchInflation_ColExtra".Translate());
            for (int i = 0; i < impact.eras.Count; i++)
            {
                EraImpact era = impact.eras[i];
                TableRow(list.GetRect(TableRowHeight), i % 2 == 0 ? RowStyle.Stripe : RowStyle.Plain, era.label, era.projects.ToString(), Points(era.vanilla), Points(era.inflated), Extra(era.inflated, era.vanilla));
            }

            TableRow(list.GetRect(TableRowHeight), RowStyle.Header, "ResearchInflation_Total".Translate(), impact.projectCount.ToString(), Points(impact.totalVanilla), Points(impact.totalInflated), Extra(impact.totalInflated, impact.totalVanilla));
            list.Label("ResearchInflation_EndMultiplier".Translate(impact.endMultiplier.ToString("0.00")));

            list.Gap(Gap);
            if (!impact.inColony)
            {
                list.Label("ResearchInflation_NoColony".Translate());
                return;
            }

            list.Label("ResearchInflation_Colony".Translate(impact.finishedCount, impact.remainingCount, impact.multiplier.ToString("0.00")));
            if (impact.currentProject != null)
            {
                Rect row = list.GetRect(TableRowHeight);
                Widgets.Label(row.LeftPart(LabelWidthPct), "ResearchInflation_Current".Translate(impact.currentProject.LabelCap));
                ProgressBar(row.RightPart(1f - LabelWidthPct).ContractedBy(0f, 2f), impact.currentProgress, impact.currentCost);
            }
        }

        private static Rect LabeledRow(Listing_Standard list, string label, string tip)
        {
            Rect row = list.GetRect(RowHeight);
            Widgets.DrawHighlightIfMouseover(row);
            TooltipHandler.TipRegion(row, tip);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.LeftPart(LabelWidthPct), label);
            Text.Anchor = TextAnchor.UpperLeft;
            return row.RightPart(1f - LabelWidthPct);
        }

        private static float Slider(Rect rect, float value, float min, float max, float step, string valueText)
        {
            Rect valueRect = rect.RightPartPixels(ValueWidth);
            Rect sliderRect = new Rect(rect.x, rect.y, rect.width - ValueWidth - Gap, rect.height);
            float result = Widgets.HorizontalSlider(sliderRect.ContractedBy(0f, (rect.height - 12f) / 2f), value, min, max, roundTo: step);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valueRect, valueText);
            Text.Anchor = TextAnchor.UpperLeft;
            return result;
        }

        private static List<FloatMenuOption> ModeOptions(ResearchInflationSettings settings)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (EraCostMode mode in new[] { EraCostMode.Off, EraCostMode.Additive, EraCostMode.Replace })
            {
                options.Add(new FloatMenuOption(ModeLabel(mode), () => settings.eraCostMode = mode));
            }

            return options;
        }

        private static string ModeLabel(EraCostMode mode)
        {
            return ("ResearchInflation_EraMode_" + mode).Translate();
        }

        private enum RowStyle
        {
            Plain,
            Stripe,
            Header
        }

        private static void TableRow(Rect rect, RowStyle style, params string[] cells)
        {
            if (style == RowStyle.Header)
            {
                Widgets.DrawTitleBG(rect);
            }
            else if (style == RowStyle.Stripe)
            {
                Widgets.DrawLightHighlight(rect);
            }

            Rect inner = rect.ContractedBy(Gap, 0f);
            float x = inner.x;
            for (int i = 0; i < cells.Length; i++)
            {
                Rect cell = new Rect(x, inner.y, inner.width * ColumnWidths[i], inner.height);
                Text.Anchor = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                Widgets.Label(cell, cells[i]);
                x += cell.width;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void ProgressBar(Rect rect, float current, float total)
        {
            Widgets.FillableBar(rect, total > 0f ? Mathf.Clamp01(current / total) : 0f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, Points(current) + " / " + Points(total));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static string Points(float value)
        {
            return Mathf.RoundToInt(value).ToString("N0");
        }

        private static string Extra(float inflated, float vanilla)
        {
            if (vanilla <= 0f)
            {
                return "-";
            }

            float percent = (inflated - vanilla) / vanilla * 100f;
            return (percent > 0.05f ? "+" : "") + percent.ToString("0") + "%";
        }
    }
}
