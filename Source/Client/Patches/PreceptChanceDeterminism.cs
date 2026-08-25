using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

// Community-reported: the "usually man's/woman's name" marriage precepts
// desync while always/random don't. Root cause found in vanilla:
// PreceptComp_UnwillingToDo_Chance.MemberWillingToDo rolls Rand.Value as its
// FIRST line - before even filtering by event - so every willingness check of
// ANY event, for any member of an ideo with such a precept, consumes one draw
// from the ambient RNG stream. That draw count is exactly the kind of state
// multiplayer cannot afford to have wander. In MP the roll becomes seeded by
// (doer, event, precept): stream-neutral, identical on every client, and
// semantically it just means a given person consistently is (or isn't) among
// the exceptions - e.g. the 5% who keep their name.
[HarmonyPatch(typeof(PreceptComp_UnwillingToDo_Chance), nameof(PreceptComp_UnwillingToDo_Chance.MemberWillingToDo))]
static class DeterministicChancePrecepts
{
    private delegate bool BaseWillingToDo(PreceptComp_UnwillingToDo self, HistoryEvent ev);

    private static readonly BaseWillingToDo baseWillingToDo =
        AccessTools.MethodDelegate<BaseWillingToDo>(
            AccessTools.Method(typeof(PreceptComp_UnwillingToDo), nameof(PreceptComp_UnwillingToDo.MemberWillingToDo)),
            virtualCall: false);

    static bool Prefix(PreceptComp_UnwillingToDo_Chance __instance, HistoryEvent ev, ref bool __result)
    {
        if (Multiplayer.Client == null)
            return true;

        ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer);

        int seed = Gen.HashCombineInt(
            Gen.HashCombineInt(doer?.thingIDNumber ?? 0, __instance.eventDef?.shortHash ?? 0),
            __instance.preceptDef?.shortHash ?? 0);

        __result = Rand.ValueSeeded(seed) >= __instance.chance || baseWillingToDo(__instance, ev);
        return false;
    }
}
