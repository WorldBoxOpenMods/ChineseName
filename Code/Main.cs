using System;
using NCMS;
using UnityEngine;
using ReflectionUtility;
using HarmonyLib;
using UnityEngine.UI;
using UnityEngine.Events;

/**
public class Mod
{
    public static ModDeclaration.Info Info;
    public static GameObject GameObject;
}
*/
namespace Chinese_Name
{
    [ModEntry]
    public class Main : MonoBehaviour{
        public bool is_chinese = false;
        public bool initialized = false;
        public bool disabled = false;
        public static Main instance;
        public NameGeneratorLibrary name_generators;
        public WordLibraries word_libraries;
        public const string family_name = "chinese_family_name";
        public static readonly string zipped_word_libraries_path = Mod.Info.Path + "/word_libraries.zip";
        public static readonly string zipped_name_generators_path = Mod.Info.Path + "/name_generators.zip";
        public static readonly string path_to_word_libraries = Application.streamingAssetsPath + "/mods/ChineseName/word_libraries";
        public static readonly string path_to_name_generators = Application.streamingAssetsPath + "/mods/ChineseName/name_generators";
        public static void warn(string str)
        {
            UnityEngine.Debug.LogWarning(str);
        }
        public static void log(string str)
        {
            UnityEngine.Debug.Log(str);
        }
        void Update()
        {
            if (!initialized && !disabled)
            {
                initialized = true;
                is_chinese = LocalizedTextManager.instance.language == "cz";
                if (!is_chinese)
                {
                    initialized = false;
                    return;
                }
                instance = this;
                init_word_libraries();
                load_word_libraries();
                init_name_generator_library();
                load_name_generators();
                patch_funcs();
            }
        }
        public void disable()
        {
            disabled = true;
            initialized = false;
            AllPatch.unpatch_all();
            name_generators = null;
            word_libraries = null;
        }
        public void patch_funcs()
        {
            AllPatch.patch_all();
        }

        public void init_word_libraries()
        {
            word_libraries = new WordLibraries();
        }
        public void load_word_libraries()
        {
            word_libraries.unzip_word_libraries();
            word_libraries.load_default_word_libraries();
            //word_libraries.load_mods_word_libraries();
        }
        public void init_name_generator_library()
        {
            name_generators = new NameGeneratorLibrary();
        }
        public void load_name_generators()
        {
            name_generators.unzip_name_generators();
            name_generators.load_default_name_generators();
            //name_generators.load_mods_name_generators();
        }

    }
}