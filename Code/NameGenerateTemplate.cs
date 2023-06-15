using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    public class TemplateElement
    {
        public float possibility;
        public string id;
        public List<WordLibrary> libraries = new List<WordLibrary>();
        public Dictionary<string, WordLibrary> optional_libraries = new Dictionary<string, WordLibrary>();
        public TemplateElement(string word_library_id, float possibility)
        {
            if (Main.instance.word_libraries.libraries.ContainsKey(word_library_id))
            {
                optional_libraries[word_library_id] = Main.instance.word_libraries.get(word_library_id);
                libraries.Add(optional_libraries[word_library_id]);
            }
            this.possibility = possibility;
            this.id = word_library_id;
        }
        public void append_word_library(string word_library_id)
        {
            if (Main.instance.word_libraries.libraries.ContainsKey(word_library_id))
            {
                optional_libraries[word_library_id] = Main.instance.word_libraries.get(word_library_id);
                libraries.Add(optional_libraries[word_library_id]);
            }
        }
        public string get_word(string library_id = null)
        {
            if (!Toolbox.randomChance(possibility))
            {
                return string.Empty;
            }
            return  libraries.Count>0 ? (library_id == null ? libraries.GetRandom().get_random() : optional_libraries[library_id].get_random()) : id;
        }
    }
    public class NameGenerateTemplate
    {
        /**
         * 导入词库，词库各有id
         * "{id,possibility}"为一项
         */
        public string template = string.Empty;
        public float weight = 1f;
        [NonSerialized]
        public List<TemplateElement> list = new List<TemplateElement>();
        public NameGenerateTemplate(string template, float possibility)
        {
            this.template = template;
            this.weight = possibility;
        }
        public void finish()
        {
            string[] split_result = template.Replace(" ", "").Split(new char[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(string unit in split_result)
            {
                string[] parts = unit.Split(',');
                Main.log($"'{parts[0]}','{parts[1]}'");
                list.Add(new TemplateElement(parts[0], Convert.ToSingle(parts[1])));
            }
        }
        public string get_template()
        {
            StringBuilder string_builder = new StringBuilder();
            foreach(TemplateElement element in list)
            {
                string_builder.Append(element.get_word());
            }
            return string_builder.ToString();
        }
        public string get_part_step_by_step(int i)
        {
            return list[i].get_word();
        }
    }
}
