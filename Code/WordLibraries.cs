using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using UnityEngine;

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
            if (Directory.Exists(Main.path_to_tmp_word_libraries)) return;

            Directory.CreateDirectory(Main.path_to_tmp_word_libraries);
            ZipFile.ExtractToDirectory(Main.zipped_word_libraries_path, Main.path_to_tmp_word_libraries);
        }
        public void load_default_word_libraries()
        {
            string[] files = System.IO.Directory.GetFiles(Main.curr_path_to_word_libraries);
            Main.warn($"WORD LIBRARIES at ({Main.curr_path_to_word_libraries}):"+files.Length.ToString());
            foreach (string file_path in files)
            {
                //Main.warn(file_path);
                string[] paths = file_path.Replace("\\", "/").Split('/');
                libraries[paths[paths.Length - 1].Replace(".txt","")] = new WordLibrary(load_list_str(file_path));
            }
        }
        public void load_mods_word_libraries()
        {
            
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
