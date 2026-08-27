using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Multiplayer.Client.Factions;

[HarmonyPatch(typeof(SettlementUtility), nameof(SettlementUtility.AttackNow))]
static class AttackNowPatch
{
    static void Prefix(Caravan caravan)
    {
        FactionContext.Push(caravan.Faction);
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(GetOrGenerateMapUtility), nameof(GetOrGenerateMapUtility.GetOrGenerateMap), [typeof(PlanetTile), typeof(IntVec3), typeof(WorldObjectDef), typeof(IEnumerable<GenStepWithParams>), typeof(bool)])]
static class MapGenFactionPatch
{
    static void Prefix(PlanetTile tile)
    {
        Faction factionToSet = GetFactionAt(tile);

        if (Multiplayer.Client != null && factionToSet == null)
            Log.Warning($"Couldn't set the faction context for map gen at {tile.tileId}: no world object and no stored faction.");

        FactionContext.Push(factionToSet);
    }

    private static Faction GetFactionAt(PlanetTile tile)
    {
        var worldObjectsHolder = Find.WorldObjects;

        var mapParent = worldObjectsHolder.MapParentAt(tile);
        if (mapParent != null && mapParent.Faction is { IsPlayer: true })
            return mapParent.Faction;

        // Desync-106: PlayerControlledCaravanAt is viewer-relative
        // (IsPlayerControlled compares against ambient Faction.OfPlayer), so
        // only the arriving caravan's own client resolved it here - everyone
        // else fell through to a null context and generated the quest-site map
        // under their own ambient faction. Look it up by actual faction, like
        // the transporter and gravship checks below already do.
        var caravan = worldObjectsHolder.Caravans.Find(c => c.Tile == tile && c.Faction is { IsPlayer: true });
        if (caravan != null)
            return caravan.Faction;

        var transporters = worldObjectsHolder.TravellingTransporters.Find(t => t.destinationTile == tile && t.Faction is { IsPlayer: true });
        if (transporters != null)
            return transporters.Faction;

        var gravship = worldObjectsHolder.AllWorldObjects.Find(t => t is Gravship g && g.destinationTile == tile && t.Faction is { IsPlayer: true });
        if (gravship != null)
            return gravship.Faction;

        var stored = TileFactionContext.GetFactionForTile(tile);
        if (stored != null)
            return stored;

        // Deterministic last resort, mirroring MapSetup.DeterministicInitFaction:
        // generation must never run under the ambient faction - that's the
        // viewer, different on every client
        if (Multiplayer.Client != null)
        {
            var spectator = Multiplayer.WorldComp?.spectatorFaction;
            return Find.FactionManager.AllFactionsListForReading
                .Where(f => f.IsPlayer && f != spectator)
                .OrderBy(f => f.loadID)
                .FirstOrDefault();
        }

        return null;
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter), new[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
static class CaravanEnterFactionPatch
{
    static void Prefix(Caravan caravan)
    {
        FactionContext.Push(caravan.Faction);
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(WealthWatcher), nameof(WealthWatcher.ForceRecount))]
static class WealthRecountFactionPatch
{
    static void Prefix(WealthWatcher __instance)
    {
        FactionContext.Push(__instance.map.ParentFaction);
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(FactionIdeosTracker), nameof(FactionIdeosTracker.RecalculateIdeosBasedOnPlayerPawns))]
static class RecalculateFactionIdeosContext
{
    static void Prefix(FactionIdeosTracker __instance)
    {
        FactionContext.Push(__instance.faction);
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(Bill), nameof(Bill.ValidateSettings))]
static class BillValidateSettingsPatch
{
    static void Prefix(Bill __instance)
    {
        if (Multiplayer.Client == null) return;
        FactionContext.Push(__instance.pawnRestriction?.Faction); // todo HostFaction, SlaveFaction?
    }

    static void Finalizer()
    {
        if (Multiplayer.Client == null) return;
        FactionContext.Pop();
    }
}

[HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ValidateSettings))]
static class BillProductionValidateSettingsPatch
{
    static void Prefix(Bill_Production __instance, ref Map __state)
    {
        if (Multiplayer.Client == null) return;

        if (__instance.Map != null && __instance.billStack?.billGiver is Thing { Faction: { } faction })
        {
            __instance.Map.PushFaction(faction);
            __state = __instance.Map;
        }
    }

    static void Finalizer(Map __state)
    {
        __state?.PopFaction();
    }
}

[HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.ArriveNewMap))]
static class GravshipArriveNewMapFactionPatch
{
    static void Prefix(Gravship gravship)
    {
        FactionContext.Push(gravship.Faction);
    }

    static void Finalizer()
    {
        FactionContext.Pop();
    }
}

// Clean up after map generation is complete
[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
static class CleanupTileFactionContext
{
    static void Finalizer(MapParent parent)
    {
        if (parent != null)
            TileFactionContext.ClearTile(parent.Tile);
    }
}

// rwmt#963: the caller-context patches above pin Faction.OfPlayer for map
// generation, but bare FactionContext.Push doesn't swap the per-faction world
// data - Find.ResearchManager stayed the local viewer's, so generation-time
// content that reads research state (techprint crates) rolled from different
// candidate lists per client: same item slot, different tech, and extra Rand
// draws shifting everything downstream (trace-hash desync). Swap the world
// data to match OfPlayer for the duration of generation; the finalizer
// restores whichever faction's data was active before, so an exception can't
// leave another faction's research installed.
[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
static class MapGenFactionDataPatch
{
    static void Prefix(ref Faction __state)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return;
        if (Faction.OfPlayer is not { } genFaction)
            return;

        __state = Find.FactionManager.GetById(
            Multiplayer.WorldComp.GetFactionId(Find.ResearchManager));
        Multiplayer.WorldComp.SetFaction(genFaction);
    }

    static void Finalizer(Faction __state)
    {
        if (__state != null)
            Multiplayer.WorldComp.SetFaction(__state);
    }
}
