using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.Client.Util;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Multiplayer.Client.Factions;

// Multifaction: vanilla quests have no owning faction, so quest parts executed
// in whatever context the tick held (usually spectator) - reward pawns joined
// the Spectator faction (#546), letters and ideo checks hit the wrong faction.
// Quests are now stamped with an owner whose context wraps their execution.
public static class QuestFactionOwnership
{
    public static Faction GetOwner(Quest quest)
    {
        if (quest == null ||
            !Multiplayer.WorldComp.questOwnership.TryGetValue(quest.id, out var factionId))
            return null;
        return Find.FactionManager.GetById(factionId);
    }

    public static void Stamp(Quest quest, Faction faction)
    {
        if (quest != null && faction != null)
            Multiplayer.WorldComp.questOwnership[quest.id] = faction.loadID;
    }

    public static bool IsOwnablePlayerFaction(Faction f) =>
        f is { IsPlayer: true } && f != Multiplayer.WorldComp.spectatorFaction;

    public static Faction ResolveOwner(Quest quest, Faction contextFaction)
    {
        // 1. Generation context (per-faction storyteller loop, synced command)
        if (IsOwnablePlayerFaction(contextFaction))
            return contextFaction;

        // 2. Infer from quest look targets (owning settlement)
        if (quest.TryGetPlayerFaction(out var inferred) && IsOwnablePlayerFaction(inferred))
            return inferred;

        // 3. Deterministic fallback: lowest-loadID player faction (the host's)
        return Find.FactionManager.AllFactionsListForReading
            .Where(IsOwnablePlayerFaction)
            .OrderBy(f => f.loadID)
            .FirstOrDefault();
    }

    // Old saves have no ownership data
    public static void BackfillOwnership()
    {
        var backfilled = 0;
        foreach (var quest in Find.QuestManager.QuestsListForReading)
        {
            if (Multiplayer.WorldComp.questOwnership.ContainsKey(quest.id))
                continue;
            Stamp(quest, ResolveOwner(quest, null));
            backfilled++;
        }

        if (backfilled > 0)
            MpLog.Log($"Backfilled faction ownership for {backfilled} quests");
    }
}

[HarmonyPatch(typeof(QuestGen), nameof(QuestGen.Generate))]
static class StampQuestFactionOnGeneration
{
    static void Postfix(Quest __result)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction || __result == null)
            return;

        var contextFaction = Faction.OfPlayer;
        var owner = QuestFactionOwnership.ResolveOwner(__result, contextFaction);

        // Logs identify generation sources that still run unrouted
        if (!QuestFactionOwnership.IsOwnablePlayerFaction(contextFaction))
            MpLog.Log($"Quest {__result.root?.defName} generated outside a player faction " +
                      $"context ({contextFaction?.Name}); assigned to {owner?.Name}");

        QuestFactionOwnership.Stamp(__result, owner);
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.Accept))]
static class StampQuestFactionOnAccept
{
    // Ownership follows whoever accepts (the synced command's context faction)
    static void Prefix(Quest __instance)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return;

        if (QuestFactionOwnership.IsOwnablePlayerFaction(Faction.OfPlayer))
            QuestFactionOwnership.Stamp(__instance, Faction.OfPlayer);
    }
}

[HarmonyPatch]
static class PushQuestOwnerContext
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.QuestTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.Notify_SignalReceived));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CleanupQuestParts));
    }

    static void Prefix(Quest __instance, ref bool __state)
    {
        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return;

        var owner = QuestFactionOwnership.GetOwner(__instance);
        if (owner == null)
            return;

        // Push/pop are null-balanced, safe when the owner already holds context
        ((Map)null).PushFaction(owner);
        __state = true;
    }

    static void Finalizer(bool __state)
    {
        if (__state)
            FactionExtensions.PopFaction();
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Remove))]
static class RemoveQuestOwnership
{
    static void Postfix(Quest quest)
    {
        if (Multiplayer.Client != null && quest != null)
            Multiplayer.WorldComp?.questOwnership.Remove(quest.id);
    }
}
