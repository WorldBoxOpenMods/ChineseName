using System.Collections.Generic;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CultureNamePatch : IPatch
{
    public void Initialize()
    {
        CultureCreateListener.RegisterHandler(new RenameCulture());
    }

    class RenameCulture : CultureCreateHandler
    {
        private static readonly HashSet<string> vanilla_postfix = new()
        {
            "ak", "an", "ok", "on", "uk", "un"
        };

        public override void Handle(Culture pCulture, Race pRace, City pCity)
        {
            if (!string.IsNullOrWhiteSpace(pCulture.data.name) &&
                !vanilla_postfix.Contains(pCulture.data.name.Trim())) return;
            string name_generator_id = pRace.name_template_culture;
            var asset = CN_NameGeneratorLibrary.Instance.get(name_generator_id);
            if (asset == null) return;

            var para = new Dictionary<string, string>();

            ParameterGetters.GetCultureParameterGetter(asset.parameter_getter)(pCulture, para);
            pCulture.data.name = asset.GenerateName(para);
        }
    }
}