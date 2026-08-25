using System;
using HarmonyLib;
using Multiplayer.Client.Util;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

// Diagnostic tripwires for content divergence in per-faction world data.
// Live case (Marco's desync reports, twice at the same spot): after
// retire->spectator->create, host and client disagreed on faction 19's
// History datacore flags, so StorytellerComp_MechanitorComplexQuest's gate
// broke one client out of the comp while the other rolled MTB. These log
// every write to those flags with full context so the diverging writer can
// be identified from a single reproduction. Remove once the writer is fixed.
static class DatacoreFlagTripwire
{
    public static void Report(History history, string flag)
    {
        if (Multiplayer.Client == null)
            return;

        // Which faction's history instance is being written?
        int historyFaction = -1;
        foreach (var (id, data) in Multiplayer.WorldComp.factionData)
            if (data.history == history)
            {
                historyFaction = id;
                break;
            }

        MpLog.Log(
            $"TRIPWIRE {flag}: historyFaction={historyFaction} " +
            $"ofPlayer={Faction.OfPlayer?.loadID.ToString() ?? "null"} " +
            $"ticking={Multiplayer.Ticking} cmds={Multiplayer.ExecutingCmds} " +
            $"tick={Find.TickManager?.TicksGame ?? -1}\n" +
            $"{Environment.StackTrace}");
    }
}

[HarmonyPatch(typeof(History), nameof(History.Notify_MechanoidDatacoreOppurtunityAvailable))]
static class DatacoreOpportunityTripwire
{
    static void Prefix(History __instance) =>
        DatacoreFlagTripwire.Report(__instance, "MechanoidDatacoreOpportunityAvailable");
}

[HarmonyPatch(typeof(History), nameof(History.Notify_MechanoidDatacoreReadOrLost))]
static class DatacoreReadOrLostTripwire
{
    static void Prefix(History __instance) =>
        DatacoreFlagTripwire.Report(__instance, "MechanoidDatacoreReadOrLost");
}
