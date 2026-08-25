using Verse;

namespace Multiplayer.Client;

// rwmt#641, root cause: saving while a gravship is travelling produces a
// corrupted save - the gravship world object and its crew end up referenced
// but not deep-saved ("Object with load ID ... is not deep-saved"), and
// loading that file loses the ship and crew, locks the camera in space and
// desyncs. Singleplayer simply cannot save during the transit cutscene; in MP
// the autosaver and join-point snapshots fired regardless. All saving is
// blocked while a gravship is travelling and retried after it lands.
// IsGravshipTravelling is simulation state, so every client skips and
// retries at the same ticks.
public static class GravshipSaveGuard
{
    public static bool SavingBlocked =>
        ModsConfig.OdysseyActive &&
        Find.GravshipController is { IsGravshipTravelling: true };
}
