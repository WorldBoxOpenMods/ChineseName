using Chinese_Name.Naming.Game;
using HarmonyLib;
using NeoModLoader.api;

namespace ChineseName.SurnameBranches;

internal sealed class ModClass : BasicMod<ModClass>
{
    protected override void OnModLoad()
    {
        WorldNamingInputs.RegisterSurnameFounding(GetDeclaration().UID, Branches.Pending, Branches.Consume);

        var harmony = new Harmony(GetDeclaration().UID + ".naming");
        harmony.CreateClassProcessor(typeof(Branches.CultureSplit)).Patch();
        harmony.CreateClassProcessor(typeof(Branches.CityFounded)).Patch();
    }
}
