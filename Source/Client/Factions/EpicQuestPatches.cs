using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions;

// Vanilla blocks new epic quests (RelicHunt and friends) while ANY epic quest
// is pending or ongoing - a global check, so in multifaction one faction's
// relic hunt starves every other faction of its own. The per-faction
// storyteller repeat already runs this comp once per faction with its context
// pushed; here the "already active" check only counts epic quests owned by
// the faction being ticked, so each ideology gets its own relic hunt.
[HarmonyPatch(typeof(StorytellerComp_RandomEpicQuest), nameof(StorytellerComp_RandomEpicQuest.MakeIntervalIncidents))]
static class PerFactionEpicQuests
{
    private delegate IEnumerable<FiringIncident> BaseIntervalIncidents(StorytellerComp_OnOffCycle self, IIncidentTarget target);

    // Non-virtual call into the base implementation, bypassing the epic gate override
    private static readonly BaseIntervalIncidents baseIntervalIncidents =
        AccessTools.MethodDelegate<BaseIntervalIncidents>(
            AccessTools.Method(typeof(StorytellerComp_OnOffCycle), nameof(StorytellerComp_OnOffCycle.MakeIntervalIncidents)),
            virtualCall: false);

    static bool Prefix(StorytellerComp_RandomEpicQuest __instance, IIncidentTarget target,
        ref IEnumerable<FiringIncident> __result)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return true;

        __result = IntervalIncidentsForFaction(__instance, target, Faction.OfPlayer);
        return false;
    }

    private static IEnumerable<FiringIncident> IntervalIncidentsForFaction(
        StorytellerComp_RandomEpicQuest comp, IIncidentTarget target, Faction faction)
    {
        foreach (var quest in Find.QuestManager.QuestsListForReading)
        {
            if (!quest.root.IsEpic || (quest.State != QuestState.NotYetAccepted && quest.State != QuestState.Ongoing))
                continue;

            // Unowned epic quests (pre-ownership saves) keep vanilla's global gate
            var owner = QuestFactionOwnership.GetOwner(quest);
            if (owner == null || owner == faction)
                yield break;
        }

        foreach (var incident in baseIntervalIncidents(comp, target))
            yield return incident;
    }
}
