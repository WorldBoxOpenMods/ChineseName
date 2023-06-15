using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using NCMS;

namespace Chinese_Name
{
    public class WordLibraries
    {
        public Dictionary<string, WordLibrary> libraries = new Dictionary<string, WordLibrary>();
        
        public void unzip_word_libraries()
        {
            if (Directory.Exists(Main.path_to_word_libraries)) return;
            //WorldBoxConsole.Console.print(Main.path_to_word_libraries);
            Directory.CreateDirectory(Main.path_to_word_libraries);
            ZipFile.ExtractToDirectory(Main.zipped_word_libraries_path, Main.path_to_word_libraries);


            if (Directory.Exists(Main.path_to_word_libraries)) return;

            /* If unzip fails */
            Main.curr_path_to_word_libraries = Main.path_to_tmp_word_libraries;
            if (Directory.Exists(Main.path_to_tmp_word_libraries)) 
            {
                Directory.Delete(Main.path_to_tmp_word_libraries, true);
            }

            Directory.CreateDirectory(Main.path_to_tmp_word_libraries);
            ZipFile.ExtractToDirectory(Main.zipped_word_libraries_path, Main.path_to_tmp_word_libraries);
        }
        public void load_default_word_libraries()
        {
            load_word_libraries_from(Main.curr_path_to_word_libraries);
        }
        private void load_word_libraries_from(string path)
        {
            string[] files = System.IO.Directory.GetFiles(path);
            Main.warn($"WORD LIBRARIES at ({path}):" + files.Length.ToString());
            foreach (string file_path in files)
            {
                //Main.warn(file_path);
                string[] paths = file_path.Replace("\\", "/").Split('/');
                libraries[paths[paths.Length - 1].Replace(".txt", "")] = new WordLibrary(load_list_str(file_path));
            }
        }
        public void load_mods_word_libraries()
        {
            if(!Directory.Exists(Main.path_to_tmp_word_libraries))
            {
                Directory.CreateDirectory(Main.path_to_tmp_word_libraries);
            }
            foreach(NCMod mod in NCMS.ModLoader.Mods)
            {
                if (mod.name == Mod.Info.Name) continue;

                string folder_name = Hash128.Compute(mod.name).ToString();
                string path = Path.Combine(mod.path, "word_libraries.zip");
                string target_path = Path.Combine(Main.path_to_tmp_word_libraries, folder_name);

                if (File.Exists(path))
                {
                    if(!Directory.Exists(target_path)) Directory.CreateDirectory(target_path);
                    ZipFile.ExtractToDirectory(path, target_path);

                    load_word_libraries_from(target_path);
                }
            }
        }
        public WordLibrary get(string id)
        {
            WordLibrary ret;
            if (libraries.TryGetValue(id, out ret)) return ret;
            return null;
        }
        public static List<string> load_list_str(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            List<string> ret = new List<string>();
            StreamReader sr = new StreamReader(fs);
            sr.BaseStream.Seek(0, SeekOrigin.Begin);
            string tmp = sr.ReadLine();
            while (tmp != null)
            {
                ret.Add(tmp);
                tmp = sr.ReadLine();
            }
            sr.Close();
            fs.Close();
            return ret;
        }
    }
}
