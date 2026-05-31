namespace SeedsPleaseLite;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
	private static readonly PropertyInfo bill_renamable_label_property = AccessTools.Property(typeof(Bill_Production), "RenamableLabel");
	private static readonly MethodInfo bill_renamable_label_setter = AccessTools.PropertySetter(typeof(Bill_Production), "RenamableLabel");

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
		return "SPL.Bill.ExtractSeedsLabel".Translate(seed_def?.label ?? produce_def.label);
	}

	private static void SetBillName(Bill_Production bill, string label)
	{
		SetNativeBillName(bill, label);
		SetBetterWorkbenchManagementBillName(bill, label);
	}

	private static bool SetNativeBillName(Bill_Production bill, string label)
	{
		if (bill == null)
		{
			return false;
		}

		if (bill_renamable_label_setter != null)
		{
			bill_renamable_label_setter.Invoke(bill, new object[] { label });
			return true;
		}

		if (bill_renamable_label_property?.CanWrite == true)
		{
			bill_renamable_label_property.SetValue(bill, label, null);
			return true;
		}

		return SetFirstStringField(bill, label,
			"playerCustomName",
			"customName",
			"untranslatedCustomLabel",
			"customLabel");
	}

	private static bool SetFirstStringField(Bill_Production bill, string label, params string[] field_names)
	{
		foreach (string field_name in field_names)
		{
			FieldInfo field = AccessTools.Field(bill.GetType(), field_name)
				?? AccessTools.Field(typeof(Bill_Production), field_name)
				?? AccessTools.Field(typeof(Bill), field_name);

			if (field?.FieldType != typeof(string))
			{
				continue;
			}

			field.SetValue(bill, label);
			return true;
		}

		return false;
	}

	private static void SetBetterWorkbenchManagementBillName(Bill_Production bill, string label)
	{
		try
		{
			Type main_type = GenTypes.GetTypeInAnyAssembly("ImprovedWorkbenches.Main");

			if (main_type == null)
			{
				return;
			}

			object main_instance = AccessTools.Property(main_type, "Instance")?.GetValue(null, null)
				?? AccessTools.Field(main_type, "Instance")?.GetValue(null);

			if (main_instance == null)
			{
				return;
			}

			MethodInfo storage_getter = AccessTools.Method(main_type, "GetExtendedBillDataStorage");
			object storage = storage_getter?.Invoke(main_instance, null);

			if (storage == null)
			{
				return;
			}

			MethodInfo get_or_create = AccessTools.Method(storage.GetType(), "GetOrCreateExtendedDataFor", new[] { typeof(Bill_Production) });
			object extended_data = get_or_create?.Invoke(storage, new object[] { bill });
			FieldInfo name_field = extended_data == null ? null : AccessTools.Field(extended_data.GetType(), "Name");

			if (name_field?.FieldType == typeof(string))
			{
				name_field.SetValue(extended_data, label);
			}
		}
		catch (Exception exception)
		{
			Log.WarningOnce($"[SeedsPleaseLiteRedux] Could not set Better Workbench Management bill name: {exception.Message}", 816631452);
		}
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
