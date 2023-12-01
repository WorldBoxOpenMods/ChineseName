using System.Collections.Generic;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class CityNamePatch : IPatch
{
    class RenameCity : CityCreateHandler
    {
        public override void Handle(City pCity)
        {
            if(!string.IsNullOrEmpty(pCity.data.name)) return;
            string name_generator_id = pCity.race.name_template_city;
            CN_NameGeneratorAsset asset = CN_NameGeneratorLibrary.Instance.get(name_generator_id);
            if(asset == null) return;
            
            CN_NameTemplate template = asset.GetRandomTemplate();
            
            Dictionary<string, string> para = template.GetParametersToFill();
            
            
            
            pCity.data.name = template.GenerateName(para);
        }
    }

    public void Initialize()
    {
        CityCreateListener.RegisterHandler(new RenameCity());
    }
}