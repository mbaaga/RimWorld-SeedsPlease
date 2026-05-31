namespace SeedsPleaseLite;

using System.Collections.Generic;
using UnityEngine;
using Verse;

public class ModSettings_SeedsPleaseLiteRedux : ModSettings
{
    public override void ExposeData()
    {
        Scribe_Values.Look(ref marketValueModifier, "marketValueModifier", 1f);
        Scribe_Values.Look(ref extractionModifier, "extractionModifier", 1f);
        Scribe_Values.Look(ref seedFactorModifier, "seedFactorModifier", 1f);
        Scribe_Values.Look(ref noUselessSeeds, "noUselessSeeds", true);
        Scribe_Values.Look(ref clearSnow, "clearSnow");
        Scribe_Values.Look(ref edibleSeeds, "edibleSeeds", true);
        Scribe_Values.Look(ref showAllPlantsInGrowMenu, "showAllPlantsInGrowMenu", false);
        Scribe_Values.Look(ref seedExtractionBillTargetCount, "seedExtractionBillTargetCount", 100);
        Scribe_Values.Look(ref seedExtractionBillSearchRadius, "seedExtractionBillSearchRadius", 20f);
        Scribe_Collections.Look(ref seedlessInversions, "seedless", LookMode.Value);

        base.ExposeData();
    }

    public static float marketValueModifier = 1f;
    public static float extractionModifier = 1f;
    public static float seedFactorModifier = 1f;
    public static bool noUselessSeeds = true, clearSnow, edibleSeeds = true, showAllPlantsInGrowMenu;
    public static int seedExtractionBillTargetCount = 100;
    public static float seedExtractionBillSearchRadius = 20f;
    public static HashSet<string> seedlessInversions;
    public static HashSet<ushort> seedlessCache;
    public static Tab selectedTab = Tab.seedless;
    public enum Tab { seedless, labels };
    public static Vector2 scrollPos;
}
