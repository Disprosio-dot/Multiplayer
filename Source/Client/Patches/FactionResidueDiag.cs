using HarmonyLib;
using Multiplayer.Client.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Client.Patches
{
    // Diagnostic only. The world tick swaps every map onto the spectator
    // faction's data and restores afterwards; if any path leaves a map on the
    // wrong faction's managers, UI reads (resource readout, zones, areas,
    // low-food alert nutrition) alternate between factions' data on frames
    // that ran a world tick - a flicker/re-ping shape that only exists at
    // mixed speeds. The restore is hardened now, but this check catches ANY
    // residue source red-handed at UI time, named and counted. Log-only.
    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootUpdate))]
    static class FactionResidueDiag
    {
        private static int residueFrames;
        private static float lastReport;
        private const float ReportIntervalSeconds = 60f;

        static void Postfix()
        {
            if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction) return;

            var map = Find.CurrentMap;
            var realFaction = Multiplayer.RealPlayerFaction;
            if (map == null || realFaction == null) return;

            var comp = map.MpComp();
            if (comp == null || !comp.factionData.TryGetValue(realFaction.loadID, out var ownData)) return;

            if (ReferenceEquals(map.resourceCounter, ownData.resourceCounter)) return;

            residueFrames++;
            var now = Time.realtimeSinceStartup;
            if (now - lastReport < ReportIntervalSeconds) return;
            lastReport = now;

            var installedFactionId = -1;
            foreach (var kv in comp.factionData)
                if (ReferenceEquals(map.resourceCounter, kv.Value.resourceCounter))
                    installedFactionId = kv.Key;

            MpLog.Warn(
                $"Faction residue: map {map.uniqueID} has faction {installedFactionId}'s data installed at UI time " +
                $"(local faction {realFaction.loadID}, OfPlayer {Faction.OfPlayer?.loadID}); " +
                $"{residueFrames} affected frames since last report");
            residueFrames = 0;
        }
    }
}
