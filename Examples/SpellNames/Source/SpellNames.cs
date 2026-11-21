using System;
using System.Collections.Generic;
using System.IO;
using Chinese_Name.Naming;
using NeoModLoader.api;

namespace ChineseNameExample;

public sealed class SpellNames : BasicMod<SpellNames>
{
    public const string TypeId = "spell-example:spell";

    protected override void OnModLoad()
    {
        var naming = NamingApi.Current;
        var packagePath = Path.Combine(GetDeclaration().FolderPath, "Content", "spell.naming.json");
        var defaults = NamingJson.Read<NamingPack>(File.ReadAllText(packagePath));
        var category = new NamingCategory(new NameType(TypeId, "法术（扩展示例）",
            new[]
            {
                new InputField("element", InputKind.Text, required: true, label: "元素", example: "fire"),
                new InputField("form", InputKind.Text, label: "形态", example: "blade"),
                new InputField("form-name", InputKind.Text, label: "形态称谓")
            }, maxLength: 12))
        {
            Defaults = defaults,
            CultureIcon = "ui/Icons/iconFire",
            PrepareContext = inputs =>
            {
                inputs.TryGetValue("form", out var form);
                inputs["form-name"] = Equals(form, "blade") ? "刃" : "印";
                return true;
            }
        };
        category.Values("element", "元素", new Dictionary<string, string> { ["fire"] = "火" });
        category.Values("form", "形态", new Dictionary<string, string> { ["blade"] = "刃", ["seal"] = "印" });
        category.InputGlyph("spell-example:form-name", '\uE260', "form-name", "法术形态", "ui/Icons/iconFire");
        naming.RegisterCategory(category);
    }

    public static NameResult Generate(string element, string form, ulong seed, long? culture = null, long? language = null) =>
        NamingApi.Current.Generate(new NameRequest(TypeId, seed, culture, language,
            new Dictionary<string, object> { ["element"] = element, ["form"] = form }));
}
