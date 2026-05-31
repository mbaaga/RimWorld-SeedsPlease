namespace SeedsPleaseLite;

using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using static SeedsPleaseLite.ResourceBank;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_Building_GetGizmos
{
	public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building __instance)
	{
		foreach (Gizmo value in __result)
		{
			yield return value;
		}

		if (__instance is not Building_WorkTable table || !IsSeedExtractionTable(table))
		{
			yield break;
		}

		yield return new Command_Action
		{
			defaultLabel = "SPL.Bill.AddSeedExtractionBill".Translate(),
			defaultDesc = "SPL.Bill.AddSeedExtractionBill.Desc".Translate(
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillTargetCount,
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillSearchRadius.ToString("0")),
			action = () => ShowBillMenu(table)
		};
	}

	private static bool IsSeedExtractionTable(Building_WorkTable table)
	{
		return table?.def == Defs.SeedExtractionBench || table?.def == Defs.SeedExtractionSpot;
	}

	private static void ShowBillMenu(Building_WorkTable table)
	{
		List<FloatMenuOption> options = GetProduceInStock(table.Map)
			.Select(produce_def => CreateMenuOption(table, produce_def))
			.ToList();

		if (options.Count == 0)
		{
			options.Add(new FloatMenuOption("SPL.Bill.NoSeedExtractableProduceInStock".Translate(), null));
		}

		Find.WindowStack.Add(new FloatMenu(options));
	}

	private static FloatMenuOption CreateMenuOption(Building_WorkTable table, ThingDef produce_def)
	{
		ThingDef seed_def = GetSeedDef(produce_def);
		int stock_count = table.Map.resourceCounter.GetCount(produce_def);
		string label = "SPL.Bill.AddSeedExtractionBill.Option".Translate(produce_def.LabelCap, seed_def?.LabelCap ?? "?", stock_count);
		return new FloatMenuOption(label, () => AddBill(table, produce_def));
	}

	private static IEnumerable<ThingDef> GetProduceInStock(Map map)
	{
		return DefDatabase<ThingDef>.AllDefsListForReading
			.Where(produce_def => map.resourceCounter.GetCount(produce_def) > 0)
			.Where(produce_def => GetSeedDef(produce_def) != null)
			.OrderBy(produce_def => produce_def.label);
	}

	private static ThingDef GetSeedDef(ThingDef produce_def)
	{
		return produce_def.butcherProducts?
			.FirstOrDefault(product => product.thingDef?.HasModExtension<Seed>() ?? false)
			?.thingDef;
	}

	private static void SetBillLabel(Bill_Production bill, string label)
	{
		FieldInfo label_field = AccessTools.Field(typeof(Bill), "untranslatedCustomLabel")
			?? AccessTools.Field(typeof(Bill), "customLabel")
			?? AccessTools.Field(bill.GetType(), "untranslatedCustomLabel")
			?? AccessTools.Field(bill.GetType(), "customLabel");

		label_field?.SetValue(bill, label);
	}

	private static void AddBill(Building_WorkTable table, ThingDef produce_def)
	{
		Bill_Production bill = (Bill_Production)Defs.ExtractSeeds.MakeNewBill();
		ThingDef seed_def = GetSeedDef(produce_def);

		bill.ingredientFilter.SetDisallowAll();
		bill.ingredientFilter.SetAllow(produce_def, true);
		bill.repeatMode = BillRepeatModeDefOf.TargetCount;
		bill.targetCount = ModSettings_SeedsPleaseLiteRedux.seedExtractionBillTargetCount;
		bill.ingredientSearchRadius = ModSettings_SeedsPleaseLiteRedux.seedExtractionBillSearchRadius;
		SetBillLabel(bill, "SPL.Bill.ExtractSeedsLabel".Translate(seed_def?.label ?? produce_def.label));

		table.BillStack.AddBill(bill);
		Messages.Message("SPL.Bill.SeedExtractionBillAdded".Translate(bill.LabelCap), table, MessageTypeDefOf.PositiveEvent, false);
	}
}
