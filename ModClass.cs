using System;
using System.Reflection;
using NeoModLoader.api;

namespace Chinese_Name
{
    class ModClass : BasicMod<ModClass>, IReloadable
    {
        protected override void OnModLoad()
        {
            WordLibraryManager.Instance.init();
            CN_NameGeneratorLibrary.Instance.init();

            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            // 虽然可以直接patch getName和getNameFromTemplate, 但那样无法获取更多的参数
            foreach (Type type in types)
            {
                if (type.GetInterface(nameof(IPatch))!=null)
                {
                    try
                    {
                        IPatch patch = (IPatch) type.GetConstructor(new Type[] { }).Invoke(new object[] { });
                        patch.Initialize();
                    }
                    catch (Exception e)
                    {
                        LogWarning("Failed to initialize patch: " + type.Name);
                        LogWarning(e.ToString());
                    }
                }
            }
        }
        
        public void Reload()
        {
            WordLibraryManager.Instance.Reload();
            CN_NameGeneratorLibrary.Instance.Reload();
        }
    }
}