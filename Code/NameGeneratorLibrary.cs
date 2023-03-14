using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    internal class NameGeneratorLibrary
    {
        private Dictionary<string, NameGenerator> generators = new Dictionary<string, NameGenerator>();
        public void unzip_name_generators()
        {
            throw new NotImplementedException();
        }
        public void load_default_name_generators()
        {
            throw new NotImplementedException();
        }
        public void load_mods_name_generators()
        {
            throw new NotImplementedException();
        }
        public NameGenerator get(string id)
        {
            NameGenerator ret;
            if (generators.TryGetValue(id, out ret)) return ret;
            return null;
        }
    }
}
