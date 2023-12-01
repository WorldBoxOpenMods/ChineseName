using System.Text;
using Newtonsoft.Json;

namespace Chinese_Name;

public class CN_NameTemplate
{
    [JsonProperty("format")]
    public string raw_format { get; private set; }
    
    [JsonProperty("weight")]
    public float weight { get; private set; }
    public CN_NameTemplate(string pFormat, float pWeight)
    {
        raw_format = pFormat;
        weight = pWeight;
        Parse();
    }
    /// <summary>
    /// 获取需要填充的参数
    /// </summary>
    public Dictionary<string, string> GetParametersToFill()
    {
        return required_parameters.ToDictionary(required_parameter => required_parameter, _ => "");
    }
    /// <summary>
    /// 通过参数生成名字
    /// </summary>
    /// <param name="pParameters">填充后的通过<see cref="GetParametersToFill"/>获取的参数表</param>
    public string GenerateName(Dictionary<string, string> pParameters)
    {
        StringBuilder builder = new();
        
        foreach(var atom in atoms_before_generate)
        {
            if (!string.IsNullOrEmpty(atom.Tag)) continue;
            pParameters[atom.Tag] = WordLibraryManager.GetRandomWord(atom.GetWordLibraryId(pParameters));
        }

        foreach (var atom in atoms)
        {
            if(!string.IsNullOrEmpty(atom.Tag)) builder.Append(pParameters[atom.Tag]);
            builder.Append(WordLibraryManager.GetRandomWord(atom.GetWordLibraryId(pParameters)));
        }
        
        return builder.ToString();
    }
    
    private readonly HashSet<string> required_parameters = new();
    private readonly List<CN_NameTemplateAtom> atoms = new();
    private List<CN_NameTemplateAtom> atoms_before_generate = new();

    private class CN_NameTemplateAtom
    {
        public string Tag;
        public bool AllParametersRequired;
        public string[] ParametersValue;
        public string[] ParametersKey;
        public string Format;

        public string GetWordLibraryId(Dictionary<string, string> pParameters)
        {
            for (int i = 0; i < ParametersKey.Length; i++)
            {
                if (!pParameters.TryGetValue(ParametersKey[i], out ParametersValue[i]) && AllParametersRequired)
                {
                    return string.Empty;
                }
            }
            return string.Format(Format, ParametersValue);
        }
    }
    internal void Parse()
    {
        bool requiring_right_bracket = false;
        
        char[] format_key = new char[] { '{', '}', '<', '>' };
        int format_key_index = 0;

        bool char_valid(char ch)
        {
            if (!requiring_right_bracket) return true;
            for(int i = 0; i < format_key.Length; i++)
            {
                if(i == format_key_index) continue;
                if (ch == format_key[i]) return false;
            }
            return true;
        }
        
        bool reading_parameters = false;
        bool reading_tag = false;
        int parameter_index = 0;
        StringBuilder para_builder = new();
        StringBuilder tag_builder = new();
        StringBuilder format_builder = new();

        CN_NameTemplateAtom atom_in_recog = null;
        for(int i = 0; i < raw_format.Length; i++)
        {
            char ch = raw_format[i];
            if (!char_valid(ch))
                throw new Exception(
                    $"Invalid character '{ch}' at {i} in format '{raw_format}', need to be right bracket('>' or '}}').");

            if (requiring_right_bracket && ch is '}' or '>')
            {
                atom_in_recog.Tag = tag_builder.ToString();
                
                var para_list = para_builder.ToString().TrimEnd(';').Split(';').ToList();
                if (para_list.Count == 1 && string.IsNullOrEmpty(para_list[0]))
                {
                    para_list.Clear();
                }
                else if (para_list.Any(string.IsNullOrEmpty))
                {
                    throw new Exception($"Invalid parameters in format '{raw_format}', parameter cannot be empty.");
                }
                atom_in_recog.ParametersKey = para_list.ToArray();
                atom_in_recog.ParametersValue = new string[atom_in_recog.ParametersKey.Length];
                atom_in_recog.Format = format_builder.ToString();
                
                required_parameters.UnionWith(atom_in_recog.ParametersKey);
                
                tag_builder.Clear();
                para_builder.Clear();
                format_builder.Clear();
                requiring_right_bracket = false;
                reading_parameters = false;
                reading_tag = false;
                
                atoms.Add(atom_in_recog);
                atom_in_recog = new();
                continue;
            }
            
            switch (ch)
            {
                case '{':
                    atom_in_recog = new();
                    atom_in_recog.AllParametersRequired = false;
                    requiring_right_bracket = true;
                    format_key_index = 1;
                    continue;
                case '<':
                    atom_in_recog = new();
                    atom_in_recog.AllParametersRequired = true;
                    requiring_right_bracket = true;
                    format_key_index = 3;
                    continue;
                case '$':
                    if (reading_tag)
                    {
                        throw new Exception($"Invalid character '{ch}' at {i} in format '{raw_format}', tag does not support reference.");
                    }
                    if (reading_parameters)
                    {
                        para_builder.Append(';');
                    }
                    else
                    {
                        format_builder.Append($"{{{parameter_index++}}}");
                    }
                    reading_parameters = !reading_parameters;
                    continue;
                case ':':
                    if (reading_parameters)
                    {
                        throw new Exception($"Invalid character '{ch}' at {i} in format '{raw_format}', parameter block is not closed.");
                    }
                    reading_tag = true;
                    continue;
                case '}':
                case '>':
                    throw new Exception($"Invalid character '{ch}' at {i} in format '{raw_format}', need to have left bracket('<' or '{{') before it.");
                default:
                    if (reading_parameters)
                    {
                        para_builder.Append(ch);
                    }
                    else if (reading_tag)
                    {
                        tag_builder.Append(ch);
                    }
                    else
                    {
                        format_builder.Append(ch);
                    }
                    continue;
            }
            
        }
        Dictionary<string, AtomNode> atom_nodes = new();
        foreach (CN_NameTemplateAtom atom in atoms.Where(atom => !string.IsNullOrEmpty(atom.Tag)))
        {
            atoms_before_generate.Add(atom);
            if(atom_nodes.ContainsKey(atom.Tag))
            {
                throw new Exception($"Duplicate tag '{atom.Tag}' in format '{raw_format}'.");
            }
            required_parameters.Remove(atom.Tag);
            atom_nodes[atom.Tag] = new AtomNode(atom);
        }
        
        foreach(var atom in atoms_before_generate)
        {
            foreach(var depend_on_tag in atom.ParametersKey)
            {
                if(!atom_nodes.ContainsKey(depend_on_tag))
                {
                    continue;
                }
                var depend_on_node = atom_nodes[depend_on_tag];
                var depend_by_node = atom_nodes[atom.Tag];
                depend_on_node.depend_by.Add(depend_by_node);
                depend_by_node.depend_on.Add(depend_on_node);
            }
        }
        List<CN_NameTemplateAtom> sorted_atoms = new();
        Queue<AtomNode> queue = new();
        foreach (AtomNode node in atom_nodes.Values.Where(node => node.depend_on.Count == 0))
        {
            queue.Enqueue(node);
        }
        
        while(queue.Count > 0)
        {
            var node = queue.Dequeue();
            sorted_atoms.Add(node.atom);
            foreach(var depend_by_node in node.depend_by)
            {
                depend_by_node.depend_on.Remove(node);
                if(depend_by_node.depend_on.Count == 0)
                {
                    queue.Enqueue(depend_by_node);
                }
            }
        }
        
        atoms_before_generate = sorted_atoms;
    }

    private class AtomNode
    {
        public CN_NameTemplateAtom atom;
        public HashSet<AtomNode> depend_on = new();
        public HashSet<AtomNode> depend_by = new();

        public AtomNode(CN_NameTemplateAtom atom)
        {
            this.atom = atom;
        }
    }
}