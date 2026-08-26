using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Client.Factions;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

// Fluid ideo development in multifaction: vanilla credits event/quest points
// through the ambient Faction.OfPlayer of whatever tick context the trigger
// fired in (PreceptComp_DevelopmentPoints reads OfPlayer.ideos.FluidIdeo), so
// a pawn acting on another faction's map loses its points to the context
// mismatch, and the "points earned" toasts and "you can reform" letter go to
// every player. These patches credit by the faction actually responsible
// (doer's faction, then quest owner, then context) and route the
// notifications to that faction only. Point mutation stays deterministic:
// the responsible faction is derived from sim state, identical on every
// client; only the toast/letter visibility is per-client.
static class IdeoDevelopmentScope
{
    public static bool Active => Multiplayer.Client != null && Multiplayer.GameComp.multifaction;

    // While set, dev-point toasts are shown only to this faction's players
    public static Faction interestedFaction;
}

// Event/quest development points: credit by the responsible faction instead
// of the ambient context. Body mirrors the vanilla method.
[HarmonyPatch(typeof(PreceptComp_DevelopmentPoints), nameof(PreceptComp_DevelopmentPoints.Notify_HistoryEvent))]
static class DevPointsEventResponsibleFaction
{
    static bool Prefix(PreceptComp_DevelopmentPoints __instance, HistoryEvent ev, Precept precept)
    {
        if (!IdeoDevelopmentScope.Active)
            return true;

        if (ev.def != __instance.eventDef)
            return false;

        var responsible = ResolveResponsibleFaction(ev);
        if (responsible?.ideos == null || precept.ideo != responsible.ideos.FluidIdeo ||
            !precept.ideo.development.CanBeDevelopedNow)
            return false;

        ev.args.TryGetArg(HistoryEventArgsNames.Ideo, out Ideo ideoArg);
        if (ideoArg != null && ideoArg != precept.ideo)
            return false;

        int before = precept.ideo.development.Points;
        IdeoDevelopmentScope.interestedFaction = responsible;
        try
        {
            if (precept.ideo.development.TryAddDevelopmentPoints(__instance.points))
            {
                ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer);
                ev.args.TryGetArg(HistoryEventArgsNames.Quest, out Quest quest);
                Messages.Message(
                    "MessageDevelopmentPointsEarned".Translate(before, precept.ideo.development.Points, __instance.Label),
                    doer, MessageTypeDefOf.PositiveEvent, quest);
            }
        }
        finally
        {
            IdeoDevelopmentScope.interestedFaction = null;
        }

        return false;
    }

    static Faction ResolveResponsibleFaction(HistoryEvent ev)
    {
        if (ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer) &&
            doer?.Faction is { } doerFaction && QuestFactionOwnership.IsOwnablePlayerFaction(doerFaction))
            return doerFaction;

        if (ev.args.TryGetArg(HistoryEventArgsNames.Quest, out Quest quest) && quest != null &&
            QuestFactionOwnership.GetOwner(quest) is { } questOwner &&
            QuestFactionOwnership.IsOwnablePlayerFaction(questOwner))
            return questOwner;

        return Faction.OfPlayer;
    }
}

// Ritual development points: the toast should reach only the church's own
// players. Context during ApplyOutcome is already the ritual map's owner;
// keying on PrimaryPlayerFollower also covers a shared ideo performed by a
// secondary follower faction (the primary is the one that can reform).
[HarmonyPatch(typeof(IdeoDevelopmentTracker), nameof(IdeoDevelopmentTracker.TryGainDevelopmentPointsForRitualOutcome))]
static class RitualDevPointsToastScope
{
    static void Prefix(IdeoDevelopmentTracker __instance)
    {
        if (IdeoDevelopmentScope.Active)
            IdeoDevelopmentScope.interestedFaction = IdeoContextUtil.PrimaryPlayerFollower(__instance.ideo);
    }

    static void Finalizer()
    {
        IdeoDevelopmentScope.interestedFaction = null;
    }
}

// The dev-point toast is archived on every client (the archive must stay
// identical) but stays visible only for the interested faction's players
[HarmonyPatch(typeof(Messages), nameof(Messages.Message), typeof(Message), typeof(bool))]
static class DevPointToastOnlyInterestedFaction
{
    static void Postfix(Message msg)
    {
        if (IdeoDevelopmentScope.interestedFaction != null &&
            Multiplayer.RealPlayerFaction != IdeoDevelopmentScope.interestedFaction)
            Messages.liveMessages.Remove(msg);
    }
}

// All three gain paths (ritual, conversion, event) funnel through
// TryAddDevelopmentPoints, which fires the "you can reform" letter when the
// bar fills: pin the ideo's primary follower faction so the existing
// LetterStackReceiveOnlyMyFaction filter routes the letter to that faction
// alone instead of the ambient context's
[HarmonyPatch(typeof(IdeoDevelopmentTracker), nameof(IdeoDevelopmentTracker.TryAddDevelopmentPoints))]
static class ReformLetterToIdeoFaction
{
    static void Prefix(IdeoDevelopmentTracker __instance, ref bool __state)
    {
        if (!IdeoDevelopmentScope.Active)
            return;

        __state = IdeoContextUtil.PushIfOwnable(IdeoContextUtil.PrimaryPlayerFollower(__instance.ideo));
    }

    static void Finalizer(bool __state)
    {
        if (__state)
            FactionExtensions.PopFaction();
    }
}

// The dev-mode "+" button next to the development bar writes
// development.points directly from interface code - a guaranteed desync in
// MP. Reroute the store through a synced (debug-only) command.
[HarmonyPatch(typeof(IdeoUIUtility), nameof(IdeoUIUtility.DoFluidIdeo))]
static class DevPointsPlusButtonSync
{
    private static readonly FieldInfo pointsField =
        AccessTools.Field(typeof(IdeoDevelopmentTracker), nameof(IdeoDevelopmentTracker.points));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> insts)
    {
        foreach (var inst in insts)
        {
            if (inst.opcode == OpCodes.Stfld && inst.operand as FieldInfo == pointsField)
            {
                inst.opcode = OpCodes.Call;
                inst.operand = AccessTools.Method(typeof(DevPointsPlusButtonSync), nameof(StorePoints));
            }

            yield return inst;
        }
    }

    static void StorePoints(IdeoDevelopmentTracker tracker, int value)
    {
        if (Multiplayer.Client == null)
        {
            tracker.points = value;
            return;
        }

        SyncedSetDevPoints(tracker.ideo, value);
    }

    [SyncMethod(debugOnly = true)]
    static void SyncedSetDevPoints(Ideo ideo, int value)
    {
        if (ideo?.development != null)
            ideo.development.points = value;
    }
}
