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

        private string bufferThreshold;
        private string bufferRate;
        private string bufferCap;
        private string bufferNeo;
        private string bufferMed;
        private string bufferInd;
        private string bufferSpa;
        private string bufferUlt;

        private bool impactInitialized;
        private ResearchInflationImpact cachedImpact;
        private float lastRate;
        private float lastCap;
        private float lastThreshold;
        private bool lastEraToggle;
        private EraCostMode lastEraMode;
        private float lastNeo;
        private float lastMed;
        private float lastInd;
        private float lastSpa;
        private float lastUlt;
        private int lastFinishedCount = -1;

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
            const float gap = 6f;
            const float buttonsH = 28f;
            float settingsH = CompactSettingsHeight();

            Rect settingsRect = new Rect(inRect.x, inRect.y, inRect.width, settingsH);
            DrawCompactSettings(settingsRect);

            ResearchInflationImpact impact = GetOrBuildImpact();

            Rect buttonsRect = new Rect(inRect.x, inRect.yMax - buttonsH, inRect.width, buttonsH);
            DrawButtons(buttonsRect);

            Rect dashOut = new Rect(inRect.x, settingsRect.yMax + gap, inRect.width, buttonsRect.y - gap - settingsRect.yMax - gap);
            if (dashOut.height > 0f)
            {
                Widgets.DrawMenuSection(dashOut);
                DrawDashboard(dashOut.ContractedBy(6f), impact);
            }
        }

        private ResearchInflationImpact GetOrBuildImpact()
        {
            bool settingsChanged = !this.impactInitialized
                || settings.inflationRate != this.lastRate
                || settings.maxMultiplier != this.lastCap
                || settings.ignoreThreshold != this.lastThreshold
                || settings.useCustomEraCosts != this.lastEraToggle
                || settings.eraCostMode != this.lastEraMode
                || settings.valNeolithic != this.lastNeo
                || settings.valMedieval != this.lastMed
                || settings.valIndustrial != this.lastInd
                || settings.valSpacer != this.lastSpa
                || settings.valUltra != this.lastUlt;

            bool colonyChanged = ResearchInflationHelper.GetFinishedCount() != this.lastFinishedCount;

            if (settingsChanged || colonyChanged)
            {
                ResearchInflationHelper.InvalidateCache();
                ResearchInflationHelper.SnapFinishedProgress();
                this.cachedImpact = ResearchInflationImpact.Build();

                this.lastRate = settings.inflationRate;
                this.lastCap = settings.maxMultiplier;
                this.lastThreshold = settings.ignoreThreshold;
                this.lastEraToggle = settings.useCustomEraCosts;
                this.lastEraMode = settings.eraCostMode;
                this.lastNeo = settings.valNeolithic;
                this.lastMed = settings.valMedieval;
                this.lastInd = settings.valIndustrial;
                this.lastSpa = settings.valSpacer;
                this.lastUlt = settings.valUltra;
                this.lastFinishedCount = ResearchInflationHelper.GetFinishedCount();
                this.impactInitialized = true;
            }

            return this.cachedImpact;
        }

        private void DrawCompactSettings(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f, 5f);
            float rowH = 24f;
            float y = inner.y;
            float colGap = 10f;
            float col = (inner.width - colGap) / 2f;

            DrawNumeric(new Rect(inner.x, y, col, rowH), "Inflation %", ref settings.inflationRate, ref this.bufferRate, 0f, 10f, "Compounding increase per counted finished tech. Progress already earned is kept.");
            DrawNumeric(new Rect(inner.x + col + colGap, y, col, rowH), "Max multiplier", ref settings.maxMultiplier, ref this.bufferCap, 0f, 50f, "Caps the inflation multiplier. 0 means unlimited.");
            y += rowH + 3f;

            DrawNumeric(new Rect(inner.x, y, col, rowH), "Ignore below", ref settings.ignoreThreshold, ref this.bufferThreshold, 0f, 10000f, "Projects below this vanilla cost are not inflated and do not count toward the multiplier.");
            Widgets.CheckboxLabeled(new Rect(inner.x + col + colGap, y, col, rowH), "Era extras", ref settings.useCustomEraCosts);
            y += rowH + 3f;

            if (!settings.useCustomEraCosts)
            {
                return;
            }

            Rect modeRow = new Rect(inner.x, y, inner.width, rowH);
            if (Widgets.RadioButtonLabeled(modeRow.LeftHalf(), "Add to vanilla", settings.eraCostMode == EraCostMode.Additive))
            {
                settings.eraCostMode = EraCostMode.Additive;
            }

            if (Widgets.RadioButtonLabeled(modeRow.RightHalf(), "Replace vanilla", settings.eraCostMode == EraCostMode.Replace))
            {
                settings.eraCostMode = EraCostMode.Replace;
            }

            y += rowH + 3f;
            float eraW = inner.width / 5f;
            DrawEraField(new Rect(inner.x, y, eraW - 4f, rowH), "Neo", ref settings.valNeolithic, ref this.bufferNeo);
            DrawEraField(new Rect(inner.x + eraW, y, eraW - 4f, rowH), "Med", ref settings.valMedieval, ref this.bufferMed);
            DrawEraField(new Rect(inner.x + eraW * 2f, y, eraW - 4f, rowH), "Ind", ref settings.valIndustrial, ref this.bufferInd);
            DrawEraField(new Rect(inner.x + eraW * 3f, y, eraW - 4f, rowH), "Spa", ref settings.valSpacer, ref this.bufferSpa);
            DrawEraField(new Rect(inner.x + eraW * 4f, y, eraW - 4f, rowH), "Ultra", ref settings.valUltra, ref this.bufferUlt);
        }

        private static void DrawNumeric(Rect rect, string label, ref float value, ref string buffer, float min, float max, string tip)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(0.42f), label);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(rect.RightPart(0.58f).ContractedBy(0f, 1f), ref value, ref buffer, min, max);
            TooltipHandler.TipRegion(rect, tip);
        }

        private static void DrawDashboard(Rect rect, ResearchInflationImpact impact)
        {
            if (!impact.defsReady)
            {
                Widgets.Label(rect, "Impact on your tree will appear once research defs have loaded.");
                return;
            }

            float extra = impact.completeTotal - impact.treeVanilla;
            float extraPct = impact.treeVanilla > 0f ? extra / impact.treeVanilla : 0f;
            float cardsH = 52f;
            DrawTopBoxes(
                new Rect(rect.x, rect.y, rect.width, cardsH),
                Pts(impact.completeTotal),
                "Final total",
                ExtraColor(extraPct),
                "Research points to finish every standard tech, compounding as each counted project completes. Order: cheapest currently available, prerequisites first. This number follows your settings, not colony progress.",
                Pct(extra, impact.treeVanilla),
                "Extra vs vanilla",
                ExtraColor(extraPct),
                Pts(impact.treeVanilla) + " vanilla  →  " + Pts(impact.completeTotal) + " inflated.",
                impact.endMultiplier.ToString("0.00") + "x",
                "Finish multiplier",
                TitleColor,
                "Multiplier after the last counted tech (" + impact.endCounted + " counted finishes).");

            Listing_Standard listing = new Listing_Standard();
            listing.maxOneColumn = true;
            listing.verticalSpacing = 2f;
            listing.Begin(new Rect(rect.x, rect.y + cardsH + 6f, rect.width, rect.height - cardsH - 6f));

            DrawKV(listing, "Research tree", impact.treeCount + " projects  ·  " + Pts(impact.treeVanilla) + " vanilla");
            if (impact.inColony)
            {
                DrawKV(listing, "Progress", impact.finishedCount + " finished  ·  " + impact.remainingCount + " remaining  ·  now " + impact.multiplier.ToString("0.00") + "x");
                if (impact.currentProject != null && impact.currentInflated > 0f)
                {
                    DrawKV(listing, "Current project", impact.currentProject.LabelCap + "  ·  vanilla " + Pts(impact.currentVanilla));
                    DrawProgressBar(listing, impact.currentProgress, impact.currentInflated);
                }
            }
            else
            {
                DrawKV(listing, "No colony loaded", "Final total assumes you start from zero and finish the whole tree.");
            }

            if (impact.eras.Count > 0)
            {
                listing.Gap(6f);
                DrawTableHeader(listing, "Era", "Techs", "Vanilla", "Final", "Extra");
                for (int i = 0; i < impact.eras.Count; i++)
                {
                    EraImpact era = impact.eras[i];
                    DrawTableRow(listing, era.label, era.techs.ToString(), Pts(era.vanilla), Pts(era.inflated), Signed(era.inflated - era.vanilla), i % 2 == 0);
                }

                DrawTableTotal(listing, "Total", impact.treeCount.ToString(), Pts(impact.treeVanilla), Pts(impact.completeTotal), Signed(extra));
            }

            listing.End();
        }

        private void DrawButtons(Rect row)
        {
            float split = 8f;
            Rect left = new Rect(row.x, row.y, (row.width - split) / 2f, row.height);
            Rect right = new Rect(left.xMax + split, row.y, left.width, row.height);

            if (Widgets.ButtonText(left, "Reset to defaults"))
            {
                settings.ResetToDefaults();
                this.bufferRate = null;
                this.bufferCap = null;
                this.bufferThreshold = null;
                this.bufferNeo = null;
                this.bufferMed = null;
                this.bufferInd = null;
                this.bufferSpa = null;
                this.bufferUlt = null;
                ResearchInflationHelper.InvalidateCache();
                ResearchInflationHelper.SnapFinishedProgress();
                this.impactInitialized = false;
            }

            if (Widgets.ButtonText(right, "Recalculate now"))
            {
                ResearchInflationHelper.InvalidateCache();
                ResearchInflationHelper.SnapFinishedProgress();
                this.impactInitialized = false;
            }
        }

        private static void DrawTopBoxes(
            Rect rect,
            string aValue, string aCaption, Color aColor, string aTip,
            string bValue, string bCaption, Color bColor, string bTip,
            string cValue, string cCaption, Color cColor, string cTip)
        {
            float gap = 6f;
            float cardW = (rect.width - gap * 2f) / 3f;
            DrawStatCard(new Rect(rect.x, rect.y, cardW, rect.height), aValue, aCaption, aColor, aTip);
            DrawStatCard(new Rect(rect.x + cardW + gap, rect.y, cardW, rect.height), bValue, bCaption, bColor, bTip);
            DrawStatCard(new Rect(rect.x + (cardW + gap) * 2f, rect.y, cardW, rect.height), cValue, cCaption, cColor, cTip);
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
            Widgets.Label(rect.LeftPart(0.28f), key);
            GUI.color = valueColor ?? Color.white;
            Widgets.Label(rect.RightPart(0.72f), value);
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
            Rect rect = listing.GetRect(24f);
            Widgets.DrawTitleBG(rect);
            GUI.color = MutedColor;
            DrawFive(rect, a, b, c, d, e);
            GUI.color = Color.white;
        }

        private static void DrawTableTotal(Listing_Standard listing, string a, string b, string c, string d, string e)
        {
            Rect rect = listing.GetRect(24f);
            Widgets.DrawTitleBG(rect);
            GUI.color = TitleColor;
            DrawFive(rect, a, b, c, d, e);
            GUI.color = Color.white;
        }

        private static void DrawTableRow(Listing_Standard listing, string a, string b, string c, string d, string e, bool stripe, string tip = null)
        {
            Rect rect = listing.GetRect(24f);
            if (stripe)
            {
                Widgets.DrawLightHighlight(rect);
            }

            Widgets.DrawHighlightIfMouseover(rect);
            DrawFive(rect, a, b, c, d, e);
            if (!tip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private static void DrawFive(Rect rect, string a, string b, string c, string d, string e)
        {
            rect = rect.ContractedBy(8f, 0f);
            float[] widths = { 0.24f, 0.12f, 0.22f, 0.22f, 0.20f };
            float x = rect.x;
            string[] cells = { a, b, c, d, e };
            for (int i = 0; i < 5; i++)
            {
                Rect cell = new Rect(x, rect.y, rect.width * widths[i], rect.height);
                Text.Anchor = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                Widgets.Label(cell.ContractedBy(6f, 0f), cells[i]);
                x += cell.width;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawEraField(Rect rect, string label, ref float value, ref string buffer)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(0.55f), label);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(rect.RightPart(0.45f), ref value, ref buffer, 0f, 1000000f);
        }

        private float CompactSettingsHeight()
        {
            float height = 10f + 24f + 3f + 24f + 4f;
            if (settings.useCustomEraCosts)
            {
                height += 24f + 3f + 24f;
            }

            return height;
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
