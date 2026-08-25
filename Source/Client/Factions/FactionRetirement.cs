using System.Linq;
using Multiplayer.Common.Networking.Packet;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions;

// rwmt#796: retiring a lost player faction. Soft delete only - the faction is
// marked defeated (vanilla flag, saved with the game), renamed, hidden from the
// faction UIs and skipped by the per-faction repeaters; its data and history
// stay in the save untouched, so nothing referencing it breaks. Settlements
// must be abandoned first through the vanilla flow - no world surgery here.
// Registered as a host-only SyncMethod in SyncMethods.cs.
public static class FactionRetirement
{
    public static void RetireFaction(Faction faction)
    {
        if (faction == null || faction.defeated) return;
        if (!Multiplayer.GameComp.multifaction) return;

        var worldComp = Multiplayer.WorldComp;
        if (faction == worldComp.spectatorFaction) return;

        bool issuer = TickPatch.currentExecutingCmdIssuedBySelf;

        // Keep at least one active player faction
        if (!Find.FactionManager.AllFactions.Any(f =>
                f.IsPlayer && !f.defeated && f != faction && f != worldComp.spectatorFaction))
        {
            if (issuer)
                Messages.Message("MpCannotRetireLastFaction".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Settlements first: the vanilla abandon flow handles pawns and maps
        if (Find.WorldObjects.Settlements.Any(s => s.Faction == faction))
        {
            if (issuer)
                Messages.Message("MpRetireFactionHasSettlements".Translate(faction.Name), MessageTypeDefOf.RejectInput, false);
            return;
        }

        var oldName = faction.Name;
        faction.defeated = true;
        faction.Name = "MpRetiredFactionName".Translate(oldName);

        // Anyone playing as the retired faction becomes a spectator; each
        // affected client switches itself and announces it, like at creation
        if (Multiplayer.RealPlayerFaction == faction)
        {
            Multiplayer.game.ChangeRealPlayerFaction(worldComp.spectatorFaction);
            Multiplayer.Client.Send(new ClientSetFactionPacket(
                Multiplayer.session.playerId, worldComp.spectatorFaction.loadID));
        }

        Messages.Message("MpFactionRetired".Translate(oldName), MessageTypeDefOf.NeutralEvent, false);
    }
}
