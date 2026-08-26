using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Multiplayer.Client.Factions;

// In-flow editor for a custom ideoligion during multifaction faction creation.
// Deliberately NOT a Page_ConfigureIdeo: the vanilla editor page registers its
// ideo in Find.IdeoManager and makes it Faction.OfPlayer's primary - local,
// unsynced sim mutations (and the IdeoUIUtility list callbacks that look the
// page up via WindowOfType<Page_ConfigureIdeo> do the same). This page reuses
// only the detail pane (IdeoUIUtility.DoIdeoDetails and its dialogs, which
// touch nothing but the ideo they are given) on a detached scratch ideo whose
// ids are interface-local negatives (UniqueIdsPatch); FactionCreator.
// ReconstructCustomIdeo reassigns them deterministically when the synced
// creation command executes.
public class Page_CreateIdeo_Multifaction : Page
{
    public override string PageTitle => "CustomizeIdeoligion".Translate();

    private Ideo ideo;
    private readonly Action<Ideo> done;

    private Vector2 scrollPosition;
    private float viewHeight;

    // Edits run on a clone so Back discards them and the chooser's current
    // custom ideo stays intact until Next delivers the replacement
    public Page_CreateIdeo_Multifaction(Ideo existing, Action<Ideo> done)
    {
        ideo = existing != null ? CloneDetached(existing) : null;
        this.done = done;
        grayOutIfOtherDialogOpen = true;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        if (ideo == null)
        {
            ideo = IdeoUtility.MakeEmptyIdeo();
            // Uncancelable while the ideo has no memes, and chains into the
            // meme picker: guarantees a named, populated ideo before the
            // player gets back to this page
            Find.WindowStack.Add(new Dialog_ChooseMemes(ideo, MemeCategory.Structure, initialSelection: true));
        }
    }

    public override void DoWindowContents(Rect rect)
    {
        DrawPageTitle(rect);
        IdeoUIUtility.DoIdeoDetails(GetMainRect(rect), ideo, ref scrollPosition, ref viewHeight,
            editMode: true,
            ideoLoadedFromFile: loaded => ideo = loaded);
        DoBottomButtons(rect, "DoneButton".Translate(), "RandomizeAll".Translate(), Randomize);
    }

    private void Randomize()
    {
        if (ideo != null && TutorSystem.AllowAction("ConfiguringIdeo"))
        {
            ideo.foundation.Init(new IdeoGenerationParms(IdeoUIUtility.FactionForRandomization(ideo)));
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
    }

    // The vanilla editor's completeness checks, minus its global mutations
    // (SetPrimary, initialPlayerIdeo, Scenario.PostIdeoChosen)
    public override bool CanDoNext()
    {
        if (!base.CanDoNext())
            return false;

        if (ideo == null || ideo.StructureMeme == null || !ideo.memes.Any(m => m.category == MemeCategory.Normal))
        {
            Messages.Message("MessageMustChooseIdeo".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        if (ideo.name.NullOrEmpty())
        {
            Messages.Message("MessageIdeoNameCantBeEmpty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        var incompatible = ideo.FirstIncompatiblePreceptPair();
        if (incompatible != default(Pair<Precept, Precept>))
        {
            Messages.Message("MessageIdeoIncompatiblePrecepts".Translate(
                    incompatible.First.Label.Named("PRECEPT1"), incompatible.Second.Label.Named("PRECEPT2")).CapitalizeFirst(),
                MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        var ritualMissingTarget = ideo.FirstRitualMissingTarget();
        if (ritualMissingTarget != null)
        {
            Messages.Message("MessageRitualMissingTarget".Translate(ritualMissingTarget.Item1.LabelCap.Named("PRECEPT"))
                    + ": " + ritualMissingTarget.Item2.ToCommaList().CapitalizeFirst() + ".",
                MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        var buildingMissingRitual = ideo.FirstConsumableBuildingMissingRitual();
        if (buildingMissingRitual != null)
        {
            Messages.Message("MessageBuildingMissingRitual".Translate(buildingMissingRitual.LabelCap.Named("PRECEPT")),
                MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        return true;
    }

    public override void DoNext()
    {
        done(ideo);
        Close();
    }

    // Scribe round-trip with the same back-reference fixups as
    // FactionCreator.ReconstructCustomIdeo (ids are not reassigned here - the
    // clone keeps its interface-local ones)
    private static Ideo CloneDetached(Ideo source)
    {
        var copy = ScribeUtil.ReadExposable<Ideo>(ScribeUtil.WriteExposable(source));

        foreach (var precept in copy.PreceptsListForReading)
            precept.ideo = copy;
        if (copy.development != null)
            copy.development.ideo = copy;
        copy.style.ideo = copy;

        return copy;
    }
}
