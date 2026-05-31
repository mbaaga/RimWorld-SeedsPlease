namespace SeedsPleaseLite;

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static SeedsPleaseLite.ResourceBank;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_Building_GetGizmos
{
	private static readonly Texture2D add_bill_icon = ContentFinder<Texture2D>.Get("UI/Commands/SPL_AddSeedExtractionBill", true);
	private static readonly Texture2D add_missing_bills_icon = ContentFinder<Texture2D>.Get("UI/Commands/SPL_AddMissingSeedExtractionBills", true);

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
			icon = add_bill_icon,
			defaultDesc = "SPL.Bill.AddSeedExtractionBill.Desc".Translate(
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillTargetCount,
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillSearchRadius.ToString("0")),
			action = () => ShowBillMenu(table)
		};

		yield return new Command_Action
		{
			defaultLabel = "SPL.Bill.AddMissingSeedExtractionBills".Translate(),
			icon = add_missing_bills_icon,
			defaultDesc = "SPL.Bill.AddMissingSeedExtractionBills.Desc".Translate(
				GetMissingProduce(table).Count(),
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillTargetCount,
				ModSettings_SeedsPleaseLiteRedux.seedExtractionBillSearchRadius.ToString("0")),
			action = () => AddMissingBills(table)
		};
	}

	private static bool IsSeedExtractionTable(Building_WorkTable table)
	{
		return table?.def == Defs.SeedExtractionBench || table?.def == Defs.SeedExtractionSpot;
	}

	private static void ShowBillMenu(Building_WorkTable table)
	{
		List<ThingDef> produce_defs = GetProduceInStock(table.Map).ToList();
		List<FloatMenuOption> options = new List<FloatMenuOption>();

		if (produce_defs.Count == 0)
		{
			options.Add(new FloatMenuOption("SPL.Bill.NoSeedExtractableProduceInStock".Translate(), null));
			Find.WindowStack.Add(new FloatMenu(options));
			return;
		}

		int missing_count = produce_defs.Count(produce_def => !BillExistsForProduce(table, produce_def));

		if (missing_count > 0)
		{
			options.Add(new FloatMenuOption("SPL.Bill.AddMissingSeedExtractionBills.Option".Translate(missing_count), () => AddMissingBills(table)));
		}
		else
		{
			options.Add(new FloatMenuOption("SPL.Bill.AllSeedExtractionBillsAlreadyExist".Translate(), null));
		}

		foreach (ThingDef produce_def in produce_defs)
		{
			options.Add(CreateMenuOption(table, produce_def));
		}

		Find.WindowStack.Add(new FloatMenu(options));
	}

	private static FloatMenuOption CreateMenuOption(Building_WorkTable table, ThingDef produce_def)
	{
		ThingDef seed_def = GetSeedDef(produce_def);
		bool has_bill = BillExistsForProduce(table, produce_def);
		int stock_count = table.Map.resourceCounter.GetCount(produce_def);
		string label_key = has_bill
			? "SPL.Bill.AddSeedExtractionBill.Option.Existing"
			: "SPL.Bill.AddSeedExtractionBill.Option.Missing";
		string label = label_key.Translate(produce_def.LabelCap, seed_def?.LabelCap ?? "?", stock_count);
		Action action = () => AddBill(table, produce_def);

		return new FloatMenuOption(label, action);
	}

	private static IEnumerable<ThingDef> GetProduceInStock(Map map)
	{
		return DefDatabase<ThingDef>.AllDefsListForReading
			.Where(produce_def => map.resourceCounter.GetCount(produce_def) > 0)
			.Where(produce_def => GetSeedDef(produce_def) != null)
			.OrderBy(produce_def => produce_def.label);
	}

	private static IEnumerable<ThingDef> GetMissingProduce(Building_WorkTable table)
	{
		return GetProduceInStock(table.Map)
			.Where(produce_def => !BillExistsForProduce(table, produce_def));
	}

	private static bool BillExistsForProduce(Building_WorkTable table, ThingDef produce_def)
	{
		if (table?.BillStack?.Bills == null || produce_def == null)
		{
			return false;
		}

		return table.BillStack.Bills
			.OfType<Bill_Production>()
			.Any(bill => bill.recipe == Defs.ExtractSeeds && bill.ingredientFilter.Allows(produce_def));
	}

	private static ThingDef GetSeedDef(ThingDef produce_def)
	{
		return produce_def.butcherProducts?
			.FirstOrDefault(product => product.thingDef?.HasModExtension<Seed>() ?? false)
			?.thingDef;
	}

	private static void AddMissingBills(Building_WorkTable table)
	{
		List<ThingDef> missing_produce_defs = GetMissingProduce(table).ToList();

		if (missing_produce_defs.Count == 0)
		{
			Messages.Message("SPL.Bill.NoMissingSeedExtractionBills".Translate(), table, MessageTypeDefOf.NeutralEvent, false);
			return;
		}

		foreach (ThingDef produce_def in missing_produce_defs)
		{
			AddBill(table, produce_def, false);
		}

		Messages.Message("SPL.Bill.SeedExtractionBillsAdded".Translate(missing_produce_defs.Count), table, MessageTypeDefOf.PositiveEvent, false);
	}

	private static string GetBillLabel(ThingDef produce_def)
	{
		ThingDef seed_def = GetSeedDef(produce_def);
		string seed_label = (seed_def?.label ?? produce_def.label ?? "?").ToLowerInvariant();
		string label = "SPL.Bill.ExtractSeedsLabel".Translate(seed_label);

		return CapitalizeFirstLetter(label);
	}

	private static string CapitalizeFirstLetter(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		if (value.Length == 1)
		{
			return value.ToUpperInvariant();
		}

		return char.ToUpperInvariant(value[0]) + value.Substring(1);
	}

	private static void SetBillName(Bill_Production bill, string label)
	{
		bill.RenamableLabel = label;
	}

	private static void AddBill(Building_WorkTable table, ThingDef produce_def, bool show_message = true)
	{
		Bill_Production bill = (Bill_Production)Defs.ExtractSeeds.MakeNewBill();

		bill.ingredientFilter.SetDisallowAll();
		bill.ingredientFilter.SetAllow(produce_def, true);
		bill.repeatMode = BillRepeatModeDefOf.TargetCount;
		bill.targetCount = ModSettings_SeedsPleaseLiteRedux.seedExtractionBillTargetCount;
		bill.ingredientSearchRadius = ModSettings_SeedsPleaseLiteRedux.seedExtractionBillSearchRadius;

		SetBillName(bill, GetBillLabel(produce_def));

		table.BillStack.AddBill(bill);

		if (show_message)
		{
			Messages.Message("SPL.Bill.SeedExtractionBillAdded".Translate(bill.LabelCap), table, MessageTypeDefOf.PositiveEvent, false);
		}
	}
}
