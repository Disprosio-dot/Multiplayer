using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Client.Factions;

public class Page_ChooseIdeo_Multifaction : Page
{
    public override string PageTitle => "ChooseYourIdeoligion".Translate();

    public Page_ChooseIdeoPreset pageChooseIdeo = new();

    // Custom ideology from a saved .rid file, loaded as a detached object
    // (TryLoadIdeo registers nothing) and serialized into the creation command
    private Ideo customIdeo;

    // Detects preset clicks made inside DrawCategory, to keep preset and
    // custom selections mutually exclusive
    private IdeoPresetDef lastSelectedPreset;

    public override void DoWindowContents(Rect inRect)
    {
        DrawPageTitle(inRect);
        float totalHeight = 0f;
        Rect mainRect = GetMainRect(inRect);
        TaggedString descText = "ChooseYourIdeoligionDesc".Translate();
        float descHeight = Text.CalcHeight(descText, mainRect.width);
        Rect descRect = mainRect;
        descRect.yMin += totalHeight;
        Widgets.Label(descRect, descText);
        totalHeight += descHeight + 10f;

        pageChooseIdeo.DrawStructureAndStyleSelection(inRect);

        // A preset picked after a custom ideo replaces it (and Done in the
        // editor clears the preset): only one source feeds GetIdeologyData
        if (pageChooseIdeo.selectedIdeo != null && pageChooseIdeo.selectedIdeo != lastSelectedPreset)
            customIdeo = null;
        lastSelectedPreset = pageChooseIdeo.selectedIdeo;

        Rect outRect = mainRect;
        outRect.width = 954f;
        outRect.yMin += totalHeight;
        float num3 = (InitialSize.x - 937f) / 2f;
        float buttonX = (inRect.width - Page_ChooseIdeoPreset.ButtonSize.x - 10f - 500f - 16f) / 2f - num3;

        Widgets.BeginScrollView(
            viewRect: new Rect(0f - num3, 0f, 921f, pageChooseIdeo.totalCategoryListHeight + 100f),
            outRect: outRect,
            scrollPosition: ref pageChooseIdeo.leftScrollPosition);

        float curY = 0f;
        pageChooseIdeo.lastCategoryGroupLabel = "";

        // "Custom ideoligions" section, vanilla Page_ChooseIdeoPreset layout
        // minus the Classic row (classic mode is game-wide, not per faction)
        Widgets.Label(new Rect(0f, curY, 300f, Text.LineHeight), "CustomIdeoligions".Translate());
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        Widgets.DrawLineHorizontal(0f, curY + Text.LineHeight + 2f, 901f);
        GUI.color = Color.white;
        curY += 12f;

        var fluidRect = new Rect(buttonX, curY + Text.LineHeight,
            Page_ChooseIdeoPreset.ButtonSize.x, Page_ChooseIdeoPreset.ButtonSize.y);
        DrawCategoryDescription(IdeoPresetCategoryDefOf.Fluid, fluidRect);
        pageChooseIdeo.DrawSelectable(fluidRect, "CreateCustomFluid".Translate(), null, TextAnchor.MiddleCenter,
            customIdeo is { Fluid: true }, true, null, () => OpenEditor(startFluid: true));
        curY = fluidRect.yMax + 10f;

        var fixedRect = new Rect(buttonX, curY + Text.LineHeight,
            Page_ChooseIdeoPreset.ButtonSize.x, Page_ChooseIdeoPreset.ButtonSize.y);
        DrawCategoryDescription(IdeoPresetCategoryDefOf.Custom, fixedRect);
        pageChooseIdeo.DrawSelectable(fixedRect, "CreateCustomFixed".Translate(), null, TextAnchor.MiddleCenter,
            customIdeo is { Fluid: false }, true, null, () => OpenEditor(startFluid: false));

        var loadRect = new Rect(fixedRect.xMax - Page_ChooseIdeoPreset.ButtonSizeSmall.x, fixedRect.yMax + 10f,
            Page_ChooseIdeoPreset.ButtonSizeSmall.x, Page_ChooseIdeoPreset.ButtonSizeSmall.y);
        pageChooseIdeo.DrawSelectable(loadRect, "LoadSaved".Translate() + "...", null, TextAnchor.MiddleCenter,
            false, true, null, OpenCustomIdeoMenu);

        if (customIdeo != null)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(loadRect.xMax + 10f, loadRect.y, 500f, loadRect.height),
                $"{customIdeo.name} ({(customIdeo.Fluid ? "fluid" : "fixed")})");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        curY = loadRect.yMax + 10f;

        foreach (IdeoPresetCategoryDef item in DefDatabase<IdeoPresetCategoryDef>.AllDefsListForReading.Where(c => c != IdeoPresetCategoryDefOf.Classic && c != IdeoPresetCategoryDefOf.Custom && c != IdeoPresetCategoryDefOf.Fluid))
        {
            pageChooseIdeo.DrawCategory(item, ref curY);
        }
        pageChooseIdeo.totalCategoryListHeight = curY;
        Widgets.EndScrollView();

        DoBottomButtons(inRect);
    }

    private static void DrawCategoryDescription(IdeoPresetCategoryDef cat, Rect buttonRect)
    {
        var descRect = new Rect(buttonRect.xMax + 10f, buttonRect.y, 500f, buttonRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(descRect, cat.description);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // Clicking the matching kind re-edits the current custom ideo; the other
    // kind starts fresh (the editor's Done replaces the custom on delivery).
    // Done also advances straight to the next stitched page (pawn config) -
    // bouncing back to this chooser just to click Next again was a dead stop;
    // Back from the pawn page still returns here to change the choice.
    private void OpenEditor(bool startFluid)
    {
        var existing = customIdeo != null && customIdeo.Fluid == startFluid ? customIdeo : null;
        Find.WindowStack.Add(new Page_CreateIdeo_Multifaction(existing, edited =>
        {
            customIdeo = edited;
            pageChooseIdeo.selectedIdeo = null;
            if (CanDoNext())
                DoNext();
        }, startFluid));
    }

    private void OpenCustomIdeoMenu()
    {
        var ideosDir = GenFilePaths.FolderUnderSaveData("Ideos");
        var files = new System.IO.DirectoryInfo(ideosDir).GetFiles("*.rid");

        if (files.Length == 0)
        {
            Messages.Message(
                "No saved ideoligions found. Use 'Create custom ideoligion...' to make one here, or save one from the ideoligion editor.",
                MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var options = files
            .Select(f => new FloatMenuOption(System.IO.Path.GetFileNameWithoutExtension(f.Name), () =>
            {
                if (GameDataSaveLoader.TryLoadIdeo(f.FullName, out var loaded))
                    customIdeo = loaded;
                else
                    Messages.Message($"Failed to load {f.Name} - see log.",
                        MessageTypeDefOf.RejectInput, historical: false);
            }))
            .Append(new FloatMenuOption("Clear custom ideoligion", () => customIdeo = null))
            .ToList();

        Find.WindowStack.Add(new FloatMenu(options));
    }

    public override bool CanDoNext()
    {
        if (customIdeo == null && pageChooseIdeo.selectedIdeo == null)
        {
            Messages.Message("Please select a preset or load a custom ideoligion.", MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        return base.CanDoNext();
    }

    public IdeologyData GetIdeologyData()
    {
        return new IdeologyData(
            pageChooseIdeo.selectedIdeo,
            pageChooseIdeo.selectedStructure,
            pageChooseIdeo.selectedStyles,
            customIdeo != null ? ScribeUtil.WriteExposable(customIdeo) : null);
    }
}

public record IdeologyData(
    IdeoPresetDef SelectedIdeo = null,
    MemeDef SelectedStructure = null,
    List<StyleCategoryDef> SelectedStyles = null,
    byte[] CustomIdeoData = null
) : ISyncSimple;
