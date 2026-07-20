using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Client.Factions;

// Multifaction: WorldObjectComp ticking runs under the spectator world tick,
// so core-side Faction.OfPlayer gates in those paths match nobody. Two sites
// with the same root cause:
// - TimedForcedExit.ForceReform gathers `x.Faction == Faction.OfPlayer` pawns
//   and reforms via ExitMapAndCreateCaravan(.., Faction.OfPlayer): under the
//   spectator both match nothing and an expiring site map can close over the
//   owner's pawns.
// - DefeatAllEnemiesQuestComp.CompTickInterval checks
//   AnyHostileActiveThreatToPlayer and delivers rewards to AnyPlayerHomeMap:
//   completion misdetects and the letter/rewards misfire for everyone.
// Both get the deterministic owner of the site map pushed around the body.
static class WorldTickOwnerContext
{
    // Deterministic "whose map is this" for maps without a player parent
    // faction: the lowest-loadID ownable player faction with a humanlike pawn
    // spawned there (same site rule as StorytellerTargetsPatch, same selector
    // shape as IdeoContextUtil.PrimaryPlayerFollower - identical on all
    // clients).
    public static Faction DeterministicMapOwner(Map map)
    {
        if (map == null)
            return null;

        if (map.ParentFaction is { IsPlayer: true } parent &&
            QuestFactionOwnership.IsOwnablePlayerFaction(parent))
            return parent;

        return Find.FactionManager.AllFactionsListForReading
            .Where(f => QuestFactionOwnership.IsOwnablePlayerFaction(f) &&
                        map.mapPawns.SpawnedPawnsInFaction(f).Any(p => p.RaceProps.Humanlike))
            .OrderBy(f => f.loadID)
            .FirstOrDefault();
    }

    public static bool PushOwnerOf(Map map)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return false;

        var owner = DeterministicMapOwner(map);
        if (owner == null || owner == Faction.OfPlayer)
            return false;

        ((Map)null).PushFaction(owner);
        return true;
    }
}

[HarmonyPatch(typeof(TimedForcedExit), nameof(TimedForcedExit.ForceReform))]
static class TimedForcedExitOwnerContext
{
    static void Prefix(MapParent mapParent, ref bool __state) =>
        __state = WorldTickOwnerContext.PushOwnerOf(mapParent?.Map);

    static void Finalizer(bool __state)
    {
        if (__state)
            FactionExtensions.PopFaction();
    }
}

[HarmonyPatch(typeof(DefeatAllEnemiesQuestComp), nameof(DefeatAllEnemiesQuestComp.CompTickInterval))]
static class DefeatAllEnemiesQuestOwnerContext
{
    static void Prefix(DefeatAllEnemiesQuestComp __instance, ref bool __state)
    {
        if (__instance.Active && __instance.parent is MapParent { HasMap: true } mapParent)
            __state = WorldTickOwnerContext.PushOwnerOf(mapParent.Map);
    }

    static void Finalizer(bool __state)
    {
        if (__state)
            FactionExtensions.PopFaction();
    }
}
