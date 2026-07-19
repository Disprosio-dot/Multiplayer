using HarmonyLib;
using Multiplayer.Client.Util;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions;

// Safety nets for multifaction incident routing (the root fix is in
// StorytellerTargetsPatch): log anything that still slips through.

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
static class SuppressCrossFactionIncidents
{
    static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return true;

        if (parms.target is Map { ParentFaction: { IsPlayer: true } owner } && owner != Faction.OfPlayer)
        {
            // QuestFactionOwnership now routes quest-fired incidents through
            // the owner's context - spectator context here is a residual gap.
            // Log it but let it through rather than eat a quest raid.
            if (Faction.OfPlayer == Multiplayer.WorldComp.spectatorFaction)
            {
                MpLog.Log(
                    $"Spectator-context incident {__instance.def?.defName} targeting map of " +
                    $"{owner.Name} - quest-ownership routing gap (61-quests-review.md)");
                return true;
            }

            MpLog.Error(
                $"Suppressed cross-faction incident {__instance.def?.defName} targeting map of " +
                $"{owner.Name} while executing as {Faction.OfPlayer?.Name} - report this, the " +
                "root-cause filter should have prevented it");
            // Report "fired" so the storyteller/queue doesn't retry it elsewhere
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
static class WarnSpectatorStorytellerFire
{
    static void Prefix(FiringIncident fi)
    {
        if (Multiplayer.Client != null && Multiplayer.GameComp.multifaction &&
            Faction.OfPlayer == Multiplayer.WorldComp.spectatorFaction)
            MpLog.Error($"Storyteller fired {fi?.def?.defName} in spectator faction context - " +
                        "an unrouted generation source (see 62-incidents-review.md)");
    }
}
