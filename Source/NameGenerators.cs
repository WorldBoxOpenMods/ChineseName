using Chinese_Name.Abstract;
using strings;

namespace Chinese_Name;

public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
{
    public static NameGeneratorAsset EasternUnit { get; private set; }
    public static NameGeneratorAsset EasternCity { get; private set; }
    public static NameGeneratorAsset EasternKingdom { get; private set; }
    public static NameGeneratorAsset EasternCulture { get; private set; }
    public static NameGeneratorAsset EasternClan { get; private set; }
    public static NameGeneratorAsset EasternFamily { get; private set; }
    public static NameGeneratorAsset EasternLanguage { get; private set; }
    public static NameGeneratorAsset EasternReligion { get; private set; }
    
    public static NameGeneratorAsset WesternUnit { get; private set; }
    public static NameGeneratorAsset WesternCity { get; private set; }
    public static NameGeneratorAsset WesternKingdom { get; private set; }
    public static NameGeneratorAsset WesternCulture { get; private set; }
    public static NameGeneratorAsset WesternClan { get; private set; }
    public static NameGeneratorAsset WesternFamily { get; private set; }
    public static NameGeneratorAsset WesternLanguage { get; private set; }
    public static NameGeneratorAsset WesternReligion { get; private set; }
    
    protected override void OnInit()
    {
        RegisterAssets();
        
        EasternUnit.addOnomastic("0Lo,1L,2L|0:百家姓;1:千字文;2:千字文");
        EasternCity.addOnomastic("0L|0:真实城名");
        EasternKingdom.addOnomastic("0L|0:真实国名");
        EasternCulture.addOnomastic("d1|1:文化");
        EasternClan.addOnomastic("de0|0:家");
        EasternFamily.addOnomastic("de0|0:家");
        EasternLanguage.addOnomastic("0L,1|0:千字文;1:语");
        EasternReligion.addOnomastic("0L,1|0:千字文;1:教");
        
        WesternUnit.addOnomastic("0L,_1fL,_2Lo|0:西方名字;1:西方名字中缀;2:西方姓氏");
        WesternCity.addOnomastic("e0|0:城");
        WesternKingdom.addOnomastic("e|");
        WesternCulture.addOnomastic("0L,1|0:西方名字;1:文化");
        WesternClan.addOnomastic("e0|0:家族");
        WesternFamily.addOnomastic("e0|0:家族");
        WesternLanguage.addOnomastic("0L,1|0:西方名字;1:语");
        WesternReligion.addOnomastic("0L,1|0:西方名字;1:教");

        ReplaceOnomastics("human_unit", EasternUnit);
        ReplaceOnomastics("sino_unit", EasternUnit);
        ReplaceOnomastics("nihon_unit", EasternUnit);
        ReplaceOnomastics("siam_unit", EasternUnit);
        ReplaceOnomastics("vishna_unit", EasternUnit);

        ReplaceOnomastics("orc_unit", WesternUnit);
        ReplaceOnomastics("dwarf_unit", WesternUnit);
        ReplaceOnomastics("elf_unit", WesternUnit);
        ReplaceOnomastics("nordic_unit", WesternUnit);
        ReplaceOnomastics("folk_unit", WesternUnit);
        ReplaceOnomastics("frankish_unit", WesternUnit);
        ReplaceOnomastics("germanic_unit", WesternUnit);
        ReplaceOnomastics("iberian_unit", WesternUnit);
        ReplaceOnomastics("monolux_unit", WesternUnit);
        ReplaceOnomastics("nomad_unit", WesternUnit);
        ReplaceOnomastics("poly_unit", WesternUnit);
        ReplaceOnomastics("pomeranian_unit", WesternUnit);
        ReplaceOnomastics("posh_unit", WesternUnit);
        ReplaceOnomastics("rome_unit", WesternUnit);
        ReplaceOnomastics("rus_unit", WesternUnit);
        ReplaceOnomastics("slavic_unit", WesternUnit);

        ReplaceManyOnomastics(EasternCity, "sino_city", "nihon_city", "siam_city", "vishna_city");
        ReplaceManyOnomastics(WesternCity,
            "folk_city", "frankish_city", "germanic_city", "hyena_city", "iberian_city", "monolux_city",
            "nomad_city", "nordic_city", "poly_city", "pomeranian_city", "posh_city", "rome_city",
            "rus_city", "slavic_city", "snake_city");

        ReplaceManyOnomastics(EasternKingdom, "sino_kingdom", "nihon_kingdom", "siam_kingdom", "vishna_kingdom");
        ReplaceManyOnomastics(WesternKingdom,
            "folk_kingdom", "frankish_kingdom", "germanic_kingdom", "iberian_kingdom", "monolux_kingdom",
            "nomad_kingdom", "nordic_kingdom", "poly_kingdom", "pomeranian_kingdom", "posh_kingdom",
            "rome_kingdom", "rus_kingdom", "slavic_kingdom");

        ReplaceManyOnomastics(EasternClan, "sino_clan", "nihon_clan", "siam_clan", "vishna_clan");
        ReplaceManyOnomastics(WesternClan,
            "folk_clan", "frankish_clan", "germanic_clan", "iberian_clan", "monolux_clan",
            "nomad_clan", "nordic_clan", "poly_clan", "pomeranian_clan", "posh_clan",
            "rome_clan", "rus_clan", "slavic_clan");

        ReplaceOnomastics("vishna_family", EasternFamily);

        ReplaceManyOnomastics(EasternLanguage, "human_language");
        ReplaceManyOnomastics(WesternLanguage, "orc_language", "dwarf_language", "elf_language");

        ReplaceManyOnomastics(WesternReligion, "orc_religion", "dwarf_religion", "elf_religion");
    }

    private void ReplaceManyOnomastics(NameGeneratorAsset templateSource, params string[] assetIds)
    {
        foreach (var assetId in assetIds)
        {
            ReplaceOnomastics(assetId, templateSource);
        }
    }

    private void ReplaceOnomastics(string assetId, NameGeneratorAsset templateSource)
    {
        var asset = Get(assetId);
        if (asset == null || templateSource == null)
        {
            return;
        }

        asset.onomastics_templates.Clear();
        foreach (var template in templateSource.onomastics_templates)
        {
            asset.addOnomastic(template);
        }
    }
}
