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
