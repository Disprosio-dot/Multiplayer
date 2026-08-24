using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

// A choice letter about to expire gets its timeout extended instead of having
// the default choice forced on the players, up to MaxExtensions times; after
// that the vanilla default fires as before. The decision runs during synced
// ticking on every client that holds the letter, from the same game state, so
// it is identical wherever the letter exists.
public static class LetterTimeoutExtensions
{
    public const int MaxExtensions = 2;
    public const int ExtensionTicks = GenDate.TicksPerDay;

    private static readonly Dictionary<LetterWithTimeout, int> extensions = new();

    public static bool TryExtend(LetterWithTimeout letter)
    {
        extensions.TryGetValue(letter, out var count);
        if (count >= MaxExtensions)
            return false;

        extensions[letter] = count + 1;
        letter.disappearAtTick += ExtensionTicks;

        // CancelFeedbackNotTargetedAtMe already keeps this from showing to
        // players whose faction context isn't concerned
        Messages.Message("MpLetterChoiceExtended".Translate(letter.Label),
            MessageTypeDefOf.NeutralEvent, historical: false);

        return true;
    }

    public static void Forget(Letter letter)
    {
        if (letter is LetterWithTimeout timeout)
            extensions.Remove(timeout);
    }
}

[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.RemoveLetter))]
static class ClearLetterTimeoutExtensions
{
    static void Postfix(Letter let) => LetterTimeoutExtensions.Forget(let);
}

// Timed letters don't auto-open in MP (DontAutoOpenLettersOnTimeout), so a
// choice could expire unseen. Warn the concerned players one day and one hour
// before a letter's timeout instead. Runs in synced ticking on every client
// holding the letter; CancelFeedbackNotTargetedAtMe keeps the message from
// players whose faction isn't concerned. Warnings re-arm naturally after a
// timeout extension pushes the expiry back past a threshold.
[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LetterStackTick))]
static class WarnBeforeLetterTimeout
{
    private static readonly int[] WarningThresholds = { GenDate.TicksPerDay, GenDate.TicksPerHour };

    static void Postfix(LetterStack __instance)
    {
        if (Multiplayer.Client == null || TickPatch.Simulating) return;

        var ticksGame = Find.TickManager.TicksGame;
        for (int i = 0; i < __instance.letters.Count; i++)
        {
            if (__instance.letters[i] is not LetterWithTimeout { TimeoutActive: true } letter)
                continue;

            var remaining = letter.disappearAtTick - ticksGame;
            foreach (var threshold in WarningThresholds)
                if (remaining == threshold)
                    Messages.Message(
                        "MpLetterExpiringSoon".Translate(letter.Label, remaining.ToStringTicksToPeriod()),
                        letter.lookTargets, MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
