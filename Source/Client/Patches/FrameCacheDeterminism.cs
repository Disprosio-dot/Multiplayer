using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Client.Patches;

// PlayerItemAccessibilityUtility caches its accessible-things scan keyed on
// (tile, RealTime.frameCount). Frame counts are per-client machine state: one
// client serves a list computed from older sim state while another recomputes
// fresh. Reachable from the synced sim via quest generation
// (QuestNode_Root_Beggars, QuestNode_TradeRequest_GetRequestedThing) and
// GameComponent_OnetimeNotification's world tick, where a flipped result
// consumes extra synced RNG (RandomNonHostileFaction) - a rand-state desync
// on stable MP, no multifaction needed. In MP the cache is invalidated per
// call: recompute from current synced state, identical on every client. Call
// sites are rare (quest gen; a 2000-tick 5%-gated component), so the rescan
// cost is negligible.
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), nameof(PlayerItemAccessibilityUtility.CacheAccessibleThings))]
static class ItemAccessibilityCacheInvalidation
{
    static void Prefix()
    {
        // Frame key, not the tile: PlanetTile.Invalid is a legitimate nearTile
        // for map-less callers and would re-match; frameCount is never -1
        if (Multiplayer.Client != null)
            PlayerItemAccessibilityUtility.cachedAccessibleThingsForFrame = -1;
    }
}
