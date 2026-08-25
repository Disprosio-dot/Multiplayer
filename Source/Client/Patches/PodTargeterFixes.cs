using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Client.Patches;

// rwmt#365: two players open the pod destination targeter for the same
// transporters; one confirms and the synced TryLaunch consumes the pods, the
// other is left aiming a world targeter bound to a launched launchable -
// confirming does nothing (or throws) with no explanation: the reported
// soft-lock. Two guards: confirming on a stale launchable closes the targeter
// with a message instead of failing silently, and any executed launch closes
// the local targeter of whoever was aiming those same pods.
static class PodTargeterUtil
{
    public static bool IsStale(CompLaunchable launchable) =>
        launchable?.parent == null ||
        !launchable.parent.Spawned ||
        launchable.Transporter is not { LoadingInProgressOrReadyToLaunch: true };

    // The world targeter holds a closure over the CompLaunchable it was opened
    // for (StartChoosingDestination's lambda); find it by reflection
    public static CompLaunchable TargetedLaunchable()
    {
        var closure = Find.WorldTargeter?.action?.Target;
        if (closure == null)
            return null;

        foreach (var field in closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (field.GetValue(closure) is CompLaunchable launchable)
                return launchable;

        return null;
    }

    public static void CloseStaleTargeter()
    {
        if (Find.WorldTargeter is not { IsTargeting: true } targeter)
            return;

        var aimed = TargetedLaunchable();
        if (aimed != null && IsStale(aimed))
        {
            targeter.StopTargeting();
            Messages.Message("MpPodsAlreadyLaunched".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}

[HarmonyPatch(typeof(CompLaunchable), nameof(CompLaunchable.ChoseWorldTarget))]
static class StaleLaunchConfirmGuard
{
    static bool Prefix(CompLaunchable __instance, ref bool __result)
    {
        if (Multiplayer.Client == null || !PodTargeterUtil.IsStale(__instance))
            return true;

        Messages.Message("MpPodsAlreadyLaunched".Translate(), MessageTypeDefOf.RejectInput, false);
        __result = true; // handled: the targeter closes
        return false;
    }
}

[HarmonyPatch(typeof(CompLaunchable), nameof(CompLaunchable.TryLaunch))]
static class CloseTargetersOnLaunch
{
    // Runs on every client when the synced launch executes: whoever was still
    // aiming these pods gets their local targeter closed instead of stranded
    static void Postfix()
    {
        if (Multiplayer.Client != null)
            PodTargeterUtil.CloseStaleTargeter();
    }
}
