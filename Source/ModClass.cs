using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chinese_Name.Abstract;
using HarmonyLib;
using NeoModLoader;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using NeoModLoader.services;

namespace Chinese_Name;

internal class ModClass : BasicMod<ModClass>, IReloadable
{
    private List<ICanReload> _reloadables = new List<ICanReload>();
    private List<ICanPostInit> _postinits = new List<ICanPostInit>();
    public Dictionary<string, string> GlobalParameters = new();
    public static void LogAllException(Exception e)
    {
        LogService.LogException(e);
        int i = 0;
        while (e.InnerException != null)
        {
            LogService.LogInfo($"Inner Level: {i}");
            LogService.LogException(e.InnerException);
            e = e.InnerException;
        }
    }
    private HashSet<IMod> _loaded_mods = new HashSet<IMod>();
    private void Update()
    {
        foreach (var mod in WorldBoxMod.LoadedMods)
        {
            if (_loaded_mods.Contains(mod)) continue;
            _loaded_mods.Add(mod);
            try
            {
                ChineseNameGeneratorLibrary.Instance.LoadFromMod(mod);
                WordLibraryLibrary.Instance.LoadFromMod(mod);
            }
            catch (Exception e)
            {
                LogAllException(e);
            }
        }
    }

    protected override void OnModLoad()
    {
        AssetManager._instance.add(WordLibraryLibrary.Instance, "Chinese_Name.WordLibraries");
        AssetManager._instance.add(ChineseNameGeneratorLibrary.Instance, "Chinese_Name.ChineseNameGeneratorLibrary");


        var init_types = new List<Type>();
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t =>
                     typeof(ICanInit).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
        {
            init_types.Add(t);
        }
        
        foreach (var t in InitAfterAttribute.Sort(init_types))
        {
            try
            {
                var can_init = (ICanInit)Activator.CreateInstance(t, true);
                can_init.Init();
                if (can_init is ICanReload reloadable)
                {
                    _reloadables.Add(reloadable);
                }

                if (can_init is ICanPostInit post_init)
                {
                    _postinits.Add(post_init);
                }
            }
            catch (Exception e)
            {
                LogAllException(e);
            }
        }

        foreach (var t in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => typeof(IPatch).IsAssignableFrom(t) && !t.IsInterface))
        {
            try
            {
                Harmony.CreateAndPatchAll(t, GetDeclaration().UID);
            }
            catch (Exception e)
            {
                LogAllException(e);
            }
        }
    }

    public override void PostInit()
    {
        base.PostInit();
        string locales_dir = GetLocaleFilesDirectory(GetDeclaration());
        if (Directory.Exists(locales_dir))
        {
            var files = Directory.GetFiles(locales_dir, "*", SearchOption.AllDirectories);
            var csv_separator = ',';

            foreach (var locale_file in files)
            {
                try
                {
                    if (locale_file.EndsWith(".json"))
                    {
                        LM.LoadLocale(Path.GetFileNameWithoutExtension(locale_file), locale_file);
                    }
                    else if (locale_file.EndsWith(".csv"))
                    {
                        LM.LoadLocales(locale_file, csv_separator);
                    }
                }
                catch (FormatException e)
                {
                    LogService.LogWarning(e.Message);
                }
            }

            LM.ApplyLocale(false);
        }

        foreach (var can_post_init in _postinits)
        {
            try
            {
                can_post_init.PostInit();
            }
            catch (Exception e)
            {
                LogAllException(e);
            }
        }
    }
    [Hotfixable]
    public void Reload()
    {
        foreach (var reloadable in _reloadables)
        {
            reloadable.Reload();
        }
    }
    
}