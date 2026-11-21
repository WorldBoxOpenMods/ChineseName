using System.IO;
using Chinese_Name.Naming;
using Chinese_Name.Naming.Game;
using HarmonyLib;
using NeoModLoader.api;

namespace ChineseName.CultureExonyms;

internal sealed class ModClass : BasicMod<ModClass>
{
    protected override void OnModLoad()
    {
        var naming = NamingApi.Current;
        var content = Path.Combine(GetDeclaration().FolderPath, "Content");
        naming.RegisterType(ForeignNames.Definition, ReadPack("local-exonyms"));

        var wars = new NamingCategoryExtension("worldbox:war");
        wars.Input(new InputField(ForeignNames.Opponent, InputKind.Text, label: "对方国号外称", example: "松岭邦"));
        wars.Presence(ForeignNames.Opponent, "对方国号外称", "有对方外称", "无对方外称");
        wars.InputGlyph("chinesename:opponent-exonym", '\uE128', ForeignNames.Opponent, "对方国号外称",
            "ui/Icons/iconKingdomList", description: "取命名方文化在长期往来中形成的对方国号外称。");
        naming.ExtendCategory(wars);
        naming.RegisterDefaultPack(ReadPack("war-exonyms"));
        WorldNamingInputs.RegisterPlaceAliases(ForeignNames.ReadAliases);
        WorldNamingInputs.RegisterWarInput(ForeignNames.Opponent, ForeignNames.OpponentName);

        var harmony = new Harmony(GetDeclaration().UID + ".naming");
        harmony.CreateClassProcessor(typeof(ForeignNames.Trade)).Patch();
        harmony.CreateClassProcessor(typeof(ForeignNames.SaveOpponent)).Patch();
        harmony.CreateClassProcessor(typeof(ResidentContact.ChangeResidence)).Patch();
        harmony.CreateClassProcessor(typeof(ResidentContact.YearlyResidence)).Patch();

        NamingPack ReadPack(string name) => NamingJson.Read<NamingPack>(File.ReadAllText(Path.Combine(content, name + ".naming.json")));
    }
}
