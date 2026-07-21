using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Multiplayer.Client.Patches;

// Turn It On and Off keeps all of its state per client and unsaved (building
// sets, rescan countdowns, power values from local settings), so host and
// joiner drift apart and fork rand in PowerNet's battery shuffle. Under MP:
// suppress the mod's own long-event-timed MapLoaded clear, clear its state on
// every client after every MP (re)load instead (its count-mismatch check then
// full-rescans next tick, identically everywhere), and carry the host's
// settings via the game comp so every client runs the same values.
// Reflection-gated: mod absent = no-op; stands down if MpCompat patches it.
[StaticConstructorOnStartup]
static class TIOAOCompat
{
    private static Type modType;
    private static Type settingsType;

    public static bool Active => modType != null;

    static TIOAOCompat()
    {
        modType = AccessTools.TypeByName("TurnItOnandOff.TurnItOnandOff");
        if (modType == null)
            return;

        settingsType = AccessTools.TypeByName("TurnItOnandOff.ModSettings");
        if (settingsType == null)
        {
            Log.Warning("MP: TIOAO compat found the mod but not its ModSettings - mod version mismatch? Compat disabled.");
            modType = null;
            return;
        }

        if (ModsConfig.IsActive("rwmt.multiplayercompatibility") &&
            AccessTools.TypeByName("Multiplayer.Compat.TurnItOnandOff") != null)
        {
            Log.Message("MP: TIOAO compat skipped - Multiplayer Compatibility covers the mod itself");
            modType = null;
            return;
        }

        Multiplayer.harmony.Patch(
            AccessTools.Method(modType, "MapLoaded"),
            prefix: new HarmonyMethod(typeof(TIOAOCompat), nameof(SkipModOwnClear))
        );

        Log.Message("MP: TIOAO compat active - deterministic resets + host settings");
    }

    // In MP the compat owns every reset point; the mod's own long-event-timed
    // clear is the asymmetric one.
    private static bool SkipModOwnClear() => Multiplayer.Client == null;

    // Called from SaveAndReloadCore before the game is written: the first
    // join-point snapshot must already carry the host's settings.
    public static void BeforeMpSave()
    {
        if (!Active || Multiplayer.LocalServer == null || Multiplayer.GameComp == null)
            return;
        if (Multiplayer.GameComp.tioaoHostSettings == null)
            Multiplayer.GameComp.tioaoHostSettings = CaptureSettings();
    }

    // Called from LoadInMainThread after every MP (re)load, host reloads and
    // joiner loads alike: apply host settings, then reset the mod's state so
    // every client rebuilds from the same blank slate on the next tick.
    public static void AfterMpLoad()
    {
        if (!Active || Multiplayer.Client == null)
            return;

        try
        {
            if (Multiplayer.GameComp?.tioaoHostSettings is { } blob)
                ApplySettings(blob);

            var singleton = AccessTools.Field(modType, "singleton").GetValue(null);
            if (singleton != null)
                AccessTools.Method(modType, "Clear").Invoke(singleton, null);
        }
        catch (Exception e)
        {
            Log.Warning($"MP: TIOAO compat reset failed - mod version mismatch? ({e.Message})");
        }
    }

    private static string CaptureSettings()
    {
        string Defs(string field) =>
            string.Join(",", (List<string>)AccessTools.Field(settingsType, field).GetValue(null) ?? new List<string>());

        return string.Join(";",
            ((float)AccessTools.Field(settingsType, "IdlePowerUsage").GetValue(null)).ToString(CultureInfo.InvariantCulture),
            ((float)AccessTools.Field(settingsType, "ActivePowerMultiplier").GetValue(null)).ToString(CultureInfo.InvariantCulture),
            ((int)AccessTools.Field(settingsType, "RescanPeriod").GetValue(null)).ToString(CultureInfo.InvariantCulture),
            Defs("WhitelistedDefs"),
            Defs("BlacklistedDefs"),
            Defs("ReservableDefs")
        );
    }

    private static void ApplySettings(string blob)
    {
        var parts = blob.Split(';');
        if (parts.Length != 6)
        {
            Log.Warning($"MP: TIOAO compat host settings malformed ({parts.Length} fields) - local settings stay");
            return;
        }

        List<string> Defs(string csv) =>
            csv.Length == 0 ? new List<string>() : csv.Split(',').ToList();

        AccessTools.Field(settingsType, "IdlePowerUsage").SetValue(null, float.Parse(parts[0], CultureInfo.InvariantCulture));
        AccessTools.Field(settingsType, "ActivePowerMultiplier").SetValue(null, float.Parse(parts[1], CultureInfo.InvariantCulture));
        AccessTools.Field(settingsType, "RescanPeriod").SetValue(null, int.Parse(parts[2], CultureInfo.InvariantCulture));
        AccessTools.Field(settingsType, "WhitelistedDefs").SetValue(null, Defs(parts[3]));
        AccessTools.Field(settingsType, "BlacklistedDefs").SetValue(null, Defs(parts[4]));
        AccessTools.Field(settingsType, "ReservableDefs").SetValue(null, Defs(parts[5]));

        // Rebuild the derived per-def power table from the applied values
        var singleton = AccessTools.Field(modType, "singleton").GetValue(null);
        if (singleton != null)
            AccessTools.Method(modType, "initPowerValues", Type.EmptyTypes)
                ?.Invoke(singleton, null);
    }
}
