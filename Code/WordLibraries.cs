using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    internal class WordLibraries
    {
        private Dictionary<string, WordLibrary> libraries = new Dictionary<string, WordLibrary>();
        public void unzip_word_libraries()
        {
            throw new NotImplementedException();
        }
        public void load_default_word_libraries()
        {
            throw new NotImplementedException();
        }
        public void load_mods_word_libraries()
        {
            throw new NotImplementedException();
        }
        public WordLibrary get(string id)
        {
            WordLibrary ret;
            if (libraries.TryGetValue(id, out ret)) return ret;
            return null;
        }
    }
}
