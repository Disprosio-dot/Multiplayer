using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

// Blood Animations rolls the synced Rand stream inside its effect patches
// behind per-client gates (mod settings, frame-driven fleck updates) and
// spawns filth from the render path - a confirmed desync source. In MP,
// isolate its rand usage and suppress its filth; visuals still play locally.
[StaticConstructorOnStartup]
static class BloodAnimationsCompat
{
    private static int effectDepth;

    static BloodAnimationsCompat()
    {
        if (AccessTools.TypeByName("BloodAnimations.FleckThrownBlood") == null)
            return;

        var targets = new (string typeName, string methodName)[]
        {
            ("BloodAnimations.DamageWorker_AddInjury_ApplyToPawn", "ApplyToPawn"),
            ("BloodAnimations.Pawn_HealthTracker_HealthTick", "HealthTick"),
            ("BloodAnimations.Pawn_Kill", "Kill"),
            ("BloodAnimations.Verb_TryCastNextBurstShot", "TryCastNextBurstShot"),
            ("BloodAnimations.FleckThrownBlood", "TimeInterval"),
        };

        var prefix = new HarmonyMethod(typeof(BloodAnimationsCompat), nameof(EffectPrefix));
        var finalizer = new HarmonyMethod(typeof(BloodAnimationsCompat), nameof(EffectFinalizer));

        foreach (var (typeName, methodName) in targets)
        {
            var method = AccessTools.Method(AccessTools.TypeByName(typeName), methodName);
            if (method == null)
            {
                Log.Warning($"MP: Blood Animations compat found no {typeName}.{methodName} - mod version mismatch? That wrap skipped.");
                continue;
            }

            Multiplayer.harmony.Patch(method, prefix: prefix, finalizer: finalizer);
        }

        var tryMakeFilth = AccessTools.Method(typeof(FilthMaker), nameof(FilthMaker.TryMakeFilth),
            new[] { typeof(IntVec3), typeof(Map), typeof(ThingDef), typeof(int), typeof(FilthSourceFlags), typeof(bool) });
        Multiplayer.harmony.Patch(tryMakeFilth,
            prefix: new HarmonyMethod(typeof(BloodAnimationsCompat), nameof(SuppressFilthPrefix)));

        Log.Message("MP: Blood Animations compat patches applied");
    }

    static void EffectPrefix()
    {
        if (Multiplayer.Client == null) return;
        effectDepth++;
        Rand.PushState();
    }

    // Finalizer, not postfix: an exception mid-effect must not leave the
    // pushed state (or the depth counter) behind
    static void EffectFinalizer()
    {
        if (Multiplayer.Client == null || effectDepth == 0) return;
        Rand.PopState();
        effectDepth--;
    }

    // Filth is simulation state; Blood Animations creates it from per-client
    // gates, so in MP its effect scopes must not spawn any
    static bool SuppressFilthPrefix(ref bool __result)
    {
        if (effectDepth == 0 || Multiplayer.Client == null) return true;
        __result = false;
        return false;
    }
}
