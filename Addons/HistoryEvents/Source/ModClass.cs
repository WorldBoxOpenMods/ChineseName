using System.Collections.Generic;
using System.IO;
using Chinese_Name.Naming;
using Chinese_Name.Naming.Game;
using HarmonyLib;
using NeoModLoader.api;

namespace ChineseName.HistoryEvents;

internal sealed class ModClass : BasicMod<ModClass>
{
    protected override void OnModLoad()
    {
        var naming = NamingApi.Current;
        var books = new NamingCategoryExtension("worldbox:book");
        books.Input(new InputField(HistoryEvents.EventName, InputKind.Text, label: "历史事件",
            example: HistoryEvents.Example(HistoryEvents.WarKind)));
        books.Input(new InputField(HistoryEvents.EventKind, InputKind.Text, label: "历史事件类型"));
        books.Presence(HistoryEvents.EventName, "历史事件", "有历史事件", "无历史事件");
        books.Values(HistoryEvents.EventKind, "历史事件类型", new Dictionary<string, string>
        {
            [HistoryEvents.WarKind] = "战争", [HistoryEvents.RebellionKind] = "叛乱",
            [HistoryEvents.MigrationKind] = "迁徙", [HistoryEvents.FoundingKind] = "建城"
        });
        books.InputGlyph("chinesename:historical-event", HistoryEvents.EventCode, HistoryEvents.EventName,
            "历史事件", "ui/Icons/iconBooksWritten", description: "取符合排列条件的战争、叛乱、迁徙或建城记录。");
        books.PrepareExample = HistoryEvents.PrepareExample;
        books.ConditionIcon = HistoryEvents.ConditionIcon;
        naming.ExtendCategory(books);

        var pack = Path.Combine(GetDeclaration().FolderPath, "Content", "history-events.naming.json");
        naming.RegisterDefaultPack(NamingJson.Read<NamingPack>(File.ReadAllText(pack)));
        WorldNamingInputs.RegisterBookNaming("book_name_history", HistoryEvents.GenerateBook, HistoryEvents.UseSavedEvent);

        var harmony = new Harmony(GetDeclaration().UID + ".naming");
        harmony.CreateClassProcessor(typeof(HistoryEvents.ExpansionMigration)).Patch();
        harmony.CreateClassProcessor(typeof(HistoryEvents.MigrantFounding)).Patch();
        harmony.CreateClassProcessor(typeof(HistoryEvents.Immigration)).Patch();
        harmony.CreateClassProcessor(typeof(HistoryEvents.ConflictEnded)).Patch();
    }
}
