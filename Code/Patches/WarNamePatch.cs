using System.Collections.Generic;
using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class WarNamePatch : IPatch
{
    public void Initialize()
    {
        WarStartListener.RegisterHandler(new RenameWar());
    }

    class RenameWar : WarStartHandler
    {
        public override void Handle(War pWar, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pWarType)
        {
            if (!string.IsNullOrWhiteSpace(pWar.data.name)) return;
            if (pDefender != null && pDefender.getAge() <= 1)
            {
                pWarType = WarTypeLibrary.rebellion;
            }

            var generator = CN_NameGeneratorLibrary.Instance.get(pWarType.name_template);
            if (generator == null) return;

            var para = new Dictionary<string, string>();
            ParameterGetters.GetWarParameterGetter(generator.parameter_getter)(pWar, para);

            pWar.data.name = generator.GenerateName(para);
        }
    }
}