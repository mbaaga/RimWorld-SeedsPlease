namespace SeedsPleaseLite;

using System;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

//This patch controls the dropping of seeds upon harvest
[HarmonyPatch]
public class Patch_WorkGiver_GrowerSow_JobOnCell
{
    const int SeedsToCarry = 25;
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(WorkGiver_GrowerSow), nameof(WorkGiver_GrowerSow.JobOnCell));

        //Try patch support for VE More Plants
        MethodInfo WorkGiver_GrowerSowSandy_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowSandy")?.GetMethod("JobOnCell");
        MethodInfo WorkGiver_GrowerSowAquatic_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowAquatic")?.GetMethod("JobOnCell");
        if (WorkGiver_GrowerSowSandy_JobOnCell != null && WorkGiver_GrowerSowAquatic_JobOnCell != null)
        {
            yield return WorkGiver_GrowerSowSandy_JobOnCell;
            yield return WorkGiver_GrowerSowAquatic_JobOnCell;
        }

        MethodInfo WorkGiver_GrowerSowMushroom_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMushrooms.WorkGiver_GrowerSowMushroom")?.GetMethod("JobOnCell");
        if (WorkGiver_GrowerSowMushroom_JobOnCell != null)
        {
            yield return WorkGiver_GrowerSowMushroom_JobOnCell;
        }
    }

    public static Job Postfix(Job __result, Pawn pawn, IntVec3 c, bool forced)
    {
        if (__result == null || __result.def != JobDefOf.Sow)
        {
            return __result;
        }

        ThingDef seed = __result.plantDefToSow?.blueprintDef;
        if (seed == null || seed.thingCategories.NullOrEmpty())
        {
            return __result;
        }

        Map map = pawn.Map;
        if (ModSettings_SeedsPleaseLiteRedux.clearSnow && NeedsToClearSnowFirst(c, map, pawn, ref __result))
        {
            return __result;
        }

        if (NeedsToCutFirst(c, map, pawn, forced, ref __result))
            return __result;

        //Predicate filtering the kind of seed allowed
        Predicate<Thing> predicate = tempThing =>
            !ForbidUtility.IsForbidden(tempThing, pawn.Faction)
            && ForbidUtility.InAllowedArea(tempThing.Position, pawn)
            && PawnLocalAwareness.AnimalAwareOf(pawn, tempThing)
            && ReservationUtility.CanReserve(pawn, tempThing, 1);

        //Find the instance on the map to go fetch
        Thing bestSeedThingForSowing = GenClosest.ClosestThingReachable(c, map, ThingRequest.ForDef(seed), PathEndMode.ClosestTouch, TraverseParms.For(pawn), validator: predicate);

        return bestSeedThingForSowing == null ? null : new Job(ResourceBank.Defs.SowWithSeeds, c, bestSeedThingForSowing)
        {
            plantDefToSow = __result.plantDefToSow,
            count = SeedsToCarry
        };
    }

    static bool NeedsToClearSnowFirst(IntVec3 cell, Map map, Pawn pawn, ref Job job)
    {
        var zoneCells = cell.GetZone(map)?.cells;
        if (!PlantUtility.SnowAllowsPlanting(cell, map))
        {
            for (int i = zoneCells?.Count ?? 0; i-- > 0;)
            {
                Job clearSnowJob = JobMaker.MakeJob(JobDefOf.ClearSnow, cell);
                if (clearSnowJob.MakeDriver(pawn).TryMakePreToilReservations(false))
                {
                    pawn.ClearReservationsForJob(clearSnowJob);
                    job = clearSnowJob;
                    return true;
                }
            }
        }

        return false;
    }

    static bool NeedsToCutFirst(IntVec3 cell, Map map, Pawn pawn, bool forced, ref Job job)
    {
        var zoneCells = cell.GetZone(map)?.cells;
        if( zoneCells == null )
            return false;
        // First check to cut the cell itself.
        if( zoneCells?.Contains( cell ) ?? false )
            if( NeedsToCutFirstHelper( cell, map, pawn, forced, ref job ))
                return true;
        // Then cells around it.
        foreach( IntVec3 c in GenAdjFast.AdjacentCells8Way( cell ))
            if( zoneCells?.Contains( c ) ?? false )
                if( NeedsToCutFirstHelper( c, map, pawn, forced, ref job ))
                    return true;
        // Then check to cut all other cells of the growing zone. This prevents pawns from running
        // back and forth with seeds to plant one plant at a time if priority of growing is higher
        // than priority of cutting.
        foreach( IntVec3 c in zoneCells )
            if( NeedsToCutFirstHelper( c, map, pawn, forced, ref job ))
                return true;
        return false;
    }

    static bool NeedsToCutFirstHelper(IntVec3 cell, Map map, Pawn pawn, bool forced, ref Job job)
    {
        if (!cell.InBounds(map))
            return false;
        // This is pretty much a copy&paste of the JobDefOf.CutPlant part of WorkGiver_GrowerSow.JobOnCell().
        Plant plant = cell.GetPlant(map);
        if (plant != null)
        {
            if( plant.def == job.plantDefToSow )
                return false;
            if (!pawn.CanReserve(plant, 1, -1, null, forced) || plant.IsForbidden(pawn))
                return false;
            Zone_Growing zone_Growing = cell.GetZone(map) as Zone_Growing;
            if (zone_Growing != null && !zone_Growing.allowCut)
                return false;
            if (!forced && plant.TryGetComp<CompPlantPreventCutting>(out var comp) && comp.PreventCutting)
                return false;
            if (!PlantUtility.PawnWillingToCutPlant_Job(plant, pawn))
                return false;
            Job cutJob = JobMaker.MakeJob(JobDefOf.CutPlant, plant);
            if (cutJob.MakeDriver(pawn).TryMakePreToilReservations(false))
            {
                pawn.ClearReservationsForJob(cutJob);
                job = cutJob;
                return true;
            }
        }
        return false;
    }
}
