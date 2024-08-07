using System.Collections.Generic;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CityNamePatch : IPatch
{
    public void Initialize()
    {
        CityCreateListener.RegisterHandler(new RenameCity());
    }

    class RenameCity : CityCreateHandler
    {
        public override void Handle(City pCity)
        {
            if (!string.IsNullOrWhiteSpace(pCity.data.name)) return;
            string name_generator_id = pCity.race.name_template_city;
            CN_NameGeneratorAsset asset = CN_NameGeneratorLibrary.Instance.get(name_generator_id);
            if (asset == null) return;

            var para = new Dictionary<string, string>();

            ParameterGetters.GetCityParameterGetter(asset.param_getters)(pCity, para);

            pCity.data.name = asset.GenerateName(para);
        }
    }
}