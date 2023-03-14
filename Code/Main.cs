using System;
using NCMS;
using UnityEngine;
using ReflectionUtility;
using HarmonyLib;

namespace Chinese_Name
{
    [ModEntry]
    class Main : MonoBehaviour{
        private bool is_chinese = false;
        private bool initialized = false;
        private bool disabled = false;
        internal NameGeneratorLibrary name_generators;
        internal WordLibraries word_libraries;
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
                init_word_libraries();
                load_word_libraries();
                init_name_generator_library();
                load_name_generators();
                patch_funcs();
            }
        }
        private void disable()
        {
            disabled = true;
            initialized = false;
            AllPatch.unpatch_all();
            name_generators = null;
            word_libraries = null;
        }
        private void patch_funcs()
        {
            AllPatch.patch_all();
        }

        private void init_word_libraries()
        {
            word_libraries = new WordLibraries();
        }
        private void load_word_libraries()
        {
            word_libraries.load_default_word_libraries();
            word_libraries.load_mods_word_libraries();
        }
        private void init_name_generator_library()
        {
            name_generators = new NameGeneratorLibrary();
        }
        private void load_name_generators()
        {
            name_generators.load_default_name_generators();
            name_generators.load_mods_name_generators();
        }

    }
}