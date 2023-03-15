using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    public delegate string name_generate_func(NameGenerator generator, object[] @params);
    [Serializable]
    public class NameGenerator
    {
        public List<NameGenerateTemplate> templates = new List<NameGenerateTemplate>();
        public string generate_func_id = "default_generator";
        [NonSerialized]
        public name_generate_func generate_func;
        [NonSerialized]
        public float total_weight;
        public string generate(params object[] @params)
        {
            return this.generate_func(this, @params);
        }
        public NameGenerateTemplate get_random_template()
        {
            float weight = Toolbox.randomFloat(0, total_weight);
            float cur_weight = 0;
            foreach(NameGenerateTemplate template in templates)
            {
                if(weight > cur_weight && weight <= cur_weight + template.weight)
                {
                    return template;
                }
                cur_weight += template.weight;
            }
            throw new Exception("Should not reach here");
        }
    }
}
