namespace SeedsPleaseLite;

using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using static SeedsPleaseLite.ResourceBank;

[HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.GetGizmos))]
public static class Patch_ZoneGrowing_GetGizmos
{
	public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values)
	{
		foreach (Gizmo value in values)
		{
			yield return value;
		}

		yield return new Command_Toggle
		{
			defaultLabel = "SPL.GrowZone.ShowAllPlants".Translate(),
			defaultDesc = "SPL.GrowZone.ShowAllPlants.Desc".Translate(),
			icon = Widgets.CheckboxOnTex,
			isActive = () => ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu,
			toggleAction = ToggleShowAllPlants
		};
	}

	private static void ToggleShowAllPlants()
	{
		ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu = !ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu;
		LoadedModManager.GetMod<Mod_SeedsPlease>()?.WriteSettings();
	}
}

[HarmonyPatch(typeof(Building_PlantGrower), nameof(Building_PlantGrower.GetGizmos))]
public static class Patch_BuildingPlantGrower_GetGizmos
{
	public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values)
	{
		foreach (Gizmo value in values)
		{
			yield return value;
		}

		yield return new Command_Toggle
		{
			defaultLabel = "SPL.GrowZone.ShowAllPlants".Translate(),
			defaultDesc = "SPL.GrowZone.ShowAllPlants.Desc".Translate(),
			icon = Widgets.CheckboxOnTex,
			isActive = () => ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu,
			toggleAction = ToggleShowAllPlants
		};
	}

	private static void ToggleShowAllPlants()
	{
		ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu = !ModSettings_SeedsPleaseLiteRedux.showAllPlantsInGrowMenu;
		LoadedModManager.GetMod<Mod_SeedsPlease>()?.WriteSettings();
	}
}
