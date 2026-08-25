using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace Multiplayer.Client.Patches;

// Twin of UnsyncedSpawnTripwire for jobs. A job started or ended on a spawned
// pawn outside the synced simulation exists on this client only: it consumes a
// job id and usually a Rand draw, and the session desyncs later when the
// diverged pawn first behaves differently (live case: an extra untraced job on
// a tamed elk put the client one job id and one draw ahead - the same family
// as the drafted-goto bug the base PR fixed). Warn the moment it happens, once
// per pawn+job, with the stack that names the offending UI path.
static class UnsyncedJobTripwire
{
    public static void Report(Pawn_JobTracker tracker, JobDef def, string what)
    {
        if (Multiplayer.Client == null || !Multiplayer.InInterface)
            return;

        var pawn = tracker.pawn;
        // Negative ids are interface-local pawns (portraits, previews) - not sim
        if (pawn == null || !pawn.Spawned || pawn.thingIDNumber < 0)
            return;

        Log.WarningOnce(
            $"MP: {what} ({def?.defName ?? "null"}) on {pawn} outside the synced simulation - " +
            $"an unsynced UI/mod action is diverging this client. Stack: {Environment.StackTrace}",
            Gen.HashCombineInt(pawn.thingIDNumber, def?.shortHash ?? 0) ^ 0x10B5);
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
static class UnsyncedStartJobTripwire
{
    static void Prefix(Pawn_JobTracker __instance, Job newJob) =>
        UnsyncedJobTripwire.Report(__instance, newJob?.def, "StartJob");
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
static class UnsyncedEndJobTripwire
{
    static void Prefix(Pawn_JobTracker __instance) =>
        UnsyncedJobTripwire.Report(__instance, __instance.curJob?.def, "EndCurrentJob");
}
