using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Chinese_Name.ui;
using Chinese_Name.utils;
using HarmonyLib;
using NeoModLoader.api;
using NeoModLoader.General;
using NeoModLoader.utils;
using UnityEngine;

namespace Chinese_Name
{
    class ModClass : BasicMod<ModClass>, IReloadable
    {
        internal Dictionary<string, string> GlobalParameters = new();

        internal List<CN_PackageMeta> WorkingPackages = new();
        private void Update()
        {
            foreach (var getter in ParameterGetters.global_parameter_getters)
            {
                getter(GlobalParameters);
            }
        }

        public void Reload()
        {
            WordLibraryManager.Instance.Reload();
            CN_NameGeneratorLibrary.Instance.Reload();
        }
        protected override void OnModLoad()
        {
            Config.isEditor = true;
            var h = new Harmony("com.github.neo-modloader.chinese-name");
            h.Patch(AccessTools.Method(typeof(NameGenerator), nameof(NameGenerator.generateNameFromTemplate), new Type[] { typeof(NameGeneratorAsset), typeof(ActorBase), typeof(Kingdom) }), transpiler: new HarmonyMethod(AccessTools.Method(typeof(NameGeneratorReplaceUtils), nameof(NameGeneratorReplaceUtils.replace_name_generate_transpiler))));

            CN_NameGeneratorLibrary.Instance = new();
            WordLibraryManager.Instance = new();

            CN_PackageManager.FindAllPackagesToLoad();
            CN_PackageManager.LoadAllPackages();

            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            // 虽然可以直接patch getName和getNameFromTemplate, 但那样无法获取更多的参数
            foreach (Type type in types)
            {
                if (type.GetInterface(nameof(IPatch)) != null)
                {
                    try
                    {
                        IPatch patch = (IPatch)type.GetConstructor(new Type[] { }).Invoke(new object[] { });
                        patch.Initialize();
                    }
                    catch (Exception e)
                    {
                        LogWarning("Failed to initialize patch: " + type.Name);
                        LogWarning(e.ToString());
                    }
                }
            }

            CN_PackageSelectWindow.CreateAndInit(nameof(CN_PackageSelectWindow));
            PowerButtonCreator.CreateWindowButton(nameof(CN_PackageSelectWindow), nameof(CN_PackageSelectWindow),
                                                  SpriteLoadUtils.LoadSingleSprite(
                                                      GeneralUtils.CombinePath(GetDeclaration().FolderPath,
                                                                   GetDeclaration().IconPath)),
                                                  PowerButtonCreator.GetTab(PowerTabNames.Main).transform,
                                                  new Vector2(248.4f, -18));
        }
    }
}