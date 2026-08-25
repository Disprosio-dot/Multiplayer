using System.Linq;
using Multiplayer.Common.Networking.Packet;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Client.Factions;

// rwmt#796: retiring a lost player faction. Everything visible of the faction
// is removed - settlements (vanilla abandon flow, which the mod already hooks
// for cache cleanup), other world objects, spawned pawns and caravans - then
// the faction is marked defeated (vanilla flag, saved), renamed, hidden from
// the faction UIs, skipped by the per-faction repeaters and no longer ownable
// for new quests. Its identity and history stay in the save so references
// don't break. Players of the faction become spectators. All of it runs
// inside the synced command, so the teardown is deterministic on every
// client. Registered as a host-only SyncMethod in SyncMethods.cs.
public static class FactionRetirement
{
    public static void RetireFaction(Faction faction)
    {
        if (faction == null || faction.defeated) return;
        if (!Multiplayer.GameComp.multifaction) return;

        var worldComp = Multiplayer.WorldComp;
        if (faction == worldComp.spectatorFaction) return;

        // Keep at least one active player faction
        if (!Find.FactionManager.AllFactions.Any(f =>
                f.IsPlayer && !f.defeated && f != faction && f != worldComp.spectatorFaction))
        {
            if (TickPatch.currentExecutingCmdIssuedBySelf)
                Messages.Message("MpCannotRetireLastFaction".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        var oldName = faction.Name;
        faction.defeated = true;
        faction.Name = "MpRetiredFactionName".Translate(oldName);

        // Players first, so nobody is left viewing a faction being torn down;
        // each affected client switches itself and announces it, like at creation
        if (Multiplayer.RealPlayerFaction == faction)
        {
            Multiplayer.game.ChangeRealPlayerFaction(worldComp.spectatorFaction);
            Multiplayer.Client.Send(new ClientSetFactionPacket(
                Multiplayer.session.playerId, worldComp.spectatorFaction.loadID));
        }

        // Pin the whole teardown to the retired faction's own context, forced,
        // on every client: ChangeRealPlayerFaction above just changed the
        // ambient context on the retired player's client ONLY, and teardown
        // notifications write into per-faction data - Find.History among them
        // (an ancient mech remains despawning flips the datacore flag). With
        // ambient contexts diverging, those writes landed in different
        // factions' histories per client and desynced the storyteller gates
        // (tripwire-confirmed live desync). Pinned to the dead faction, the
        // writes are deterministic and land in data nothing ticks anymore.
        ((Map)null).PushFaction(faction, force: true);
        try
        {
            // Settlements through the vanilla abandon flow (destroys their maps;
            // RemoveMapCacheOnAbandon moves cached quests to the world)
            foreach (var settlement in Find.WorldObjects.Settlements.Where(s => s.Faction == faction).ToList())
                SettlementAbandonUtility.Abandon(settlement);

            // Everything else the faction owns on the world map: caravans,
            // travelling transporters, camps... MapParent.Destroy tears down any
            // map it still holds
            foreach (var obj in Find.WorldObjects.AllWorldObjects.Where(o => o.Faction == faction).ToList())
                if (!obj.Destroyed)
                    obj.Destroy();

            // Stragglers of the faction spawned on other maps (visitors, guests)
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.PawnsInFaction(faction).ToList())
                    if (!pawn.Destroyed)
                        pawn.Destroy();
        }
        finally
        {
            FactionExtensions.PopFaction();
        }

        Messages.Message("MpFactionRetired".Translate(oldName), MessageTypeDefOf.NeutralEvent, false);
    }
}
