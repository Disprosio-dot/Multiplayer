using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions;

// In multifaction games a quest's dialog choices belong to the quest's owner
// faction: everyone can read an open dialog, only the owner's players can pick
// an option. The gate itself lives in NodeTreeDialogSync.Prefix (Patches.cs) so
// a blocked click can't still be synced by that prefix; here we only track
// which faction owns the letter dialog currently open on this client.
public static class QuestDialogOwnership
{
    // UI-side state, never synced: the owner of the letter dialog open locally
    public static Faction currentDialogOwner;
}

[HarmonyPatch(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter))]
static class TrackQuestLetterDialogOwner
{
    static void Postfix(ChoiceLetter __instance)
    {
        QuestDialogOwnership.currentDialogOwner = null;

        if (Multiplayer.Client == null || !Multiplayer.GameComp.multifaction)
            return;
        if (__instance.quest == null)
            return;

        QuestDialogOwnership.currentDialogOwner = QuestFactionOwnership.GetOwner(__instance.quest);
    }
}

[HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.PostClose))]
static class ClearQuestLetterDialogOwner
{
    static void Postfix() => QuestDialogOwnership.currentDialogOwner = null;
}
