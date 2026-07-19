using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Client.Patches;

// Vanilla Nutrient Paste Expanded's Extract and Drain gizmo actions mutate
// simulation state (spawn meals, consume feedstock, empty tanks) directly in
// interface context - on the clicking client only - a confirmed desync source.
// Register the gizmo entry points as synced methods; the bodies are
// deterministic and safe no-ops when preconditions fail, so all-client
// re-execution is exactly vanilla behavior.
[StaticConstructorOnStartup]
static class VNPECompat
{
    static VNPECompat()
    {
        var dispenserGizmos = AccessTools.TypeByName("VNPE.Building_NutrientPasteDispenser_GetGizmos");
        if (dispenserGizmos == null)
            return;

        // Multiplayer Compatibility ships its own VNPE gizmo sync; registering
        // twice would double-patch and skew syncIds vs its expectations
        if (ModsConfig.IsActive("rwmt.multiplayercompatibility"))
        {
            Log.Message("MP: VNPE compat skipped - Multiplayer Compatibility is active and covers VNPE itself");
            return;
        }

        // Static TryDropFood(dispenser, amount) backs all three dispenser
        // Extract gizmos and the pipe-network fallback branch
        RegisterMethod(dispenserGizmos, "TryDropFood")?.CancelIfAnyArgNull();

        // The tap overrides gizmos with its own private instance TryDropFood(amount);
        // the instance rides along as the sync target
        RegisterMethod(AccessTools.TypeByName("VNPE.Building_NutrientPasteTap"), "TryDropFood");

        // The Drain gizmo action is a lambda capturing only 'this', hoisted onto
        // the comp class itself - resolvable by parent method + ordinal
        try
        {
            SyncMethod.Lambda(AccessTools.TypeByName("VNPE.CompRegisterIngredients"), "CompGetGizmosExtra", 0);
        }
        catch (Exception e)
        {
            Log.Warning($"MP: VNPE compat found no Drain gizmo lambda - mod version mismatch? That sync skipped. ({e.Message})");
        }

        Log.Message("MP: VNPE compat sync methods registered");
    }

    private static SyncMethod RegisterMethod(Type type, string methodName)
    {
        var method = type != null ? AccessTools.Method(type, methodName) : null;
        if (method == null)
        {
            Log.Warning($"MP: VNPE compat found no {type?.ToString() ?? methodName} sync target - mod version mismatch? That sync skipped.");
            return null;
        }

        return Sync.RegisterSyncMethod(method);
    }
}
