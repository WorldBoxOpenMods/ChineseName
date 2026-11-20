using System;
using Chinese_Name.Naming.Game;
using HarmonyLib;
using NeoModLoader.api;
using NeoModLoader.services;

namespace Chinese_Name;

internal sealed class ModClass : BasicMod<ModClass>
{
    protected override void OnModLoad()
    {
        WorldNamingStore.Initialize(GetDeclaration().FolderPath);
        NativeNamingGlyphs.Register();
        var harmony = new Harmony(GetDeclaration().UID + ".naming");
        Type[] patches =
        {
            typeof(WorldNamingStore.NewWorld), typeof(VanillaNames.ActorNames), typeof(VanillaNames.FirstName),
            typeof(VanillaNames.Birth), typeof(VanillaNames.CopyIdentity), typeof(VanillaNames.MaterializeIdentity),
            typeof(IndependentNames.WorldNames), typeof(KingdomNames.Founding), typeof(AllianceNames.ManagedCreation),
            typeof(AllianceNames.DirectCreation), typeof(AllianceNames.Founders), typeof(AllianceNames.MemberFallback),
            typeof(IndependentNames.WarName),
            typeof(IndependentNames.BookName), typeof(IndependentNames.PlotName), typeof(IndependentNames.ArmyName),
            typeof(IndependentNames.SubspeciesName), typeof(SpecialNames.VoidEvent), typeof(SpecialNames.StartupAngles),
            typeof(IndependentNames.ItemOrigin), typeof(IndependentNames.LegendaryName), typeof(IndependentNames.ReforgeName),
            typeof(IndependentNames.UndeadNames),
            typeof(IndependentNames.KingdomMotto), typeof(IndependentNames.ClanMotto), typeof(IndependentNames.AllianceMotto),
            typeof(NamingLineage.CultureSplit), typeof(NamingLineage.NewLanguage), typeof(NamingLineage.RenameSkeletonSubspecies),
            typeof(NamingLineage.UpliftName), typeof(NativeSurnameEditor.Attach), typeof(OnomasticsEditor.Keyboard),
            typeof(NativeOnomastics.DrawGroup), typeof(NativeOnomastics.BeginArrangementDraw), typeof(NativeNamingTab.ReadDraft), typeof(NativeNamingTab.AttachEditor),
            typeof(NativeNamingTab.DraftPreview), typeof(NativeNamingTab.InvalidatePreview), typeof(NativeNamingTab.CloseWordEditorOnLoad), typeof(NativeNamingTab.WordGroupSelection),
            typeof(NativeNamingTab.BoundGroups), typeof(NativeNamingTab.GlyphPalette), typeof(NativeNamingTab.Categories),
            typeof(NativeNamingTab.ResetGlyphs), typeof(NativeNamingTab.GlyphImport), typeof(NativeNamingTab.PreparePooledGlyph),
            typeof(NativeNamingTab.WordGroupCharacters),
            typeof(OnomasticsEditor.PalettePosition)
        };
        foreach (var patch in patches)
        {
            try { harmony.CreateClassProcessor(patch).Patch(); }
            catch (Exception error)
            {
                LogService.LogWarning("ChineseName could not install " + patch.Name + ". That entry uses vanilla naming.");
                LogService.LogException(error);
            }
        }
    }

    public override void PostInit()
    {
        base.PostInit();
        NamingLineage.ConnectLanguageSplit();
        NativeNamingSettings.Register();
    }
}
