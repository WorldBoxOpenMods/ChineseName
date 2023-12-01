using NeoModLoader.General.Event.Handlers;
using NeoModLoader.General.Event.Listeners;

namespace Chinese_Name;

public class WarNamePatch : IPatch
{
    class RenameWar : WarStartHandler
    {
        public override void Handle(WarManager pWarManager, War pWar, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pWarType)
        {
            if (!string.IsNullOrEmpty(pWar.data.name)) return;
            if (pDefender != null && pDefender.getAge() <= 1)
            {
                pWarType = WarTypeLibrary.rebellion;
            }
            var generator = CN_NameGeneratorLibrary.Instance.get(pWarType.name_template);
            if (generator == null) return;
            
            var template = generator.GetRandomTemplate();
            var para = template.GetParametersToFill();
            
            pWar.data.name = template.GenerateName(para);
        }
    }
    public void Initialize()
    {
        WarStartListener.RegisterHandler(new RenameWar());
    }
}