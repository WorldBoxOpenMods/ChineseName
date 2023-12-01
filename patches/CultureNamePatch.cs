using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CultureNamePatch : IPatch
{
    class RenameCulture : CultureCreateHandler
    {
        public override void Handle(Culture pCulture, Race pRace, City pCity)
        {
            if (!string.IsNullOrEmpty(pCulture.data.name)) return;
            string name_generator_id = pRace.name_template_culture;
            var asset = CN_NameGeneratorLibrary.Instance.get(name_generator_id);
            if (asset == null) return;
            var template = asset.GetRandomTemplate();
            var para = template.GetParametersToFill();
            pCulture.data.name = template.GenerateName(para);
        }
    }
    public void Initialize()
    {
        CultureCreateListener.RegisterHandler(new RenameCulture());
    }
}