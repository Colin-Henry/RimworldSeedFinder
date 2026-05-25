using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Profile;

namespace SeedFinder {

enum FeatureFilter {
    Present,
    NotPresent,
    Either
};

enum Hemisphere {
    Northern,
    Southern,
    Either
};

enum Seasonality {
    Any,
    Normal,
    PermSummer,
    PermWinter,
};

enum OutputMode {
    Screenshot,
    SeedText
};

class SeedFinderFilterParameters {
    public string outDirectory;
    public string baseSeed;
    public int maxFound;
    public bool clearFog;
    public bool highlightPOI;
    public float planetCoverage;
    public OverallRainfall rainfall;
    public OverallTemperature temperature;
    public LandmarkDensity landmarkDensity;
    public OverallPopulation population;
    public float pollution;
    public int mapSize;
    public List<FactionDef> factions;
    public BiomeDef biome;
    public Hilliness hilliness;
    public FeatureFilter river;
    public List<bool> desiredRivers;
    public FeatureFilter road;
    public List<bool> desiredRoads;
    public FeatureFilter coastal;
    public FeatureFilter caves;
    public Hemisphere hemisphere;
    public int maxTemp;
    public int minTemp;
    public int minGrowingDays;
    public Seasonality seasonality;
    public int minGeysers;
    public int minRichSoilTiles;
    public bool needCivilOutlanderNear;
    public bool needRoughOutlanderNear;
    public bool needCivilTribeNear;
    public bool needRoughTribeNear;
    public bool needEmpireNear;

    public OutputMode outputMode;
    public bool searchMultipleSeeds;
    public List<bool> desiredCoastDirections;
    public int minAvgTemp;
    public int maxAvgTemp;
    public int minElevation;
    public int maxElevation;
    public int minRainfall;
    public int maxRainfall;
    public FeatureFilter tilePollutionFilter;
    public float minTilePollution;
    public float maxTilePollution;

    public ThingDef[] stoneSlots;
    public bool stoneThirdEnabled;

    public Vector2 windowScroll;

    public SeedFinderFilterParameters()
    {
    }
}

class FilterWindow : Verse.Window
{
    private SeedFinderFilterParameters filterParams;

    public FilterWindow(SeedFinderFilterParameters fp)
    {
        filterParams = fp;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        resizeable = false;
        draggable = false;
    }
    public override Vector2 InitialSize => new Vector2(750f, UI.screenHeight - 100);

    public override void DoWindowContents(Rect inRect)
    {
        float curY = 0f;
        var buttonSize = new Vector2(150f, 30f);
        var labelSize = 28f;
        var largeButtonSize = new Vector2(150f, 38f);
        var rightOffset = 360f;
        var buttonOffset = 150f;
        var skipSize = 35f;
        var titleSkipSize = 45f;

        var origAnchor = Text.Anchor;
        var origFont = Text.Font;

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;
        

        Rect fullWindowRect = new Rect(0f, 0f, 690f, 1340f);
        Rect windowScrollRect = new Rect(0f, 0f, inRect.width - 8f, (inRect.height - largeButtonSize.y)-10);
        Widgets.BeginScrollView(windowScrollRect, ref filterParams.windowScroll, fullWindowRect);

        Rect titleRect = new Rect(0, 0, inRect.width, 40f);
        Widgets.Label(titleRect, "SeedFinder Settings");

        Text.Font = origFont;
        
        curY += titleSkipSize;

        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Output Mode: ");
        Func<OutputMode, string> modeToStr = (OutputMode m) => m == OutputMode.Screenshot ? "Screenshot" : "Seed List";
        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), modeToStr(filterParams.outputMode), true, true, true)) {
            var modeOptions = new List<FloatMenuOption>();
            modeOptions.Add(new FloatMenuOption(modeToStr(OutputMode.SeedText), () => { filterParams.outputMode = OutputMode.SeedText; }));
            modeOptions.Add(new FloatMenuOption(modeToStr(OutputMode.Screenshot), () => { filterParams.outputMode = OutputMode.Screenshot; }));
            Find.WindowStack.Add(new FloatMenu(modeOptions));
        }

        curY += skipSize;

        if (filterParams.outputMode == OutputMode.Screenshot) {
            Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Output Directory: ");
            filterParams.outDirectory = Widgets.TextField(new Rect(buttonOffset, curY, 300f, buttonSize.y), filterParams.outDirectory);
            if (Widgets.ButtonText(new Rect(buttonOffset + 305f, curY, buttonSize.x - 25, buttonSize.y), "Open Folder")) {
                Directory.CreateDirectory(filterParams.outDirectory);
                Process.Start(@filterParams.outDirectory);
            }

            curY += skipSize;
        }

        string seedLabel = filterParams.searchMultipleSeeds ? "Start Seed: " : "Seed: ";
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), seedLabel);
        filterParams.baseSeed = Widgets.TextField(new Rect(buttonOffset, curY, 300f, buttonSize.y), filterParams.baseSeed);

        curY += skipSize;

        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Max # Matches: ");
        var numMatchStr = filterParams.maxFound.ToString();
        Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.maxFound, ref numMatchStr, 1f, 10000f);

        curY += skipSize;

        if (filterParams.outputMode == OutputMode.Screenshot) {
            Widgets.CheckboxLabeled(new Rect(0, curY, 250, labelSize), "Clear Fog in Screenshots", ref filterParams.clearFog, disabled: false, null, null, placeCheckboxNearText: true);

            Widgets.CheckboxLabeled(new Rect(250, curY, 360, labelSize), "Highlight Map Features (Anima Tree, Geysers, etc)", ref filterParams.highlightPOI, disabled: false, null, null, placeCheckboxNearText: true);

            curY += 60f;
        } else {
            Widgets.CheckboxLabeled(new Rect(0, curY, 420, labelSize), "Search multiple seeds until max found", ref filterParams.searchMultipleSeeds, disabled: false, null, null, placeCheckboxNearText: true);
            curY += 30f;
        }

        Text.Font = GameFont.Medium;

        Widgets.Label(new Rect(0, curY, inRect.width, 25f), "Filters");

        Text.Font = origFont;

        curY += titleSkipSize;

        buttonOffset = 135f;
        
        // Biome
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Biome: ");

        var curBiome = filterParams.biome != null ? GenText.CapitalizeAsTitle(filterParams.biome.label) : "Any";
        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), curBiome, true, true, true)) {
            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("Any", () => { filterParams.biome = null; }));
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading) {
                if (!biomeDef.canBuildBase) continue;
                if (biomeDef.defName == "Underground") continue;

                var label = GenText.CapitalizeAsTitle(biomeDef.label);
                options.Add(new FloatMenuOption(label, () => {
                    filterParams.biome = biomeDef;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Hemisphere

        Func<Hemisphere, String> hemiToStr = (Hemisphere h) => {
            if (h == Hemisphere.Northern) {
                return "Northern";
            } else if (h == Hemisphere.Southern) {
                return "Southern";
            } else {
                return "Don't care";
            }
        };

        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Hemisphere: ");

        if (Widgets.ButtonText(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), hemiToStr(filterParams.hemisphere), true, true, true)) {
            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption(hemiToStr(Hemisphere.Either), () => {
                filterParams.hemisphere = Hemisphere.Either;
            }));

            options.Add(new FloatMenuOption(hemiToStr(Hemisphere.Northern), () => {
                filterParams.hemisphere = Hemisphere.Northern;
            }));

            options.Add(new FloatMenuOption(hemiToStr(Hemisphere.Southern), () => {
                filterParams.hemisphere = Hemisphere.Southern;
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        curY += skipSize;

        // Hilliness
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Hilliness: ");

        string hillinessLabel = filterParams.hilliness == Hilliness.Undefined ? "Any" : GenText.CapitalizeAsTitle(HillinessUtility.GetLabel(filterParams.hilliness));
        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), hillinessLabel, true, true, true)) {
            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("Any", () => { filterParams.hilliness = Hilliness.Undefined; }));
            var possibleHilliness = new List<Hilliness>() { Hilliness.Flat, Hilliness.SmallHills, Hilliness.LargeHills, Hilliness.Mountainous };
            foreach (var hilliness in possibleHilliness) {
                options.Add(new FloatMenuOption(GenText.CapitalizeAsTitle(HillinessUtility.GetLabel(hilliness)), () => {
                    filterParams.hilliness = hilliness;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        Func<FeatureFilter, String> filterToStr = (FeatureFilter f) => {
            if (f == FeatureFilter.Present) {
                return "Yes";
            } else if (f == FeatureFilter.NotPresent) {
                return "No";
            } else {
                return "Don't care";
            }
        };

        var featureFilters = new List<FeatureFilter>() { FeatureFilter.Either, FeatureFilter.Present, FeatureFilter.NotPresent };

        // Caves
        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Caves: ");

        if (Widgets.ButtonText(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), filterToStr(filterParams.caves), true, true, true)) {
            var options = new List<FloatMenuOption>();
            foreach (var filter in featureFilters) {
                options.Add(new FloatMenuOption(filterToStr(filter), () => {
                    filterParams.caves = filter;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        curY += skipSize;

        // Coastal
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Coastal: ");

        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), filterToStr(filterParams.coastal), true, true, true)) {
            var options = new List<FloatMenuOption>();
            foreach (var filter in featureFilters) {
                options.Add(new FloatMenuOption(filterToStr(filter), () => {
                    filterParams.coastal = filter;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (filterParams.coastal == FeatureFilter.Present) {
            var coastLabels = new string[] { "North", "South", "East", "West" };
            var coastOffset = 300f;
            for (int i = 0; i < 4; i++) {
                bool desired = filterParams.desiredCoastDirections[i];
                bool isLast = desired && filterParams.desiredCoastDirections.Count(v => v) == 1;
                Widgets.CheckboxLabeled(new Rect(coastOffset, curY + 3.5f, 150, labelSize - 3), coastLabels[i],
                                        ref desired, disabled: isLast, null, null, placeCheckboxNearText: true);
                filterParams.desiredCoastDirections[i] = desired;
                coastOffset += 50 + 6 * coastLabels[i].Length;
            }
        }

        curY += skipSize;

        // Rivers
        curY += 10f;

        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "River: ");

        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), filterToStr(filterParams.river), true, true, true)) {
            var options = new List<FloatMenuOption>();
            foreach (var filter in featureFilters) {
                options.Add(new FloatMenuOption(filterToStr(filter), () => {
                    filterParams.river = filter;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (filterParams.river == FeatureFilter.Present) {
            var offset = 300f;
            for (int idx = 0; idx < SeedFinderController.Instance.allRivers.Count; idx++) {
                var riverDef = SeedFinderController.Instance.allRivers[idx];
                bool desired = filterParams.desiredRivers[idx];
                var riverLabel = GenText.CapitalizeAsTitle(riverDef.label);

                bool isLastRiver = desired && filterParams.desiredRivers.Count(v => v) == 1;
                Widgets.CheckboxLabeled(new Rect(offset, curY + 3.5f, 150, labelSize - 3), riverLabel,
                                        ref desired, disabled: isLastRiver, null, null, placeCheckboxNearText: true);

                filterParams.desiredRivers[idx] = desired;

                var numChars = riverLabel.Length;
                offset += 50 + 6 * numChars;
            }

        }
        curY += titleSkipSize;

        // Roads
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Road: ");

        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), filterToStr(filterParams.road), true, true, true)) {
            var options = new List<FloatMenuOption>();
            foreach (var filter in featureFilters) {
                options.Add(new FloatMenuOption(filterToStr(filter), () => {
                    filterParams.road = filter;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (filterParams.road == FeatureFilter.Present) {
            var offset = 300f;
            var rowOffset = 0f;
            for (int idx = 0; idx < SeedFinderController.Instance.allRoads.Count; idx++) {
                var roadDef = SeedFinderController.Instance.allRoads[idx];
                bool desired = filterParams.desiredRoads[idx];
                var roadLabel = GenText.CapitalizeAsTitle(roadDef.label);
                var checkboxWidth = 50 + 6 * roadLabel.Length;

                if (idx == 3) {
                    offset = 0f;
                    rowOffset += labelSize;
                }

                bool isLastRoad = desired && filterParams.desiredRoads.Count(v => v) == 1;
                Widgets.CheckboxLabeled(new Rect(offset, curY + rowOffset + 3.5f, checkboxWidth, labelSize - 3), roadLabel,
                                        ref desired, disabled: isLastRoad, null, null, placeCheckboxNearText: true);

                filterParams.desiredRoads[idx] = desired;
                offset += checkboxWidth;
            }
            curY += rowOffset;
        }

        curY += titleSkipSize;

        // Growing days
        Func<int, String> growingDaysToStr = (int growingDays) => {
            if (growingDays < 60) {
                return growingDays.ToString();
            }

            return "Year-round";
        };

        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Growing Days: ");
        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), growingDaysToStr(filterParams.minGrowingDays), true, true, true)) {
            var growingIncrements = new List<int>() { 0, 10, 20, 30, 40, 50, 60 };
            var options = new List<FloatMenuOption>();
            foreach (var growingDays in growingIncrements) {
                var label = growingDaysToStr(growingDays);
                options.Add(new FloatMenuOption(label, () => {
                    filterParams.minGrowingDays = growingDays;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Permanent Summer
        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Seasonality: ");

        Func<Seasonality, String> seasonalityToStr = (Seasonality seasonality) => {
            if (seasonality == Seasonality.Normal) {
                return "Normal";
            } else if (seasonality == Seasonality.PermSummer) {
                return "Permanent Summer";
            } else if (seasonality == Seasonality.PermWinter) {
                return "Permanent Winter";
            } else {
                return "Don't Care";
            }
        };

        if (Widgets.ButtonText(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), seasonalityToStr(filterParams.seasonality), true, true, true)) {
            var seasonalities = new List<Seasonality>() { Seasonality.Any, Seasonality.Normal, Seasonality.PermSummer, Seasonality.PermWinter };
            var options = new List<FloatMenuOption>();
            foreach (var seasonality in seasonalities) {
                options.Add(new FloatMenuOption(seasonalityToStr(seasonality), () => {
                    filterParams.seasonality = seasonality;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        curY += skipSize;

        // Min Temp
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Allowed Temp: ");
        string minTempStr = filterParams.minTemp.ToString();
        Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minTemp, ref minTempStr, -500f, 500f);

        // Max Temp
        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Max Allowed Temp: ");
        string maxTempStr = filterParams.maxTemp.ToString();
        Widgets.TextFieldNumeric(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.maxTemp, ref maxTempStr, -500f, 500f);

        curY += skipSize;

        // Min Avg Temp
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Avg Temp: ");
        string minAvgTempStr = filterParams.minAvgTemp.ToString();
        Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minAvgTemp, ref minAvgTempStr, -500f, 500f);

        // Max Avg Temp
        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Max Avg Temp: ");
        string maxAvgTempStr = filterParams.maxAvgTemp.ToString();
        Widgets.TextFieldNumeric(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.maxAvgTemp, ref maxAvgTempStr, -500f, 500f);

        curY += skipSize;

        // Min/Max Elevation
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Elevation: ");
        string minElevStr = filterParams.minElevation.ToString();
        Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minElevation, ref minElevStr, -9999f, 9999f);

        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Max Elevation: ");
        string maxElevStr = filterParams.maxElevation.ToString();
        Widgets.TextFieldNumeric(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.maxElevation, ref maxElevStr, -9999f, 9999f);

        curY += skipSize;

        // Min/Max Rainfall (mm/year)
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Rainfall: ");
        string minRainStr = filterParams.minRainfall.ToString();
        Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minRainfall, ref minRainStr, 0f, 9999f);

        Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Max Rainfall: ");
        string maxRainStr = filterParams.maxRainfall.ToString();
        Widgets.TextFieldNumeric(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.maxRainfall, ref maxRainStr, 0f, 9999f);

        curY += skipSize;

        if (filterParams.outputMode == OutputMode.Screenshot) {
            Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Min Geysers: ");
            string geysersTempStr = filterParams.minGeysers.ToString();
            Widgets.TextFieldNumeric(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minGeysers, ref geysersTempStr, 0, 10);

            Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Min Fertile Soil Tiles: ");
            string soilTempStr = filterParams.minRichSoilTiles.ToString();
            Widgets.TextFieldNumeric(new Rect(rightOffset + buttonOffset, curY, buttonSize.x, buttonSize.y), ref filterParams.minRichSoilTiles, ref soilTempStr, 0, int.MaxValue);

            curY += skipSize;
        }
        curY += 10f;

        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Stone Types:");
        curY += skipSize;

        for (int i = 0; i < 2; i++) {
            int ci = i;
            string slotLabel = filterParams.stoneSlots[ci] != null
                ? GenText.CapitalizeAsTitle(filterParams.stoneSlots[ci].label) : "Any";
            float slotX = buttonOffset + ci * 155f;
            if (Widgets.ButtonText(new Rect(slotX, curY, 110f, buttonSize.y), slotLabel)) {
                var options = new List<FloatMenuOption>();
                options.Add(new FloatMenuOption("Any", () => { filterParams.stoneSlots[ci] = null; }));
                foreach (var stoneDef in SeedFinderController.Instance.allStones) {
                    var sd = stoneDef;
                    bool usedElsewhere = false;
                    for (int j = 0; j < 2; j++) {
                        if (j != ci && filterParams.stoneSlots[j] == sd) { usedElsewhere = true; break; }
                    }
                    if (usedElsewhere) continue;
                    options.Add(new FloatMenuOption(GenText.CapitalizeAsTitle(sd.label), () => { filterParams.stoneSlots[ci] = sd; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (i == 0)
                Widgets.Label(new Rect(slotX + 113f, curY + 5f, 40f, labelSize), "and");
        }

        float thirdCheckX = buttonOffset + 2 * 155f;
        Widgets.CheckboxLabeled(new Rect(thirdCheckX, curY + 3.5f, 55f, labelSize - 3), "and",
            ref filterParams.stoneThirdEnabled, disabled: false, null, null, placeCheckboxNearText: true);
        if (filterParams.stoneThirdEnabled) {
            string slot2Label = filterParams.stoneSlots[2] != null
                ? GenText.CapitalizeAsTitle(filterParams.stoneSlots[2].label) : "Any";
            if (Widgets.ButtonText(new Rect(thirdCheckX + 55f, curY, 110f, buttonSize.y), slot2Label)) {
                var options = new List<FloatMenuOption>();
                options.Add(new FloatMenuOption("Any", () => { filterParams.stoneSlots[2] = null; }));
                foreach (var stoneDef in SeedFinderController.Instance.allStones) {
                    var sd = stoneDef;
                    bool usedElsewhere = filterParams.stoneSlots[0] == sd || filterParams.stoneSlots[1] == sd;
                    if (usedElsewhere) continue;
                    options.Add(new FloatMenuOption(GenText.CapitalizeAsTitle(sd.label), () => { filterParams.stoneSlots[2] = sd; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        } else {
            filterParams.stoneSlots[2] = null;
        }

        curY += skipSize;
        curY += 10f;

        // Faction filters
        Widgets.Label(new Rect(0, curY, 350, labelSize), "Require Nearby Settlements (within drop pod range):");
        curY += 25f;

        Widgets.CheckboxLabeled(new Rect(30, curY, 150, labelSize - 3), "Civil Outlander", ref filterParams.needCivilOutlanderNear, disabled: false, null, null, placeCheckboxNearText: true);
        Widgets.CheckboxLabeled(new Rect(180, curY, 150, labelSize - 3), "Rough Outlander", ref filterParams.needRoughOutlanderNear, disabled: false, null, null, placeCheckboxNearText: true);

        Widgets.CheckboxLabeled(new Rect(330, curY, 120, labelSize - 3), "Gentle Tribe", ref filterParams.needCivilTribeNear, disabled: false, null, null, placeCheckboxNearText: true);
        Widgets.CheckboxLabeled(new Rect(450, curY, 120, labelSize - 3), "Fierce Tribe", ref filterParams.needRoughTribeNear, disabled: false, null, null, placeCheckboxNearText: true);

        if (ModLister.RoyaltyInstalled) {
            Widgets.CheckboxLabeled(new Rect(590, curY, 120, labelSize - 3), "Empire", ref filterParams.needEmpireNear, disabled: false, null, null, placeCheckboxNearText: true);
        }

        curY += 60f;

        if (ModsConfig.BiotechActive) {
            Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Tile Pollution: ");

            if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), filterToStr(filterParams.tilePollutionFilter), true, true, true)) {
                var options = new List<FloatMenuOption>();
                foreach (var filter in featureFilters) {
                    options.Add(new FloatMenuOption(filterToStr(filter), () => {
                        filterParams.tilePollutionFilter = filter;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (filterParams.tilePollutionFilter == FeatureFilter.Present) {
                string minPolStr = filterParams.minTilePollution.ToString("F0");
                string maxPolStr = filterParams.maxTilePollution.ToString("F0");
                Widgets.Label(new Rect(rightOffset, curY, buttonOffset, labelSize), "Min%: ");
                Widgets.TextFieldNumeric(new Rect(rightOffset + 50f, curY, 80f, buttonSize.y), ref filterParams.minTilePollution, ref minPolStr, 0f, 100f);
                Widgets.Label(new Rect(rightOffset + 145f, curY, buttonOffset, labelSize), "Max%: ");
                Widgets.TextFieldNumeric(new Rect(rightOffset + 195f, curY, 80f, buttonSize.y), ref filterParams.maxTilePollution, ref maxPolStr, 0f, 100f);
            }

            curY += skipSize;
        }

        Text.Font = GameFont.Medium;

        Widgets.Label(new Rect(0, curY, inRect.width, 25f), "World Gen Parameters");
        Text.Font = GameFont.Tiny;

        Widgets.Label(new Rect(225, curY + 5f, inRect.width, 15f), "(must match parameters you will use)");

        Text.Font = origFont;

        curY += titleSkipSize;

        buttonOffset = 150f;

        // Planet Size
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Global Coverage: ");
        Func<float, string> covToStr = (float cov) => {
            if (cov == 1f)    return "100%";
            if (cov == 0.5f)  return "50%";
            if (cov == 0.3f)  return "30%";
            return "5% (dev)";
        };

        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), covToStr(filterParams.planetCoverage), true, true, true)) {
            var possibleCoverage = new List<float>() { 0.3f, 0.5f, 1.0f };
            if (Prefs.DevMode) possibleCoverage.Add(0.05f);
            var options = new List<FloatMenuOption>();

            foreach (var coverage in possibleCoverage) {
                options.Add(new FloatMenuOption(covToStr(coverage), () => {
                    filterParams.planetCoverage = coverage;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }


        curY += skipSize;

        var sliderSize = new Vector2(200f, 28f);

        // Rainfall
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Overall Rainfall: ");
        filterParams.rainfall = (OverallRainfall)Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(buttonOffset, curY, sliderSize.x, sliderSize.y), (float)filterParams.rainfall, 0f, OverallRainfallUtility.EnumValuesCount - 1, middleAlignment: true, "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(), "PlanetRainfall_High".Translate(), 1f));

        curY += skipSize;

        // Temperature
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Overall Temperature: ");
        filterParams.temperature = (OverallTemperature)Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(buttonOffset, curY, sliderSize.x, sliderSize.y), (float)filterParams.temperature, 0f, OverallTemperatureUtility.EnumValuesCount - 1, middleAlignment: true, "PlanetTemperature_Normal".Translate(), "PlanetTemperature_Low".Translate(), "PlanetTemperature_High".Translate(), 1f));

        curY += skipSize;

        // Landmark Density (Anomaly DLC only)
        if (ModsConfig.AnomalyActive) {
            Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Landmarks: ");
            filterParams.landmarkDensity = (LandmarkDensity)Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(buttonOffset, curY, sliderSize.x, sliderSize.y), (float)filterParams.landmarkDensity, 0f, LandmarkDensityUtility.EnumValuesCount - 1, middleAlignment: true, "PlanetLandmarkDensity_Normal".Translate(), "PlanetLandmarkDensity_Low".Translate(), "PlanetLandmarkDensity_High".Translate(), 1f));

            curY += skipSize;
        }

        // Population
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Population: ");
        filterParams.population = (OverallPopulation)Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(buttonOffset, curY, sliderSize.x, sliderSize.y), (float)filterParams.population, 0f, OverallPopulationUtility.EnumValuesCount - 1, middleAlignment: true, "PlanetPopulation_Normal".Translate(), "PlanetPopulation_Low".Translate(), "PlanetPopulation_High".Translate(), 1f));

        curY += skipSize;

        if (ModsConfig.BiotechActive) {
            Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Pollution: ");
            filterParams.pollution = Widgets.HorizontalSlider(new Rect(buttonOffset, curY, sliderSize.x, sliderSize.y), filterParams.pollution, 0f, 1f, middleAlignment: true, filterParams.pollution.ToStringPercent(), null, null, 0.05f);

            curY += skipSize;
        }

        // Map size
        Widgets.Label(new Rect(0, curY, buttonOffset, labelSize), "Map Size: ");
        Func<int, string> mapSizeToStr = (int size) => {
            return string.Concat(size.ToString(), " x ", size.ToString());
        };

        if (Widgets.ButtonText(new Rect(buttonOffset, curY, buttonSize.x, buttonSize.y), mapSizeToStr(filterParams.mapSize))) {
            var mapSizes = new List<int>() { 200, 225, 250, 275, 300, 325 };

            if (Prefs.TestMapSizes) {
                mapSizes.Add(350);
                mapSizes.Add(400);
            }

            var options = new List<FloatMenuOption>();

            foreach (var size in mapSizes) {
                options.Add(new FloatMenuOption(mapSizeToStr(size), () => {
                    filterParams.mapSize = size;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        curY += skipSize;


    Widgets.EndScrollView();

        var rangeErrors = new List<string>();
        if (filterParams.minTemp > filterParams.maxTemp) rangeErrors.Add("Min Temp > Max Temp");
        if (filterParams.minAvgTemp > filterParams.maxAvgTemp) rangeErrors.Add("Min Avg Temp > Max Avg Temp");
        if (filterParams.minElevation > filterParams.maxElevation) rangeErrors.Add("Min Elevation > Max Elevation");
        if (filterParams.minRainfall > filterParams.maxRainfall) rangeErrors.Add("Min Rainfall > Max Rainfall");
        if (filterParams.tilePollutionFilter == FeatureFilter.Present && filterParams.minTilePollution > filterParams.maxTilePollution) rangeErrors.Add("Min Pollution% > Max Pollution%");
        if (rangeErrors.Count > 0) {
            Color prevColor = GUI.color;
            GUI.color = Color.red;
            float warnY = inRect.height - largeButtonSize.y - 22f;
            Widgets.Label(new Rect(0f, warnY, inRect.width, 20f), "Warning: " + string.Join(", ", rangeErrors));
            GUI.color = prevColor;
        }

        if (Widgets.ButtonText(new Rect(0f, inRect.height - largeButtonSize.y, largeButtonSize.x, largeButtonSize.y), "Reset")) {
            SeedFinderController.Instance.resetFilterParams();
        }

        if (Widgets.ButtonText(new Rect(inRect.width / 2 - largeButtonSize.x / 2, inRect.height - largeButtonSize.y, largeButtonSize.x, largeButtonSize.y), "Presets")) {
            Find.WindowStack.Add(new PresetDialog(filterParams));
        }

        if (Widgets.ButtonText(new Rect(inRect.width - largeButtonSize.x, inRect.height - largeButtonSize.y, largeButtonSize.x, largeButtonSize.y), "Search")) {
            SeedFinderController.Instance.startFinding();
        }

        Text.Anchor = origAnchor;
    }
}

class SeedListResultDialog : Verse.Window {
    private class Entry {
        public string seedStr;
        public int tileID;
        public string display;
        public Entry(string seedStr, int tileID, string display) {
            this.seedStr = seedStr;
            this.tileID = tileID;
            this.display = display;
        }
    }

    private List<Entry> entries;
    private Vector2 scroll;
    private int worldsSearched;
    private double elapsedSeconds;

    private SeedListResultDialog(List<Entry> entries, int worldsSearched, double elapsedSeconds) {
        this.entries = entries;
        this.worldsSearched = worldsSearched;
        this.elapsedSeconds = elapsedSeconds;
        doCloseX = true;
        absorbInputAroundWindow = true;
        forcePause = true;
        closeOnAccept = false;
        closeOnCancel = true;
    }

    public static SeedListResultDialog Make(List<(string seedStr, int tileID, string display)> results, int worldsSearched, double elapsedSeconds) {
        var entries = results.Select(r => new Entry(r.seedStr, r.tileID, r.display)).ToList();
        return new SeedListResultDialog(entries, worldsSearched, elapsedSeconds);
    }

    public override Vector2 InitialSize => new Vector2(760f, 500f);

    public override void DoWindowContents(Rect inRect) {
        float lineH = 32f;
        float btnW = 110f;
        float gap = 6f;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), entries.Count > 0 ? "Seeds Found" : "No seeds found.");
        Text.Font = GameFont.Small;
        double secsPerWorld = elapsedSeconds > 0 && worldsSearched > 0 ? elapsedSeconds / worldsSearched : 0;
        string statsStr = string.Format("Worlds searched: {0}    |    Time: {1:F1}s    |    {2:F3} secs/world",
            worldsSearched, elapsedSeconds, secsPerWorld);

        if (entries.Count > 0 && Widgets.ButtonText(new Rect(inRect.width - 150f, 3f, 150f, 30f), "Copy to Clipboard")) {
            GUIUtility.systemCopyBuffer = string.Join("\n", entries.Select(e => e.display)) + "\n" + statsStr;
        }
        Widgets.Label(new Rect(0f, 38f, inRect.width, 22f), statsStr);
        float listY = 64f;

        Rect listRect = new Rect(0f, listY, inRect.width, inRect.height - listY);
        float totalH = entries.Count * lineH;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, totalH);
        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float copyBtnW = 65f;
        float y = 0f;
        foreach (var entry in entries) {
            Widgets.Label(new Rect(0f, y + 5f, viewRect.width - btnW - gap - copyBtnW - gap, lineH), entry.display);
            if (Widgets.ButtonText(new Rect(viewRect.width - btnW - copyBtnW - gap, y + 3f, copyBtnW, lineH - 6f), "Copy")) {
                GUIUtility.systemCopyBuffer = entry.display;
            }
            if (Widgets.ButtonText(new Rect(viewRect.width - btnW, y + 3f, btnW, lineH - 6f), "Generate")) {
                Close();
                SeedFinderController.Instance.generateTileFromList(entry.seedStr, entry.tileID);
            }
            y += lineH;
        }
        Widgets.EndScrollView();
    }
}

[HarmonyPatch(typeof (Page_SelectScenario), "DoWindowContents")]
public static class Page_SelectScenario_DoWindowContents_PostPatch {
    [HarmonyPostfix]
    public static void DrawFinderButton(Page_SelectScenario __instance, Rect rect) {
        var buttonSize = new Vector2(120f, 38f);
        if (Widgets.ButtonText(new Rect(rect.width - buttonSize.x,
                                        0, buttonSize.x, buttonSize.y), "Find Seeds")) {
            SeedFinderController.Instance.openFilterWindow();
        }
    }
}

[HarmonyPatch(typeof(LongEventHandler), "LongEventsOnGUI")]
public static class LongEventHandler_LongEventsOnGUI_Patch {
    [HarmonyPostfix]
    public static void DrawStopButton() {
        var ctrl = SeedFinderController.Instance;
        if (ctrl == null || !ctrl.IsSearching) return;
        float btnW = 160f, btnH = 40f;
        float x = (UI.screenWidth - btnW) / 2f;
        float y = UI.screenHeight * 0.68f + 4 * btnH;
        if (GUI.Button(new Rect(x, y, btnW, btnH), "Stop Search")) {
            ctrl.cancelSearch = true;
        }
        if (ctrl.IsSearchingSeedList) {
            float labelW = 200f, labelH = 56f;
            Rect labelRect = new Rect((UI.screenWidth - labelW) / 2f, y - labelH - 4f, labelW, labelH);
            GUI.Button(labelRect, string.Format("Seeds found: {0} / {1}\nWorlds checked: {2}", ctrl.NumFound, ctrl.MaxFound, ctrl.WorldsChecked));
        }
    }
}

static class PresetManager {
    private static string FilePath =>
        Path.Combine(GenFilePaths.ConfigFolderPath, "SeedFinderPresets.xml");

    private static XDocument LoadOrCreate() {
        if (File.Exists(FilePath))
            return XDocument.Load(FilePath);
        return new XDocument(new XElement("Presets"));
    }

    public static List<string> GetNames() {
        try {
            return LoadOrCreate().Root.Elements("Preset")
                .Select(e => (string)e.Attribute("name")).ToList();
        } catch { return new List<string>(); }
    }

    public static void Save(string name, SeedFinderFilterParameters fp) {
        try {
            var doc = LoadOrCreate();
            doc.Root.Elements("Preset").Where(e => (string)e.Attribute("name") == name).Remove();
            var el = new XElement("Preset", new XAttribute("name", name));
            el.Add(new XElement("Biome",                  fp.biome?.defName ?? "Any"));
            el.Add(new XElement("Hilliness",              (int)fp.hilliness));
            el.Add(new XElement("River",                  (int)fp.river));
            el.Add(new XElement("Road",                   (int)fp.road));
            el.Add(new XElement("Coastal",                (int)fp.coastal));
            el.Add(new XElement("Caves",                  (int)fp.caves));
            el.Add(new XElement("Hemisphere",             (int)fp.hemisphere));
            el.Add(new XElement("TilePollutionFilter",    (int)fp.tilePollutionFilter));
            el.Add(new XElement("MinTilePollution",       fp.minTilePollution));
            el.Add(new XElement("MaxTilePollution",       fp.maxTilePollution));
            el.Add(new XElement("MinTemp",                fp.minTemp));
            el.Add(new XElement("MaxTemp",                fp.maxTemp));
            el.Add(new XElement("MinAvgTemp",             fp.minAvgTemp));
            el.Add(new XElement("MaxAvgTemp",             fp.maxAvgTemp));
            el.Add(new XElement("MinElevation",           fp.minElevation));
            el.Add(new XElement("MaxElevation",           fp.maxElevation));
            el.Add(new XElement("MinRainfall",            fp.minRainfall));
            el.Add(new XElement("MaxRainfall",            fp.maxRainfall));
            el.Add(new XElement("MinGrowingDays",         fp.minGrowingDays));
            el.Add(new XElement("Seasonality",            (int)fp.seasonality));
            el.Add(new XElement("NeedCivilOutlanderNear", fp.needCivilOutlanderNear));
            el.Add(new XElement("NeedRoughOutlanderNear", fp.needRoughOutlanderNear));
            el.Add(new XElement("NeedCivilTribeNear",     fp.needCivilTribeNear));
            el.Add(new XElement("NeedRoughTribeNear",     fp.needRoughTribeNear));
            el.Add(new XElement("NeedEmpireNear",         fp.needEmpireNear));
            el.Add(new XElement("StoneSlot0",             fp.stoneSlots[0]?.defName ?? "Any"));
            el.Add(new XElement("StoneSlot1",             fp.stoneSlots[1]?.defName ?? "Any"));
            el.Add(new XElement("StoneSlot2",             fp.stoneSlots[2]?.defName ?? "Any"));
            el.Add(new XElement("StoneThirdEnabled",      fp.stoneThirdEnabled));
            el.Add(new XElement("DesiredRivers",          string.Join(",", fp.desiredRivers)));
            el.Add(new XElement("DesiredRoads",           string.Join(",", fp.desiredRoads)));
            el.Add(new XElement("DesiredCoastDirections", string.Join(",", fp.desiredCoastDirections)));
            el.Add(new XElement("PlanetCoverage",         fp.planetCoverage));
            el.Add(new XElement("Rainfall",               (int)fp.rainfall));
            el.Add(new XElement("Temperature",            (int)fp.temperature));
            el.Add(new XElement("Population",             (int)fp.population));
            el.Add(new XElement("LandmarkDensity",        (int)fp.landmarkDensity));
            el.Add(new XElement("Pollution",              fp.pollution));
            el.Add(new XElement("MapSize",                fp.mapSize));
            doc.Root.Add(el);
            doc.Save(FilePath);
        } catch (Exception e) { Log.Error("SeedFinder: failed to save preset: " + e); }
    }

    public static void Load(string name, SeedFinderFilterParameters fp, List<RiverDef> allRivers, List<RoadDef> allRoads) {
        try {
            var el = LoadOrCreate().Root.Elements("Preset")
                .FirstOrDefault(e => (string)e.Attribute("name") == name);
            if (el == null) return;

            string biomeStr = (string)el.Element("Biome");
            fp.biome = biomeStr == "Any" ? null : DefDatabase<BiomeDef>.GetNamedSilentFail(biomeStr);
            fp.hilliness          = (Hilliness)    int.Parse((string)el.Element("Hilliness"));
            fp.river              = (FeatureFilter) int.Parse((string)el.Element("River"));
            fp.road               = (FeatureFilter) int.Parse((string)el.Element("Road"));
            fp.coastal            = (FeatureFilter) int.Parse((string)el.Element("Coastal"));
            fp.caves              = (FeatureFilter) int.Parse((string)el.Element("Caves"));
            fp.hemisphere         = (Hemisphere)    int.Parse((string)el.Element("Hemisphere"));
            fp.tilePollutionFilter = (FeatureFilter)int.Parse((string)(el.Element("TilePollutionFilter") ?? new XElement("x", "2")));
            fp.minTilePollution   = float.Parse((string)(el.Element("MinTilePollution") ?? new XElement("x", "0")));
            fp.maxTilePollution   = float.Parse((string)(el.Element("MaxTilePollution") ?? new XElement("x", "100")));
            fp.minTemp            = int.Parse((string)el.Element("MinTemp"));
            fp.maxTemp            = int.Parse((string)el.Element("MaxTemp"));
            fp.minAvgTemp         = int.Parse((string)(el.Element("MinAvgTemp") ?? new XElement("x", "-200")));
            fp.maxAvgTemp         = int.Parse((string)(el.Element("MaxAvgTemp") ?? new XElement("x", "200")));
            fp.minElevation       = int.Parse((string)(el.Element("MinElevation") ?? new XElement("x", "-9999")));
            fp.maxElevation       = int.Parse((string)(el.Element("MaxElevation") ?? new XElement("x", "9999")));
            fp.minRainfall        = int.Parse((string)(el.Element("MinRainfall") ?? new XElement("x", "0")));
            fp.maxRainfall        = int.Parse((string)(el.Element("MaxRainfall") ?? new XElement("x", "9999")));
            fp.minGrowingDays     = int.Parse((string)el.Element("MinGrowingDays"));
            fp.seasonality        = (Seasonality)   int.Parse((string)el.Element("Seasonality"));
            fp.needCivilOutlanderNear = bool.Parse((string)el.Element("NeedCivilOutlanderNear"));
            fp.needRoughOutlanderNear = bool.Parse((string)el.Element("NeedRoughOutlanderNear"));
            fp.needCivilTribeNear     = bool.Parse((string)el.Element("NeedCivilTribeNear"));
            fp.needRoughTribeNear     = bool.Parse((string)el.Element("NeedRoughTribeNear"));
            fp.needEmpireNear         = bool.Parse((string)(el.Element("NeedEmpireNear") ?? new XElement("x", "False")));

            fp.stoneSlots = new ThingDef[3];
            foreach (var (tag, idx) in new[] { ("StoneSlot0", 0), ("StoneSlot1", 1), ("StoneSlot2", 2) }) {
                string s = (string)(el.Element(tag) ?? new XElement("x", "Any"));
                fp.stoneSlots[idx] = s == "Any" ? null : DefDatabase<ThingDef>.GetNamedSilentFail(s);
            }
            fp.stoneThirdEnabled = bool.Parse((string)(el.Element("StoneThirdEnabled") ?? new XElement("x", "False")));

            var rivers = ((string)el.Element("DesiredRivers"))?.Split(',');
            if (rivers != null && rivers.Length == allRivers.Count)
                for (int i = 0; i < rivers.Length; i++) fp.desiredRivers[i] = bool.Parse(rivers[i]);

            var roads = ((string)el.Element("DesiredRoads"))?.Split(',');
            if (roads != null && roads.Length == allRoads.Count)
                for (int i = 0; i < roads.Length; i++) fp.desiredRoads[i] = bool.Parse(roads[i]);

            var coasts = ((string)el.Element("DesiredCoastDirections"))?.Split(',');
            if (coasts != null && coasts.Length == 4)
                for (int i = 0; i < 4; i++) fp.desiredCoastDirections[i] = bool.Parse(coasts[i]);

            fp.planetCoverage  = float.Parse((string)el.Element("PlanetCoverage"));
            fp.rainfall        = (OverallRainfall)   int.Parse((string)el.Element("Rainfall"));
            fp.temperature     = (OverallTemperature)int.Parse((string)el.Element("Temperature"));
            fp.population      = (OverallPopulation) int.Parse((string)el.Element("Population"));
            fp.landmarkDensity = (LandmarkDensity)   int.Parse((string)(el.Element("LandmarkDensity") ?? new XElement("x", "2")));
            fp.pollution       = float.Parse((string)(el.Element("Pollution") ?? new XElement("x", "0")));
            fp.mapSize         = int.Parse((string)(el.Element("MapSize") ?? new XElement("x", "250")));

            if (!ModsConfig.BiotechActive) {
                fp.tilePollutionFilter = FeatureFilter.Either;
                fp.minTilePollution = 0f;
                fp.maxTilePollution = 100f;
                fp.pollution = 0f;
            }
            if (!ModLister.RoyaltyInstalled) {
                fp.needEmpireNear = false;
            }
            if (!ModsConfig.AnomalyActive) {
                fp.landmarkDensity = LandmarkDensity.Normal;
            }
        } catch (Exception e) { Log.Error("SeedFinder: failed to load preset: " + e); }
    }

    public static void Delete(string name) {
        try {
            var doc = LoadOrCreate();
            doc.Root.Elements("Preset").Where(e => (string)e.Attribute("name") == name).Remove();
            doc.Save(FilePath);
        } catch (Exception e) { Log.Error("SeedFinder: failed to delete preset: " + e); }
    }
}

class PresetDialog : Verse.Window {
    private SeedFinderFilterParameters filterParams;
    private string newPresetName = "";
    private Vector2 scroll;

    public PresetDialog(SeedFinderFilterParameters fp) {
        filterParams = fp;
        doCloseX = true;
        absorbInputAroundWindow = false;
        forcePause = false;
        closeOnAccept = false;
        closeOnCancel = true;
    }

    public override Vector2 InitialSize => new Vector2(500f, 420f);

    public override void DoWindowContents(Rect inRect) {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Presets");
        Text.Font = GameFont.Small;

        float curY = 35f;
        Widgets.Label(new Rect(0f, curY + 4f, 50f, 28f), "Name:");
        newPresetName = Widgets.TextField(new Rect(55f, curY, 280f, 28f), newPresetName);
        if (Widgets.ButtonText(new Rect(340f, curY, 80f, 28f), "Save") && !newPresetName.NullOrEmpty()) {
            string safeName = new string(newPresetName.Where(c => c != '<' && c != '>' && c != '&' && c != '"' && c != '\'').ToArray()).Trim();
            if (!safeName.NullOrEmpty()) {
                PresetManager.Save(safeName, filterParams);
                newPresetName = "";
            }
        }
        curY += 38f;

        Widgets.DrawLineHorizontal(0f, curY, inRect.width);
        curY += 6f;

        var names = PresetManager.GetNames();
        if (names.Count == 0) {
            Widgets.Label(new Rect(0f, curY, inRect.width, 28f), "No saved presets.");
            return;
        }

        float totalH = names.Count * 34f;
        Rect listRect = new Rect(0f, curY, inRect.width, inRect.height - curY);
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(totalH, listRect.height));
        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach (var name in names.ToList()) {
            Widgets.Label(new Rect(0f, y + 5f, viewRect.width - 180f, 28f), name);
            if (Widgets.ButtonText(new Rect(viewRect.width - 175f, y + 2f, 80f, 28f), "Load")) {
                PresetManager.Load(name, filterParams,
                    SeedFinderController.Instance.allRivers,
                    SeedFinderController.Instance.allRoads);
                Close();
            }
            if (Widgets.ButtonText(new Rect(viewRect.width - 90f, y + 2f, 85f, 28f), "Delete")) {
                PresetManager.Delete(name);
            }
            y += 34f;
        }
        Widgets.EndScrollView();
    }
}

/// <summary>
/// The hub of the mod. Instantiated by HugsLib.
/// </summary>
public class SeedFinderController : ModBase {

    public static SeedFinderController Instance { get; private set; }

    private volatile int curSeedOffset;
    private volatile int numFound;
    private Stack<int> validTiles;
    private bool isSeedFinding;
    internal volatile bool cancelSearch;
    internal bool IsSearchingSeedList => isSeedFinding && filterParams.outputMode == OutputMode.SeedText;
    internal bool IsSearching => isSeedFinding;
    private List<(string seedStr, int tileID, string display)> activeResults;
    internal int NumFound => activeResults?.Count ?? numFound;
    internal int MaxFound => filterParams.maxFound;
    internal int WorldsChecked => curSeedOffset;
    private bool needCapture;
    private bool captureFinished;
    private SeedFinderFilterParameters filterParams;
    private Vector2 origAnimaSize;
    private float animaRadius;
    public List<ThingDef> allStones { get; private set; }
    public List<RiverDef> allRivers { get; private set; }
    public List<RoadDef> allRoads { get; private set; }

    public override string ModIdentifier {
        get { return "SeedFinder"; }
    }

    internal new ModLogger Logger {
        get { return base.Logger; }
    }

    private void reset() {
        curSeedOffset = 0;
        numFound = 0;
        validTiles = new Stack<int>();
        isSeedFinding = false;
        cancelSearch = false;
        needCapture = false;
        captureFinished = false;
    }

    private SeedFinderController() {
        Instance = this;
        filterParams = new SeedFinderFilterParameters();
        origAnimaSize = new Vector2(0, 0);
        animaRadius = -1f;
        reset();
    }

    private static bool IsStone(ThingDef thingDef) {
        return thingDef.category == ThingCategory.Building &&
            thingDef.building.isNaturalRock &&
            !thingDef.building.isResourceRock;
    }

    internal void openFilterWindow() {
        Find.WindowStack.Add(new FilterWindow(filterParams));
    }

    internal void startFinding() {
        isSeedFinding = true;

        if (ModLister.RoyaltyInstalled) {
            ThingDefOf.Plant_TreeAnima.graphicData.drawSize *= 2.5f;
        }

        visitNextMap();
    }

    internal void stopFinding() {
        reset();

        if (ModLister.RoyaltyInstalled) {
            ThingDefOf.Plant_TreeAnima.graphicData.drawSize = origAnimaSize;
        }
    }

    internal void resetFilterParams() {
        filterParams.outDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RimworldSeedFinder");
        filterParams.baseSeed = GenText.RandomSeedString();
        filterParams.maxFound = 100;
        filterParams.outputMode = OutputMode.SeedText;
        filterParams.searchMultipleSeeds = false;
        filterParams.clearFog = false;
        filterParams.highlightPOI = true;
        filterParams.planetCoverage = 0.3f;
        filterParams.rainfall = OverallRainfall.Normal;
        filterParams.temperature = OverallTemperature.Normal;
        filterParams.landmarkDensity = LandmarkDensity.Normal;
        filterParams.population = OverallPopulation.Normal;
        filterParams.pollution = (ModsConfig.BiotechActive ? 0.05f : 0f);
        filterParams.mapSize = 250;
        filterParams.biome = null;
        filterParams.hilliness = Hilliness.Undefined;
        filterParams.river = FeatureFilter.Either;
        filterParams.coastal = FeatureFilter.Either;
        filterParams.caves = FeatureFilter.Either;
        filterParams.hemisphere = Hemisphere.Either;
        filterParams.tilePollutionFilter = FeatureFilter.Either;
        filterParams.minTilePollution = 0f;
        filterParams.maxTilePollution = 100f;
        filterParams.maxTemp = 200;
        filterParams.minTemp = -200;
        filterParams.maxAvgTemp = 200;
        filterParams.minAvgTemp = -200;
        filterParams.minElevation = -9999;
        filterParams.maxElevation = 9999;
        filterParams.minRainfall = 0;
        filterParams.maxRainfall = 9999;
        filterParams.minGrowingDays = 0;
        filterParams.seasonality = Seasonality.Any;
        filterParams.minGeysers = 0;
        filterParams.minRichSoilTiles = 0;
        filterParams.needCivilOutlanderNear = false;
        filterParams.needRoughOutlanderNear = false;
        filterParams.needCivilTribeNear = false;
        filterParams.needRoughTribeNear = false;
        filterParams.needEmpireNear = false;

        filterParams.factions.Clear();
        foreach (FactionDef faction in FactionGenerator.ConfigurableFactions) {
            filterParams.factions.Add(faction);
        }

        filterParams.road = FeatureFilter.Either;
        for (int i = 0; i < filterParams.desiredRivers.Count; i++) filterParams.desiredRivers[i] = true;
        for (int i = 0; i < filterParams.desiredRoads.Count; i++) filterParams.desiredRoads[i] = true;
        filterParams.stoneSlots = new ThingDef[3];
        filterParams.stoneThirdEnabled = false;
        filterParams.desiredCoastDirections = new List<bool> { true, true, true, true };
        filterParams.windowScroll = new Vector2(0, 0);
    }

    public override void Initialize() {
        filterParams.outDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RimworldSeedFinder");
        filterParams.baseSeed = GenText.RandomSeedString();
        filterParams.maxFound = 100;
        filterParams.outputMode = OutputMode.SeedText;
        filterParams.searchMultipleSeeds = false;
        filterParams.desiredCoastDirections = new List<bool> { true, true, true, true };
        filterParams.clearFog = false;
        filterParams.highlightPOI = true;
        filterParams.planetCoverage = 0.3f;
        filterParams.rainfall = OverallRainfall.Normal;
        filterParams.temperature = OverallTemperature.Normal;
        filterParams.landmarkDensity = LandmarkDensity.Normal;
        filterParams.population = OverallPopulation.Normal;
        filterParams.pollution = (ModsConfig.BiotechActive ? 0.05f : 0f);
        filterParams.mapSize = 250;

        filterParams.factions = new List<FactionDef>();
        foreach (FactionDef faction in FactionGenerator.ConfigurableFactions) {
            filterParams.factions.Add(faction);
        }

        filterParams.biome = null;
        filterParams.hilliness = Hilliness.Undefined;

        filterParams.river = FeatureFilter.Either;
        filterParams.coastal = FeatureFilter.Either;
        filterParams.caves = FeatureFilter.Either;
        filterParams.hemisphere = Hemisphere.Either;
        filterParams.tilePollutionFilter = FeatureFilter.Either;
        filterParams.minTilePollution = 0f;
        filterParams.maxTilePollution = 100f;

        filterParams.maxTemp = 200;
        filterParams.minTemp = -200;
        filterParams.maxAvgTemp = 200;
        filterParams.minAvgTemp = -200;
        filterParams.minElevation = -9999;
        filterParams.maxElevation = 9999;
        filterParams.minRainfall = 0;
        filterParams.maxRainfall = 9999;
        filterParams.minGrowingDays = 0;
        filterParams.seasonality = Seasonality.Any;
        filterParams.minGeysers = 0;
        filterParams.minRichSoilTiles = 0;

        filterParams.needCivilOutlanderNear = false;
        filterParams.needRoughOutlanderNear = false;
        filterParams.needCivilTribeNear = false;
        filterParams.needRoughTribeNear = false;
        filterParams.needEmpireNear = false;

        allStones = DefDatabase<ThingDef>.AllDefs.Where(SeedFinderController.IsStone).ToList();
        allRivers = DefDatabase<RiverDef>.AllDefsListForReading;
        allRoads = DefDatabase<RoadDef>.AllDefsListForReading.Where(r => r.priority > 0).ToList();

        filterParams.desiredRivers = new List<bool>();
        foreach (var riverDef in allRivers) {
            filterParams.desiredRivers.Add(true);
        }

        filterParams.road = FeatureFilter.Either;
        filterParams.desiredRoads = new List<bool>();
        foreach (var roadDef in allRoads) {
            filterParams.desiredRoads.Add(true);
        }

        filterParams.stoneSlots = new ThingDef[3];
        filterParams.stoneThirdEnabled = false;

        filterParams.windowScroll = new Vector2(0, 0);

        if (ModLister.RoyaltyInstalled) {
            origAnimaSize = ThingDefOf.Plant_TreeAnima.graphicData.drawSize;

            foreach (var animaComp in ThingDefOf.Plant_TreeAnima.comps) {
                var meditationComp = animaComp as CompProperties_MeditationFocus;
                if (meditationComp != null) {
                    foreach (var offset in meditationComp.offsets) {
                        var radiusOffset = offset as FocusStrengthOffset_ArtificialBuildings;
                        if (radiusOffset != null) {
                            animaRadius = radiusOffset.radius;
                            break;
                        }
                    }
                }

                if (animaRadius != -1f) {
                    break;
                }
            }
        }
    }

    internal float FertilityAt(Map map, IntVec3 loc) {
        float topFertility = map.terrainGrid.TerrainAt(loc).fertility;
        float bottomFertility = 0f;
        if (map.terrainGrid.CanRemoveTopLayerAt(loc)) {
            bottomFertility = map.terrainGrid.UnderTerrainAt(loc).fertility;
        }

        return Mathf.Max(topFertility, bottomFertility);
    }

    public override void MapLoaded(Map map) {
        if (!isSeedFinding) {
            return;
        }

        Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(120f);
        
        foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction) {
            pawn.inventory.DestroyAll();
            if (pawn.Spawned) {
                pawn.DeSpawn();
            }

            if (pawn.holdingOwner != null) {
                pawn.holdingOwner.Remove(pawn);
            }

            if (!pawn.IsWorldPawn()) {
                Find.WorldPawns.PassToWorld(pawn);
            }
        }

        foreach (var pawn in Find.WorldPawns.AllPawnsAliveOrDead.ToList()) {
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
        }

        // Get rid of error spam from triggers trying to run after the map has been destroyed
        foreach (var thing in map.listerThings.AllThings.ToList()) {
            if (thing.def == ThingDefOf.InsectJelly ||
                thing.def == ThingDefOf.RectTrigger ||
                thing.def == ThingDefOf.AncientCryptosleepCasket ||
                thing.Faction == Faction.OfMechanoids ||
                thing.Faction == Faction.OfInsects ||
                thing.Faction == Faction.OfAncients ||
                thing.Faction == Faction.OfAncientsHostile ||
                thing.Faction == Faction.OfHoraxCult) {
                if (thing.holdingOwner != null) {
                    thing.holdingOwner.Remove(thing);
                }
                thing.DeSpawn();
                thing.Destroy();
            }
        }

        int numGeysers = 0;
        foreach (var geyser in map.listerBuildings.AllBuildingsNonColonistOfDef(ThingDefOf.SteamGeyser)) {
            numGeysers++;
        }

        bool mapFilterPassed = true;

        if (numGeysers < filterParams.minGeysers) {
            mapFilterPassed = false;
        }

        if (filterParams.minRichSoilTiles != 0 && mapFilterPassed) {
            int numRichSoilTiles = 0;

            for (int z = 0; z < map.Size.z; z++) {
                for (int x = 0; x < map.Size.x; x++) {
                    var loc = new IntVec3(x, 0, z);

                    if (FertilityAt(map, loc) >= 1.1f) {
                        numRichSoilTiles++;
                    }
                }
            }

            if (numRichSoilTiles < filterParams.minRichSoilTiles) {
                mapFilterPassed = false;
            }
        }

        if (mapFilterPassed) {
            needCapture = true;

            if (filterParams.outputMode == OutputMode.Screenshot) {
            if (filterParams.clearFog) {
                map.fogGrid.ClearAllFog();
            } else {
			    FloodFillerFog.FloodUnfog(MapGenerator.PlayerStartSpot, map);

                List<IntVec3> rootsToUnfog = MapGenerator.rootsToUnfog;
		        for (int i = 0; i < rootsToUnfog.Count; i++)
		        {
		        	FloodFillerFog.FloodUnfog(rootsToUnfog[i], map);
		        }
            }

            float longitude = Find.WorldGrid.LongLatOf(map.Tile).x;
            long absTicks = Find.TickManager.TicksAbs;

            int dayTicks = GenDate.DayTick(absTicks, longitude);
            // Slight offset from noon to preserve shadows
            int advanceTicks = 25000;
            if (dayTicks > advanceTicks) {
                advanceTicks += 60000 - dayTicks;
            } else {
                advanceTicks -= dayTicks;
            }

            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + advanceTicks);

            // Uniform weather
            map.weatherManager.curWeather = WeatherDefOf.Clear;
            } // end outputMode == Screenshot
        } else {
            captureFinished = true;
        }
    }

    public override void Tick(int tick) {
        if (!isSeedFinding) return;

        if (needCapture) {
            needCapture = false;

            int curTile = Find.CurrentMap.Tile;

            Vector2 longlat = Find.WorldGrid.LongLatOf(curTile);

            string latitudePostfix = longlat.y >= 0f ? "N" : "S";
            string longitudePostfix = longlat.x >= 0f ? "E" : "W";

            string seedStr = currentSeedString();

            if (filterParams.outputMode == OutputMode.Screenshot) {
                string path = Path.Combine(filterParams.outDirectory,
                                           string.Concat(seedStr, "_",
                                                         Math.Abs(longlat.y).ToString("F2"), latitudePostfix,
                                                         "_", Math.Abs(longlat.x).ToString("F2"), longitudePostfix, ".png"));
                Find.CameraDriver.StartCoroutine(RenderAndSave(Find.CurrentMap, path));
            } else {
                string line = string.Concat(seedStr, ",", Find.CurrentMap.Tile.ToString(), ",",
                                            Math.Abs(longlat.y).ToString("F2"), latitudePostfix,
                                            ",", Math.Abs(longlat.x).ToString("F2"), longitudePostfix);
                string textPath = Path.Combine(filterParams.outDirectory, "seeds.txt");
                var fileInfo = new FileInfo(textPath);
                fileInfo.Directory.Create();
                File.AppendAllText(fileInfo.FullName, line + Environment.NewLine);
                captureFinished = true;
            }

            numFound++;
        }

        if (captureFinished) {
            captureFinished = false;

            if (numFound < filterParams.maxFound) {
                visitNextMap();
            } else {
                stopFinding();
                GenScene.GoToMainMenu();
            }
        }
    }

    // This function is based on code from the Progress-Render mod, authored by Lanilor
    // LGPL 3 License
    private IEnumerator RenderAndSave(Map map, string path) {
        yield return new WaitForFixedUpdate();

        if (filterParams.highlightPOI) {
            if (ModLister.RoyaltyInstalled) {
                foreach (var animaThing in Find.CurrentMap.listerThings.ThingsOfDef(ThingDefOf.Plant_TreeAnima)) {
                    GenDraw.DrawRadiusRing(animaThing.Position, animaRadius, MeditationUtility.ArtificialBuildingRingColor);
                }
            }

            foreach (var geyser in map.listerBuildings.AllBuildingsNonColonistOfDef(ThingDefOf.SteamGeyser)) {
                GenDraw.DrawRadiusRing(geyser.Position, 4f, new Color(0.8f, 0.1f, 0.6f), (IntVec3 v) => {
                    float geyserDistX = geyser.Position.x + 0.5f - (float)v.x;
                    float geyserDistZ = geyser.Position.z + 0.5f - (float)v.z;

                    float geyserDist = Mathf.Sqrt(geyserDistX * geyserDistX + geyserDistZ * geyserDistZ);

                    return geyserDist <= 3.5f;
                });
            }

            var fertilityDrawer = new CellBoolDrawer((int idx) => {
                var loc = CellIndicesUtility.IndexToCell(idx, map.Size.x);
                if (loc.Filled(map) || loc.Fogged(map)) {
                    return false;
                }

                return FertilityAt(map, loc) >= 1.1f;
            }, () => {
                return Color.white;
            }, (int idx) => {
                return Color.green;
            }, map.Size.x, map.Size.z, 3610);

            fertilityDrawer.MarkForDraw();
            fertilityDrawer.CellBoolDrawerUpdate();
        }

        CameraJumper.TryHideWorld();
        float startX = 0;
        float startZ = 0;
        float endX = map.Size.x;
        float endZ = map.Size.z;

        float distX = endX - startX;
        float distZ = endZ - startZ;

        float pixelsPerCell = 8f;
        int imageWidth = (int)(distX * pixelsPerCell);
        int imageHeight = (int)(distZ * pixelsPerCell);

        int RenderTextureSize = 4096;

        int renderCountX = (int)Math.Ceiling((float)imageWidth / RenderTextureSize);
        int renderCountZ = (int)Math.Ceiling((float)imageHeight / RenderTextureSize);
        int renderWidth = (int)Math.Ceiling((float)imageWidth / renderCountX);
        int renderHeight = (int)Math.Ceiling((float)imageHeight / renderCountZ);

        float cameraPosX = (float)distX / 2 / renderCountX;
        float cameraPosZ = (float)distZ / 2 / renderCountZ;
        float orthographicSize = Math.Min(cameraPosX, cameraPosZ);
        orthographicSize = cameraPosZ;
        Vector3 cameraBasePos = new Vector3(cameraPosX, 15f + (orthographicSize - 11f) / 49f * 50f, cameraPosZ);

        RenderTexture renderTexture = RenderTexture.GetTemporary(renderWidth, renderHeight, 24);
        Texture2D imageTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        Camera camera = Find.Camera;
        CameraDriver camDriver = camera.GetComponent<CameraDriver>();
        camDriver.enabled = false;

        // Store current camera data
        Vector3 rememberedRootPos = map.rememberedCameraPos.rootPos;
        float rememberedRootSize = map.rememberedCameraPos.rootSize;
        float rememberedFarClipPlane = camera.farClipPlane;

        // Overwrite current view rect in the camera driver
        CellRect camViewRect = camDriver.CurrentViewRect;
        int camRectMinX = Math.Min((int)startX, camViewRect.minX);
        int camRectMinZ = Math.Min((int)startZ, camViewRect.minZ);
        int camRectMaxX = Math.Max((int)Math.Ceiling(endX), camViewRect.maxX);
        int camRectMaxZ = Math.Max((int)Math.Ceiling(endZ), camViewRect.maxZ);
        Traverse camDriverTraverse = Traverse.Create(camDriver);
        camDriverTraverse.Field("lastViewRect").SetValue(CellRect.FromLimits(camRectMinX, camRectMinZ, camRectMaxX, camRectMaxZ));
        camDriverTraverse.Field("lastViewRectGetFrame").SetValue(Time.frameCount);
        yield return new WaitForEndOfFrame();

        // Set camera values needed for rendering
        camera.orthographicSize = orthographicSize;
        camera.farClipPlane = cameraBasePos.y + 6.5f;

        camera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;

        for (int i = 0; i < renderCountZ; i++)
        {
            for (int j = 0; j < renderCountX; j++)
            {
                camera.transform.position = new Vector3(startX + cameraBasePos.x * (2 * j + 1), cameraBasePos.y, startZ + cameraBasePos.z * (2 * i + 1));
                camera.Render();
                imageTexture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), renderWidth * j, renderHeight * i, false);
            }
        }

        // Restore camera and viewport
        RenderTexture.active = null;
        camera.targetTexture = null;
        camera.farClipPlane = rememberedFarClipPlane;
        camDriver.SetRootPosAndSize(rememberedRootPos, rememberedRootSize);
        camDriver.enabled = true;

        RenderTexture.ReleaseTemporary(renderTexture);

        byte[] png = imageTexture.EncodeToPNG();

        var fileInfo = new FileInfo(path);
        fileInfo.Directory.Create();
        File.WriteAllBytes(fileInfo.FullName, png);

        UnityEngine.Object.Destroy(imageTexture);

        captureFinished = true;
        yield break;
    }

    internal void resetGame() {
        MemoryUtility.ClearAllMapsAndWorld();
        Current.Game = null;

        Current.Game = new Game();
        Current.Game.InitData = new GameInitData();

        // Make custom scenario that doesn't spawn any items
        var scen = new Scenario();
        scen.Category = ScenarioCategory.CustomLocal;
        scen.name = "SeedFinderScenario";
        scen.description = null;
        scen.summary = null;

        var scenFaction = (ScenPart_PlayerFaction)ScenarioMaker.MakeScenPart(ScenPartDefOf.PlayerFaction);
        typeof(ScenPart_PlayerFaction).GetField("factionDef", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scenFaction, FactionDefOf.PlayerColony);
        typeof(Scenario).GetField("playerFaction",
            BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scen, scenFaction);

        var pawnConfig = (ScenPart_ConfigPage_ConfigureStartingPawns)ScenarioMaker.MakeScenPart(
            ScenPartDefOf.ConfigPage_ConfigureStartingPawns);
        pawnConfig.pawnCount = 1;
        pawnConfig.pawnChoiceCount = 8;

        var scenParts = new List<ScenPart>();
        typeof(Scenario).GetField("parts", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scen, scenParts);

        scenParts.Add(pawnConfig);
        scenParts.Add(ScenarioMaker.MakeScenPart(ScenPartDefOf.PlayerPawnsArriveMethod));

		    var surfaceLayer = new ScenPart_PlanetLayer {
		    	def = ScenPartDefOf.PlanetLayerFixed,
		    	layer = PlanetLayerDefOf.Surface,
		    	settingsDef = PlanetLayerSettingsDefOf.Surface,
		    	hide = true,
		    	tag = "Surface"
		    };

        typeof(Scenario).GetField("surfaceLayer",
          BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scen, surfaceLayer);
        
        if (ModsConfig.OdysseyActive) {
        	ScenPart_PlanetLayer scenPart_PlanetLayer = new ScenPart_PlanetLayer {
        		def = ScenPartDefOf.PlanetLayerFixed,
        		layer = PlanetLayerDefOf.Orbit,
        		settingsDef = PlanetLayerSettingsDefOf.Orbit,
        		hide = true,
        		tag = "Orbit"
        	};
        	scenParts.Add(scenPart_PlanetLayer);
        	surfaceLayer.connections.Add(new LayerConnection {
        		tag = scenPart_PlanetLayer.tag,
        		zoomMode = LayerConnection.ZoomMode.ZoomOut
        	});
        	scenPart_PlanetLayer.connections.Add(new LayerConnection {
        		tag = surfaceLayer.tag,
        		zoomMode = LayerConnection.ZoomMode.ZoomIn
        	});
        }

        Current.Game.Scenario = scen;

        Find.Scenario.PreConfigure();
        Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
    }

    private void visitNextMap() {
        LongEventHandler.ClearQueuedEvents();
        LongEventHandler.QueueLongEvent(delegate {
            if (validTiles.Count == 0) {
                resetGame();
                if (filterParams.outputMode == OutputMode.SeedText) {
                    LongEventHandler.QueueLongEvent(delegate {
                        activeResults = new List<(string seedStr, int tileID, string display)>();
                        var stopwatch = Stopwatch.StartNew();
                        do {
                            curSeedOffset++;
                            generateWorld();
                            filterTiles();
                            string seedStr = currentSeedString();
                            var worldGrid = Current.Game.World.grid;
                            while (validTiles.Count > 0 && activeResults.Count < filterParams.maxFound) {
                                int tileID = validTiles.Pop();
                                Vector2 longlat = worldGrid.LongLatOf(tileID);
                                string latPostfix = longlat.y >= 0f ? "N" : "S";
                                string lonPostfix = longlat.x >= 0f ? "E" : "W";
                                string display = string.Concat(seedStr, "  |  Tile ", tileID,
                                                               "  |  ", Math.Abs(longlat.y).ToString("F2"), latPostfix,
                                                               ", ", Math.Abs(longlat.x).ToString("F2"), lonPostfix);
                                activeResults.Add((seedStr, tileID, display));
                            }
                            if (cancelSearch) break;
                        } while (filterParams.searchMultipleSeeds && activeResults.Count < filterParams.maxFound);
                        stopwatch.Stop();
                        int worldsSearched = curSeedOffset;
                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        var results = activeResults;
                        activeResults = null;
                        LongEventHandler.ExecuteWhenFinished(() => {
                            stopFinding();
                            Find.WindowStack.Add(SeedListResultDialog.Make(results, worldsSearched, elapsedSeconds));
                        });
                    }, "SeedFinder.FindingSeeds", doAsynchronously: true, null);
                } else {
                    LongEventHandler.QueueLongEvent(delegate {
                        while (validTiles.Count == 0 && !cancelSearch) {
                            curSeedOffset++;
                            generateWorld();
                            filterTiles();
                        }
                        if (cancelSearch) {
                            LongEventHandler.ExecuteWhenFinished(() => {
                                stopFinding();
                                GenScene.GoToMainMenu();
                            });
                            return;
                        }
                        int curTile = validTiles.Pop();
                        Find.GameInitData.startingTile = curTile;
                        Find.GameInitData.mapSize = filterParams.mapSize;
                        Find.Scenario.PostIdeoChosen();
                        Find.GameInitData.PrepForMapGen();
                        Find.Scenario.PreMapGenerate();
                    }, "Play", "SeedFinder.FindingSeeds", doAsynchronously: true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
                }
            } else {
                int oldTile = Find.CurrentMap.Tile;
                int curTile = validTiles.Pop();

                LongEventHandler.QueueLongEvent(delegate {
                    var world = Current.Game.World;
                    Current.Game.World = null;
                    MemoryUtility.ClearAllMapsAndWorld();
                    Current.Game.World = world;

                    foreach (WorldObject item in Find.WorldObjects.ObjectsAt(oldTile).ToList()) {
                        item.Destroy();
                    }

                    MemoryUtility.UnloadUnusedUnityAssets();

                    Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                    settlement.SetFaction(Faction.OfPlayer);
                    settlement.Tile = curTile;
                    settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement, Faction.OfPlayer.def.playerInitialSettlementNameMaker);
                    Find.WorldObjects.Add(settlement);
                    var map = GetOrGenerateMapUtility.GetOrGenerateMap(curTile, WorldObjectDefOf.Settlement);
                    Current.Game.CurrentMap = map;

                    CameraJumper.TryJump(MapGenerator.PlayerStartSpot, settlement.Map);
                }, "GeneratingMap", doAsynchronously: true, null);
            }
        }, "Finding Seeds", doAsynchronously: true, null, showExtraUIInfo: false);
    }

    private string currentSeedString() {
        if (filterParams.searchMultipleSeeds && int.TryParse(filterParams.baseSeed, out int baseNum))
            return (baseNum + curSeedOffset - 1).ToString();
        return filterParams.baseSeed + (curSeedOffset > 1 ? curSeedOffset.ToString() : "");
    }

    private void generateWorld() {
        Find.GameInitData.ResetWorldRelatedMapInitData();
        string seedString = currentSeedString();

        Current.Game.World = WorldGenerator.GenerateWorld(filterParams.planetCoverage, seedString,
                                                          filterParams.rainfall,
                                                          filterParams.temperature,
                                                          filterParams.population,
                                                          filterParams.landmarkDensity,
                                                          filterParams.factions,
                                                          filterParams.pollution);
    }

    internal void generateTileFromList(string seedStr, int tileID) {
        LongEventHandler.QueueLongEvent(delegate {
            resetGame();
            Find.GameInitData.ResetWorldRelatedMapInitData();
            Current.Game.World = WorldGenerator.GenerateWorld(filterParams.planetCoverage, seedStr,
                                                              filterParams.rainfall,
                                                              filterParams.temperature,
                                                              filterParams.population,
                                                              filterParams.landmarkDensity,
                                                              filterParams.factions,
                                                              filterParams.pollution);
            Find.GameInitData.startingTile = tileID;
            Find.GameInitData.mapSize = filterParams.mapSize;
            Find.Scenario.PostIdeoChosen();
            Find.GameInitData.PrepForMapGen();
            Find.Scenario.PreMapGenerate();
        }, "Play", "GeneratingMap", doAsynchronously: true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
    }

    private void filterTiles() {
        var allSettlements = Find.WorldObjects.Settlements;

        var outlanderSettlements = new List<Settlement>();
        var roughOutlanderSettlements = new List<Settlement>();

        var tribeSettlements = new List<Settlement>();
        var roughTribeSettlements = new List<Settlement>();

        var empireSettlements = new List<Settlement>();

        {
            var civilOutlander = Find.FactionManager.AllFactionsVisible.Where(
                (Faction f) => f.def == FactionDefOf.OutlanderCivil).FirstOrDefault();
            var roughOutlander = Find.FactionManager.AllFactionsVisible.Where(
                (Faction f) => f.def == FactionDefOf.OutlanderRough).FirstOrDefault();
            var civilTribe = Find.FactionManager.AllFactionsVisible.Where(
                (Faction f) => f.def == FactionDefOf.TribeCivil).FirstOrDefault();
            var roughTribe = Find.FactionManager.AllFactionsVisible.Where(
                (Faction f) => f.def == FactionDefOf.TribeRough).FirstOrDefault();
            var empire = Find.FactionManager.OfEmpire;

            foreach (var settlement in allSettlements) {
                if (settlement.Faction == null ||
                    settlement.Faction == Faction.OfPlayer ||
                    settlement.Faction.def.permanentEnemy) {
                    continue;
                }

                if (settlement.Faction == civilOutlander) {
                    outlanderSettlements.Add(settlement);
                }

                if (settlement.Faction == roughOutlander) {
                    roughOutlanderSettlements.Add(settlement);
                }

                if (settlement.Faction == civilTribe) {
                    tribeSettlements.Add(settlement);
                }

                if (settlement.Faction == roughTribe) {
                    roughTribeSettlements.Add(settlement);
                }

                if (settlement.Faction == empire) {
                    empireSettlements.Add(settlement);
                }

            }
        }

        var pollComp = Current.Game.World.GetComponent<TilePollutionComp>();
        var pollutions = pollComp != null ? Traverse.Create(pollComp).Field("tilePollution").GetValue<float[]>() : null;

        var tileCount = Current.Game.World.grid.TilesCount;
        for (var tileID = 0; tileID < tileCount; tileID++) {
            if (cancelSearch) break;
            var tile = Current.Game.World.grid[tileID];

            if (!TileFinder.IsValidTileForNewSettlement(tileID)) {
                continue;
            }

            if (filterParams.biome != null && tile.PrimaryBiome != filterParams.biome) continue;
            if (filterParams.hilliness != Hilliness.Undefined && tile.hilliness != filterParams.hilliness) continue;

            if (filterParams.minElevation > -9999 || filterParams.maxElevation < 9999)
                if (tile.elevation < filterParams.minElevation || tile.elevation > filterParams.maxElevation) continue;

            if (filterParams.minRainfall > 0 || filterParams.maxRainfall < 9999)
                if (tile.rainfall < filterParams.minRainfall || tile.rainfall > filterParams.maxRainfall) continue;

            // Northern hemisphere
            if (filterParams.hemisphere != Hemisphere.Either) {
                float lat = Find.WorldGrid.LongLatOf(tileID).y;
                if (filterParams.hemisphere == Hemisphere.Northern && lat < 0) continue;
                if (filterParams.hemisphere == Hemisphere.Southern && lat > 0) continue;
            }

            if (filterParams.river != FeatureFilter.Either) {
                bool hasRiver = tile.Rivers != null && tile.Rivers.Count > 0;

                if (filterParams.river == FeatureFilter.NotPresent && hasRiver) continue;
                if (filterParams.river == FeatureFilter.Present) {
                    if (!hasRiver) continue;

                    var tileRiver = tile.Rivers.MaxBy((SurfaceTile.RiverLink riverlink) => riverlink.river.degradeThreshold).river;

                    bool anyRiverWanted = filterParams.desiredRivers.Any(v => v);
                    bool desiredRiverFound = !anyRiverWanted;

                    for (int riverIdx = 0; !desiredRiverFound && riverIdx < allRivers.Count; riverIdx++) {
                        if (filterParams.desiredRivers[riverIdx] && allRivers[riverIdx] == tileRiver)
                            desiredRiverFound = true;
                    }

                    if (!desiredRiverFound) continue;
                }
            }

            if (filterParams.road != FeatureFilter.Either) {
                bool hasRoad = tile.Roads != null && tile.Roads.Count > 0;

                if (filterParams.road == FeatureFilter.NotPresent && hasRoad) continue;
                if (filterParams.road == FeatureFilter.Present) {
                    if (!hasRoad) continue;

                    bool anyRoadWanted = filterParams.desiredRoads.Any(v => v);
                    bool desiredRoadFound = !anyRoadWanted;

                    for (int roadIdx = 0; !desiredRoadFound && roadIdx < allRoads.Count; roadIdx++) {
                        if (!filterParams.desiredRoads[roadIdx]) continue;
                        var roadDef = allRoads[roadIdx];
                        for (int linkIdx = 0; linkIdx < tile.Roads.Count; linkIdx++) {
                            if (tile.Roads[linkIdx].road == roadDef) {
                                desiredRoadFound = true;
                                break;
                            }
                        }
                    }

                    if (!desiredRoadFound) continue;
                }
            }

            if (filterParams.coastal != FeatureFilter.Either) {
                var rot = Current.Game.World.CoastDirectionAt(tileID);

                if (filterParams.coastal == FeatureFilter.Present && !rot.IsValid) continue;
                if (filterParams.coastal == FeatureFilter.NotPresent && rot.IsValid) continue;

                if (filterParams.coastal == FeatureFilter.Present) {
                    var coastDirs = new Rot4[] { Rot4.North, Rot4.South, Rot4.East, Rot4.West };
                    bool anyDirWanted = filterParams.desiredCoastDirections.Any(v => v);
                    bool dirMatch = !anyDirWanted;
                    for (int i = 0; !dirMatch && i < 4; i++) {
                        if (filterParams.desiredCoastDirections[i] && rot == coastDirs[i])
                            dirMatch = true;
                    }
                    if (!dirMatch) continue;
                }
            }

            if (filterParams.caves != FeatureFilter.Either) {
                bool hasCaves = Find.World.HasCaves(tileID);

                if (filterParams.caves == FeatureFilter.Present && !hasCaves) continue;
                if (filterParams.caves == FeatureFilter.NotPresent && hasCaves) continue;
            }

            if (filterParams.tilePollutionFilter != FeatureFilter.Either) {
                bool hasPollution = pollutions != null && tileID < pollutions.Length && pollutions[tileID] > 0f;

                if (filterParams.tilePollutionFilter == FeatureFilter.NotPresent && hasPollution) continue;
                if (filterParams.tilePollutionFilter == FeatureFilter.Present) {
                    if (!hasPollution) continue;
                    float pct = pollutions[tileID] * 100f;
                    if (pct < filterParams.minTilePollution || pct > filterParams.maxTilePollution) continue;
                }
            }

            if (filterParams.stoneSlots[0] != null || filterParams.stoneSlots[1] != null ||
                (filterParams.stoneThirdEnabled && filterParams.stoneSlots[2] != null)) {
                var tileStones = Find.World.NaturalRockTypesIn(tileID).ToList();
                if (filterParams.stoneSlots[0] != null && !tileStones.Contains(filterParams.stoneSlots[0])) continue;
                if (filterParams.stoneSlots[1] != null && !tileStones.Contains(filterParams.stoneSlots[1])) continue;
                if (filterParams.stoneThirdEnabled && filterParams.stoneSlots[2] != null && !tileStones.Contains(filterParams.stoneSlots[2])) continue;
                if (!filterParams.stoneThirdEnabled && tileStones.Count > 2) continue;
            }

            float maxTemp = GenTemperature.CelsiusTo(GenTemperature.MaxTemperatureAtTile(tileID),
                                                     Prefs.TemperatureMode);
            float minTemp = GenTemperature.CelsiusTo(GenTemperature.MinTemperatureAtTile(tileID),
                                                     Prefs.TemperatureMode);

            if (maxTemp > (float)filterParams.maxTemp || minTemp < (float)filterParams.minTemp) {
                continue;
            }

            if (filterParams.minAvgTemp > -200 || filterParams.maxAvgTemp < 200) {
                float avgTemp = 0f;
                for (int t = 0; t < 12; t++)
                    avgTemp += GenTemperature.AverageTemperatureAtTileForTwelfth(tileID, (Twelfth)t);
                avgTemp = GenTemperature.CelsiusTo(avgTemp / 12f, Prefs.TemperatureMode);
                if (avgTemp < (float)filterParams.minAvgTemp || avgTemp > (float)filterParams.maxAvgTemp) continue;
            }

            if (filterParams.minGrowingDays > 0) {
                int numGrowingDays = GenTemperature.TwelfthsInAverageTemperatureRange(tileID, 6f, 42f).Count * 5;
                if (numGrowingDays < filterParams.minGrowingDays) continue;
            }

            if (filterParams.seasonality != Seasonality.Any) {
                Vector2 longlat = Find.WorldGrid.LongLatOf(tileID);
                Season season = SeasonUtility.GetReportedSeason(0, longlat.y);

                if (season == Season.PermanentSummer) {
                    if (filterParams.seasonality != Seasonality.PermSummer) {
                        continue;
                    }
                } else if (season == Season.PermanentWinter) {
                    if (filterParams.seasonality != Seasonality.PermWinter) {
                        continue;
                    }
                } else {
                    if (filterParams.seasonality != Seasonality.Normal) {
                        continue;
                    }
                }
            }

            Func<List<Settlement>, bool> searchSettlements = (List<Settlement> settlements) => {
                foreach (var settlement in settlements) {
                    int dist = Find.WorldGrid.TraversalDistanceBetween(tileID, settlement.Tile, passImpassable: true, 66);
                    if (dist != int.MaxValue) {
                        return true;
                    }
                }

                return false;
            };

            if (filterParams.needCivilOutlanderNear && !searchSettlements(outlanderSettlements)) continue;
            if (filterParams.needRoughOutlanderNear && !searchSettlements(roughOutlanderSettlements)) continue;
            if (filterParams.needCivilTribeNear && !searchSettlements(tribeSettlements)) continue;
            if (filterParams.needRoughTribeNear && !searchSettlements(roughTribeSettlements)) continue;
            if (filterParams.needEmpireNear && !searchSettlements(empireSettlements)) continue;

            validTiles.Push(tileID);
        }
    }
}
}
