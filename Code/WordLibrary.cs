using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    public class WordLibrary
    {
        public List<string> words;
        public WordLibrary(List<string> words)
        {
            this.words = words;
        }

        public string get_random()
        {
            return this.words.GetRandom();
        }
    }
}
