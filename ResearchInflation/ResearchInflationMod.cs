using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace ResearchInflation
{
    public class ResearchInflationMod : Mod
    {
        public static ResearchInflationSettings settings;

        private static readonly Color TitleColor = new Color(1f, 0.85f, 0.4f);
        private static readonly Color MutedColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color CardBg = new Color(0.12f, 0.13f, 0.14f, 0.55f);
        private static readonly Color ExtraLow = new Color(0.72f, 0.78f, 0.58f);
        private static readonly Color ExtraMid = new Color(0.95f, 0.72f, 0.38f);
        private static readonly Color ExtraHigh = new Color(0.95f, 0.48f, 0.36f);
        private static readonly Color BarBg = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color BarFill = new Color(0.42f, 0.68f, 0.42f);
        private const float SectionTitleHeight = 48f;

        private string bufferThreshold;
        private string bufferNeo;
        private string bufferMed;
        private string bufferInd;
        private string bufferSpa;
        private string bufferUlt;
        private Vector2 scrollPosition = Vector2.zero;
        private float scrollHeight = 0f;

        public ResearchInflationMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ResearchInflationSettings>();
            Harmony harmony = new Harmony("com.researchinflation.core");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Research Inflation";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ResearchInflationHelper.InvalidateCache();
            ResearchInflationHelper.SnapFinishedProgress();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, this.scrollHeight > inRect.height ? this.scrollHeight : inRect.height);
            Widgets.BeginScrollView(inRect, ref this.scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.maxOneColumn = true;
            listing.Begin(viewRect);

            ResearchInflationImpact impact = ResearchInflationImpact.Build();

            DrawBoxed(listing, SettingsHeight(), box => DrawSettingsBox(box));
            DrawBoxed(listing, ImpactHeight(impact), box => DrawImpactBox(box, impact));
            DrawButtons(listing);

            this.scrollHeight = listing.CurHeight + 12f;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawSettingsBox(Listing_Standard box)
        {
            DrawSectionTitle(box, "Settings", "Each finished tech makes leftover research more expensive. Progress already earned is kept.");

            settings.inflationRate = DrawSliderSetting(
                box,
                "Inflation per finished tech",
                settings.inflationRate,
                0f,
                10f,
                settings.inflationRate.ToString("0.00") + "%",
                "Compounding increase applied to remaining projects each time a counted tech is finished.");

            string capDisplay = settings.maxMultiplier <= 0f ? "No cap" : settings.maxMultiplier.ToString("0.0") + "x";
            settings.maxMultiplier = DrawSliderSetting(
                box,
                "Maximum multiplier",
                settings.maxMultiplier,
                0f,
                50f,
                capDisplay,
                "Stops late-game costs exploding on huge modded trees. 0 means unlimited.");

            Rect thresh = box.GetRect(28f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(thresh.LeftPart(0.62f), "Ignore techs costing less than");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(thresh.RightPart(0.38f).ContractedBy(0f, 2f), ref settings.ignoreThreshold, ref this.bufferThreshold, 0f, 10000f);
            TooltipHandler.TipRegion(thresh, "Projects below this vanilla cost are not inflated and do not count toward the multiplier.");

            box.Gap(6f);
            Rect divider = box.GetRect(6f);
            Widgets.DrawLineHorizontal(divider.x, divider.y + 2f, divider.width);

            box.CheckboxLabeled("Add extra cost by era", ref settings.useCustomEraCosts);
            if (!settings.useCustomEraCosts)
            {
                return;
            }

            if (box.RadioButton("Add to vanilla cost", settings.eraCostMode == EraCostMode.Additive, 0f, "Each era value is added to the project's vanilla cost before inflation."))
            {
                settings.eraCostMode = EraCostMode.Additive;
            }

            if (box.RadioButton("Replace vanilla cost", settings.eraCostMode == EraCostMode.Replace, 0f, "Each era value becomes the project's new base cost before inflation."))
            {
                settings.eraCostMode = EraCostMode.Replace;
            }

            box.Gap(2f);
            DrawEraPair(box, "Neolithic", ref settings.valNeolithic, ref this.bufferNeo, "Medieval", ref settings.valMedieval, ref this.bufferMed);
            DrawEraPair(box, "Industrial", ref settings.valIndustrial, ref this.bufferInd, "Spacer", ref settings.valSpacer, ref this.bufferSpa);

            Rect ultraRow = box.GetRect(26f);
            float half = (ultraRow.width - 10f) / 2f;
            DrawEraField(new Rect(ultraRow.x, ultraRow.y, half, ultraRow.height), "Ultra / Archotech", ref settings.valUltra, ref this.bufferUlt);
        }

        private static void DrawImpactBox(Listing_Standard box, ResearchInflationImpact impact)
        {
            if (!impact.defsReady)
            {
                DrawSectionTitle(box, "Impact", "Research defs have not loaded yet.");
                return;
            }

            if (impact.inColony)
            {
                DrawSectionTitle(box, "This colony", impact.treeCount + " techs in the loaded tree · typical project " + Pts(impact.medianCost) + " points");
                DrawColonyImpact(box, impact);
            }
            else
            {
                DrawSectionTitle(box, "Loaded tree", impact.treeCount + " techs totaling " + Pts(impact.treeVanilla) + " points · typical project " + Pts(impact.medianCost));
                DrawMenuImpact(box, impact);
            }
        }

        private static void DrawColonyImpact(Listing_Standard box, ResearchInflationImpact impact)
        {
            float extra = impact.remainingInflated - impact.remainingVanilla;
            float extraPct = impact.remainingVanilla > 0f ? extra / impact.remainingVanilla : 0f;

            Rect cards = box.GetRect(58f);
            float gap = 6f;
            float cardW = (cards.width - gap * 2f) / 3f;
            DrawStatCard(new Rect(cards.x, cards.y, cardW, cards.height), impact.multiplier.ToString("0.00") + "x", "Multiplier", TitleColor, "Counted finished techs: " + impact.countedFinished);
            DrawStatCard(new Rect(cards.x + cardW + gap, cards.y, cardW, cards.height), Pct(extra, impact.remainingVanilla), "Extra cost", ExtraColor(extraPct), "Remaining tree is " + Signed(extra) + " points above vanilla.");
            DrawStatCard(new Rect(cards.x + (cardW + gap) * 2f, cards.y, cardW, cards.height), Pts(impact.remainingWork), "Work left", Color.white, "Research points still needed on unfinished projects.");
            box.Gap(6f);

            DrawKV(box, "Progress", impact.finishedCount + " finished  ·  " + impact.remainingCount + " remaining");
            if (impact.remainingCount <= 0)
            {
                DrawKV(box, "Remaining tree", "Nothing left to inflate");
                return;
            }

            DrawKV(box, "Remaining tree", Pts(impact.remainingVanilla) + "  →  " + Pts(impact.remainingInflated), ExtraColor(extraPct));

            if (impact.currentProject != null && impact.currentInflated > 0f)
            {
                DrawKV(box, "Current project", impact.currentProject.LabelCap + "  ·  vanilla " + Pts(impact.currentVanilla));
                DrawProgressBar(box, impact.currentProgress, impact.currentInflated);
            }

            if (impact.priciestRemaining != null)
            {
                DrawKV(box, "Most expensive left", impact.priciestRemaining.LabelCap + "  " + Pts(impact.priciestVanilla) + " → " + Pts(impact.priciestInflated));
            }

            float delta = impact.afterNextListed - impact.othersListedNow;
            if (impact.nextFinishKnown)
            {
                DrawKV(box, "If " + impact.nextFinishName + " finishes", Pts(impact.othersListedNow) + "  →  " + Pts(impact.afterNextListed) + "  (" + Signed(delta) + ")", ExtraColor(impact.othersListedNow > 0f ? delta / impact.othersListedNow : 0f));
            }
            else
            {
                DrawKV(box, "Next counted finish", Pts(impact.othersListedNow) + "  →  " + Pts(impact.afterNextListed) + "  (" + Signed(delta) + ")", ExtraColor(impact.othersListedNow > 0f ? delta / impact.othersListedNow : 0f), "Approximate: that tech is not removed from the remaining total.");
            }

            if (impact.eras.Count > 0)
            {
                box.Gap(4f);
                DrawTableHeader(box, "Era", "Techs", "Vanilla", "Now", "Extra");
                for (int i = 0; i < impact.eras.Count; i++)
                {
                    EraImpact era = impact.eras[i];
                    DrawTableRow(box, era.label, era.remaining.ToString(), Pts(era.vanilla), Pts(era.inflated), Signed(era.inflated - era.vanilla), i % 2 == 0);
                }
            }
        }

        private static void DrawMenuImpact(Listing_Standard box, ResearchInflationImpact impact)
        {
            box.Gap(2f);
            Rect note = box.GetRect(18f);
            Text.Font = GameFont.Tiny;
            GUI.color = MutedColor;
            Widgets.Label(note, "No colony loaded. Projections assume the cheapest techs are finished first.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (impact.typicalProject != null)
            {
                float cost25 = ResearchInflationHelper.GetInflatedCost(impact.typicalProject, impact.typicalProject.baseCost, ResearchInflationHelper.GetMultiplierForCount(25));
                float cost50 = ResearchInflationHelper.GetInflatedCost(impact.typicalProject, impact.typicalProject.baseCost, ResearchInflationHelper.GetMultiplierForCount(50));
                float cost100 = ResearchInflationHelper.GetInflatedCost(impact.typicalProject, impact.typicalProject.baseCost, ResearchInflationHelper.GetMultiplierForCount(100));
                DrawKV(box, "Typical project", impact.typicalProject.LabelCap + "  ·  " + Pts(impact.typicalProject.baseCost) + " vanilla");
                DrawKV(box, "After 25 / 50 / 100 counted", Pts(cost25) + "   ·   " + Pts(cost50) + "   ·   " + Pts(cost100));
            }

            if (impact.projections.Count > 0)
            {
                box.Gap(4f);
                DrawTableHeader(box, "If cheapest…", "Counted", "Mult", "Remaining", "Extra");
                for (int i = 0; i < impact.projections.Count; i++)
                {
                    ResearchInflationImpact.Projection p = impact.projections[i];
                    DrawTableRow(
                        box,
                        p.finished + " done",
                        p.counted.ToString(),
                        p.multiplier.ToString("0.00") + "x",
                        Pts(p.inflated),
                        Pct(p.inflated - p.vanilla, p.vanilla),
                        i % 2 == 0,
                        Pts(p.vanilla) + " vanilla → " + Pts(p.inflated) + " inflated for " + p.remaining + " remaining techs.");
                }
            }
        }

        private void DrawButtons(Listing_Standard listing)
        {
            listing.Gap(4f);
            Rect row = listing.GetRect(32f);
            float gap = 8f;
            Rect left = new Rect(row.x, row.y, (row.width - gap) / 2f, row.height);
            Rect right = new Rect(left.xMax + gap, row.y, left.width, row.height);

            if (Widgets.ButtonText(left, "Reset to defaults"))
            {
                settings.ResetToDefaults();
                this.bufferThreshold = null;
                this.bufferNeo = null;
                this.bufferMed = null;
                this.bufferInd = null;
                this.bufferSpa = null;
                this.bufferUlt = null;
                ResearchInflationHelper.InvalidateCache();
                ResearchInflationHelper.SnapFinishedProgress();
            }

            if (Widgets.ButtonText(right, "Recalculate now"))
            {
                ResearchInflationHelper.InvalidateCache();
                ResearchInflationHelper.SnapFinishedProgress();
            }
        }

        private static void DrawBoxed(Listing_Standard listing, float height, Action<Listing_Standard> content)
        {
            Listing_Standard box = listing.BeginSection(height, 8f, 8f);
            box.maxOneColumn = true;
            box.verticalSpacing = 4f;
            content(box);
            listing.EndSection(box);
            listing.Gap(10f);
        }

        private static void DrawSectionTitle(Listing_Standard listing, string title, string subtitle)
        {
            Rect rect = listing.GetRect(SectionTitleHeight);
            Widgets.DrawLightHighlight(rect);

            Rect titleRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 22f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TitleColor;
            Widgets.Label(titleRect, title);

            Text.Font = GameFont.Tiny;
            Rect subtitleRect = new Rect(rect.x + 6f, titleRect.yMax, rect.width - 12f, Text.LineHeight + 2f);
            GUI.color = MutedColor;
            Widgets.Label(subtitleRect, subtitle);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            listing.Gap(6f);
        }

        private static float DrawSliderSetting(Listing_Standard listing, string label, float value, float min, float max, string display, string tip)
        {
            Rect row = listing.GetRect(22f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.LeftPart(0.68f), label);
            GUI.color = TitleColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(row.RightPart(0.32f), display);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(row, tip);
            value = listing.Slider(value, min, max);
            if (max <= 10f)
            {
                return (float)Math.Round(value, 2);
            }

            return (float)Math.Round(value, 1);
        }

        private static void DrawStatCard(Rect rect, string value, string caption, Color valueColor, string tip)
        {
            Widgets.DrawBoxSolid(rect, CardBg);
            Widgets.DrawBox(rect);
            Rect inner = rect.ContractedBy(6f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = valueColor;
            Widgets.Label(inner, value);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = MutedColor;
            Widgets.Label(inner, caption);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, tip);
        }

        private static void DrawKV(Listing_Standard listing, string key, string value, Color? valueColor = null, string tip = null)
        {
            Rect rect = listing.GetRect(22f);
            Widgets.DrawHighlightIfMouseover(rect);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = MutedColor;
            Widgets.Label(rect.LeftPart(0.42f), key);
            GUI.color = valueColor ?? Color.white;
            Widgets.Label(rect.RightPart(0.58f), value);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (!tip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private static void DrawProgressBar(Listing_Standard listing, float current, float total)
        {
            Rect rect = listing.GetRect(18f);
            float pct = total > 0f ? Mathf.Clamp01(current / total) : 0f;
            Widgets.DrawBoxSolid(rect, BarBg);
            if (pct > 0f)
            {
                Widgets.DrawBoxSolid(rect.LeftPart(pct), BarFill);
            }

            Widgets.DrawBox(rect);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, Pts(current) + " / " + Pts(total));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawTableHeader(Listing_Standard listing, string a, string b, string c, string d, string e)
        {
            Rect rect = listing.GetRect(20f);
            Widgets.DrawTitleBG(rect);
            Text.Font = GameFont.Tiny;
            GUI.color = MutedColor;
            DrawFive(rect, a, b, c, d, e, false);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void DrawTableRow(Listing_Standard listing, string a, string b, string c, string d, string e, bool stripe, string tip = null)
        {
            Rect rect = listing.GetRect(20f);
            if (stripe)
            {
                Widgets.DrawLightHighlight(rect);
            }

            Widgets.DrawHighlightIfMouseover(rect);
            Text.Font = GameFont.Tiny;
            DrawFive(rect, a, b, c, d, e, true);
            Text.Font = GameFont.Small;
            if (!tip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private static void DrawFive(Rect rect, string a, string b, string c, string d, string e, bool rightAlignValues)
        {
            rect = rect.ContractedBy(4f, 0f);
            float[] weights = { 0.28f, 0.14f, 0.20f, 0.20f, 0.18f };
            float x = rect.x;
            string[] cells = { a, b, c, d, e };
            for (int i = 0; i < 5; i++)
            {
                Rect cell = new Rect(x, rect.y, rect.width * weights[i], rect.height);
                Text.Anchor = (rightAlignValues && i > 0) ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                Widgets.Label(cell, cells[i]);
                x += cell.width;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawEraPair(Listing_Standard listing, string leftLabel, ref float leftValue, ref string leftBuffer, string rightLabel, ref float rightValue, ref string rightBuffer)
        {
            Rect row = listing.GetRect(26f);
            float gap = 10f;
            Rect left = new Rect(row.x, row.y, (row.width - gap) / 2f, row.height);
            DrawEraField(left, leftLabel, ref leftValue, ref leftBuffer);
            Rect right = new Rect(left.xMax + gap, row.y, left.width, row.height);
            DrawEraField(right, rightLabel, ref rightValue, ref rightBuffer);
        }

        private static void DrawEraField(Rect rect, string label, ref float value, ref string buffer)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(0.55f), label);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(rect.RightPart(0.45f), ref value, ref buffer, 0f, 1000000f);
        }

        private float SettingsHeight()
        {
            float height = SectionTitleHeight + 6f + 48f + 48f + 28f + 6f + 6f + 28f + 8f;
            if (settings.useCustomEraCosts)
            {
                height += 28f * 2f + 4f + 26f * 3f;
            }

            return height;
        }

        private static float ImpactHeight(ResearchInflationImpact impact)
        {
            float height = SectionTitleHeight + 6f;
            if (!impact.defsReady)
            {
                return height + 24f;
            }

            if (!impact.inColony)
            {
                height += 18f + 4f;
                height += 22f * 2f;
                height += 4f + 20f + impact.projections.Count * 20f;
                return height + 12f;
            }

            height += 58f + 6f;
            height += 22f * 2f;
            if (impact.remainingCount <= 0)
            {
                return height + 12f;
            }

            if (impact.currentProject != null && impact.currentInflated > 0f)
            {
                height += 22f + 18f;
            }

            if (impact.priciestRemaining != null)
            {
                height += 22f;
            }

            height += 22f;
            if (impact.eras.Count > 0)
            {
                height += 4f + 20f + impact.eras.Count * 20f;
            }

            return height + 12f;
        }

        private static string Pts(float value)
        {
            return Mathf.RoundToInt(value).ToString("N0");
        }

        private static string Signed(float value)
        {
            float rounded = Mathf.Round(value);
            if (rounded > 0f)
            {
                return "+" + Pts(rounded);
            }

            return Pts(rounded);
        }

        private static string Pct(float extra, float vanilla)
        {
            if (vanilla <= 0f)
            {
                return "0%";
            }

            float percent = extra / vanilla * 100f;
            string sign = percent > 0.05f ? "+" : "";
            return sign + percent.ToString("0.0") + "%";
        }

        private static Color ExtraColor(float fraction)
        {
            if (fraction <= 0.001f)
            {
                return Color.white;
            }

            if (fraction < 0.25f)
            {
                return ExtraLow;
            }

            if (fraction < 0.75f)
            {
                return ExtraMid;
            }

            return ExtraHigh;
        }
    }
}
