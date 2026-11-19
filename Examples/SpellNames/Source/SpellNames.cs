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
        naming.RegisterProvider("spell-example:form", request =>
        {
            request.Inputs.TryGetValue("form", out var form);
            bool blade = string.Equals(form as string, "blade", StringComparison.Ordinal);
            return new[] { new Word { Id = blade ? "spell-example:blade" : "spell-example:seal", Text = blade ? "刃" : "印" } };
        });
        naming.RegisterType(new NameType(TypeId, "法术（扩展示例）",
            new[]
            {
                new InputField("element", InputKind.Text, required: true, label: "元素"),
                new InputField("form", InputKind.Text, label: "形态")
            }, maxLength: 12), defaults);
    }

    public static NameResult Generate(string element, string form, ulong seed, long? culture = null, long? language = null) =>
        NamingApi.Current.Generate(new NameRequest(TypeId, seed, culture, language,
            new Dictionary<string, object> { ["element"] = element, ["form"] = form }));
}
