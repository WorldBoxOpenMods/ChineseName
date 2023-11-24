using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NCMS;
using Newtonsoft;
using UnityEngine;

namespace Chinese_Name
{
    public class NameGeneratorLibrary
    {
        public Dictionary<string, NameGenerator> generators = new Dictionary<string, NameGenerator>();
        public Dictionary<string, name_generate_func> generate_funcs = new Dictionary<string, name_generate_func>();
        public NameGeneratorLibrary()
        {
            generate_funcs[nameof(NameGenerateFuncs.default_generator)] = NameGenerateFuncs.default_generator;
            generate_funcs[nameof(NameGenerateFuncs.unit_name_generator)] = NameGenerateFuncs.unit_name_generator;
            generate_funcs[nameof(NameGenerateFuncs.clan_name_generator)] = NameGenerateFuncs.clan_name_generator;
            generate_funcs[nameof(NameGenerateFuncs.war_name_generator)] = NameGenerateFuncs.war_name_generator;
            generate_funcs[nameof(NameGenerateFuncs.alliance_name_generator)] = NameGenerateFuncs.alliance_name_generator;
        }
        public void unzip_name_generators()
        {
            if (Directory.Exists(Main.path_to_name_generators)) return;
            WorldBoxConsole.Console.print(Main.path_to_name_generators);
            Directory.CreateDirectory(Main.path_to_name_generators);
            ZipFile.ExtractToDirectory(Main.zipped_name_generators_path, Main.path_to_name_generators);

            if (Directory.Exists(Main.path_to_name_generators)) return;
            /* If unzip fails */
            Main.curr_path_to_name_generators = Main.path_to_tmp_name_generators;
            if (Directory.Exists(Main.path_to_tmp_name_generators))
            {
                Directory.Delete(Main.path_to_tmp_name_generators, true);
            }

            Directory.CreateDirectory(Main.path_to_tmp_name_generators);
            ZipFile.ExtractToDirectory(Main.zipped_name_generators_path, Main.path_to_tmp_name_generators);
        }
        private void load_name_generators_from(string path)
        {
            string[] files = System.IO.Directory.GetFiles(path);

            Main.warn($"NAME GENERATORS at ({path}):" + files.Length.ToString());
            foreach (string file_path in files)
            {
                //Main.warn(file_path);
                Dictionary<string, NameGenerator> generators_in_file = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, NameGenerator>>(File.ReadAllText(file_path));
                if (generators_in_file == null) continue;
                foreach (string key in generators_in_file.Keys)
                {
                    generators[key] = generators_in_file[key];
                    generators[key].total_weight = 0;
                    generators[key].generate_func = generate_funcs[generators[key].generate_func_id];
                    foreach (NameGenerateTemplate template in generators[key].templates)
                    {
                        template.finish();
                        generators[key].total_weight += template.weight;
                    }
                    if(!AssetManager.nameGenerator.dict.ContainsKey(key)) 
                    {
                        AssetManager.nameGenerator.clone(key, "human_culture").templates = new List<string>(){"Part_group"};
                    }
                }
            }
        }
        public void load_default_name_generators()
        {
            load_name_generators_from(Main.curr_path_to_name_generators);
        }
        public void load_mods_name_generators()
        {
            if (!Directory.Exists(Main.path_to_tmp_name_generators))
            {
                Directory.CreateDirectory(Main.path_to_tmp_name_generators);
            }
            foreach (NCMod mod in NCMS.ModLoader.Mods)
            {
                if (mod.name == Mod.Info.Name) continue;

                string folder_name = Hash128.Compute(mod.name).ToString();
                    
                string path = Path.Combine(mod.path, "name_generators.zip");
                string target_path = Path.Combine(Main.path_to_tmp_name_generators, folder_name);

                if (File.Exists(path))
                {
                    if (!Directory.Exists(target_path)) Directory.CreateDirectory(target_path);
                    ZipFile.ExtractToDirectory(path, target_path);

                    load_name_generators_from(target_path);
                }
            }
        }
        public NameGenerator get(string id)
        {
            NameGenerator ret;
            if (generators.TryGetValue(id, out ret)) return ret;
            return null;
        }
    }
}
