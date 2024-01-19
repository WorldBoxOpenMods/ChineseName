using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Chinese_Name.exceptions;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;

namespace Chinese_Name;

/// <summary>
/// 单个的命名模板
/// </summary>
[Serializable]
public class CN_NameTemplate
{
    private readonly List<CN_NameTemplateAtom> atoms = new();

    private readonly HashSet<string>           required_parameters   = new();
    private          List<CN_NameTemplateAtom> atoms_before_generate = new();
    private          bool                      has_parsed            = false;

    CN_NameTemplate(string pFormat, float pWeight)
    {
        raw_format = pFormat;
        weight = pWeight;
        Parse();
    }

    CN_NameTemplate()
    {
    }

    [JsonProperty("format")] public string raw_format { get; private set; }

    [JsonProperty("weight")] public float weight { get; private set; } = 1;

    /// <summary>
    /// 创建器
    /// </summary>
    /// <param name="pFormat">format文本</param>
    /// <param name="pWeight">权重</param>
    /// <returns></returns>
    public static CN_NameTemplate Create(string pFormat, float pWeight)
    {
        return new CN_NameTemplate(pFormat, pWeight);
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
    [Hotfixable]
    public string GenerateName(Dictionary<string, string> pParameters)
    {
        StringBuilder builder = new();

        foreach (var atom in atoms_before_generate)
        {
            if (pParameters.TryGetValue(atom.Tag, out string para) && !string.IsNullOrEmpty(para)) continue;
            pParameters[atom.Tag] = WordLibraryManager.GetRandomWord(atom.GetFilledTemplate(pParameters));
        }

        foreach (var atom in atoms)
        {
            if (!string.IsNullOrEmpty(atom.Tag))
            {
                builder.Append(pParameters[atom.Tag]);
                continue;
            }

            switch (atom.Type)
            {
                case AtomType.Parameter:
                    string para = atom.GetFilledTemplate(pParameters);
                    if (string.IsNullOrEmpty(para))
                    {
                        return string.Empty;
                    }

                    builder.Append(atom.GetFilledTemplate(pParameters));
                    continue;
                case AtomType.RawText:
                    builder.Append(atom.Format);
                    continue;
                case AtomType.WordLibrary:
                    string word = WordLibraryManager.GetRandomWord(atom.GetFilledTemplate(pParameters));
                    if (string.IsNullOrEmpty(word) && atom.AllParametersRequired)
                    {
                        return string.Empty;
                    }

                    builder.Append(word);
                    continue;
            }
        }

        return builder.ToString();
    }

    internal void ReParse()
    {
        has_parsed = false;
        atoms.Clear();
        atoms_before_generate.Clear();
        required_parameters.Clear();
        Parse();
    }

    internal void Parse()
    {
        if (has_parsed) return;
        has_parsed = true;

        bool requiring_right_bracket = false;

        char[] format_key = new char[] { '{', '}', '<', '>' };
        int format_key_index = 0;

        bool char_valid(char ch)
        {
            if (!requiring_right_bracket) return true;
            for (int i = 0; i < format_key.Length; i++)
            {
                if (i  == format_key_index) continue;
                if (ch == format_key[i]) return false;
            }

            return true;
        }

        bool reading_parameters = false;
        bool reading_tag = false;
        bool reading_raw_text = false;
        int parameter_index = 0;
        StringBuilder para_builder = new();
        StringBuilder tag_builder = new();
        StringBuilder format_builder = new();

        CN_NameTemplateAtom atom_in_recog = new();
        for (int i = 0; i < raw_format.Length; i++)
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
                atom_in_recog.Type = AtomType.WordLibrary;

                atoms.Add(atom_in_recog);
                atom_in_recog = new();
                parameter_index = 0;
                continue;
            }

            switch (ch)
            {
                case '{':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_raw_text)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Raw Text Block");
                    }

                    if (reading_parameters)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Parameter Block");
                    }

                    atom_in_recog.AllParametersRequired = false;
                    requiring_right_bracket = true;
                    format_key_index = 1;
                    continue;
                case '<':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_raw_text)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Raw Text Block");
                    }

                    if (reading_parameters)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Parameter Block");
                    }

                    atom_in_recog.AllParametersRequired = true;
                    requiring_right_bracket = true;
                    format_key_index = 3;
                    continue;
                case '$':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_raw_text)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Raw Text Block");
                    }

                    if (reading_parameters)
                    {
                        if (!requiring_right_bracket)
                        {
                            atom_in_recog.Format = format_builder.ToString();
                            atom_in_recog.Tag = tag_builder.ToString();
                            atom_in_recog.AllParametersRequired = true;
                            atom_in_recog.ParametersKey = new string[1] { para_builder.ToString() };
                            atom_in_recog.ParametersValue = new string[atom_in_recog.ParametersKey.Length];
                            atom_in_recog.Type = AtomType.Parameter;
                            atoms.Add(atom_in_recog);
                            tag_builder.Clear();
                            para_builder.Clear();
                            format_builder.Clear();
                            reading_parameters = false;
                            atom_in_recog = new();
                            parameter_index = 0;
                            continue;
                        }

                        para_builder.Append(';');
                    }
                    else
                    {
                        format_builder.Append($"{{{parameter_index++}}}");
                    }

                    reading_parameters = !reading_parameters;
                    continue;
                case '#':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_parameters)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Parameter Block");
                    }

                    if (reading_raw_text)
                    {
                        atom_in_recog.Format = format_builder.ToString();
                        atom_in_recog.Tag = tag_builder.ToString();
                        atom_in_recog.AllParametersRequired = false;
                        atom_in_recog.ParametersKey = Array.Empty<string>();
                        atom_in_recog.ParametersValue = Array.Empty<string>();
                        atom_in_recog.Type = AtomType.RawText;
                        atoms.Add(atom_in_recog);
                        tag_builder.Clear();
                        para_builder.Clear();
                        format_builder.Clear();
                        reading_raw_text = false;
                        atom_in_recog = new();
                    }
                    else
                    {
                        reading_raw_text = true;
                    }

                    continue;
                case ':':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_raw_text)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Raw Text Block");
                    }

                    if (reading_parameters)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Parameter Block");
                    }

                    reading_tag = true;
                    continue;
                case '}':
                case '>':
                    if (reading_tag)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Tag Block");
                    }

                    if (reading_raw_text)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Raw Text Block");
                    }

                    if (reading_parameters)
                    {
                        throw new InvalidKeyCharException(ch, i, raw_format, "Parameter Block");
                    }

                    throw new InvalidKeyCharException(ch, i, raw_format, "Missing Left Bracket");
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

        if (requiring_right_bracket)
        {
            throw new Exception(
                                $"Missing right bracket('>' or '}}') in format '{raw_format}'. (Maybe you forget to close a atom block?)");
        }

        Dictionary<string, AtomNode> atom_nodes = new();
        foreach (CN_NameTemplateAtom atom in atoms.Where(atom => !string.IsNullOrEmpty(atom.Tag)))
        {
            atoms_before_generate.Add(atom);
            if (atom_nodes.ContainsKey(atom.Tag))
            {
                throw new Exception($"Duplicate tag '{atom.Tag}' in format '{raw_format}'.");
            }

            required_parameters.Remove(atom.Tag);
            atom_nodes[atom.Tag] = new AtomNode(atom);
        }

        foreach (var atom in atoms_before_generate)
        {
            foreach (var depend_on_tag in atom.ParametersKey)
            {
                if (!atom_nodes.ContainsKey(depend_on_tag))
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

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            sorted_atoms.Add(node.atom);
            foreach (var depend_by_node in node.depend_by)
            {
                depend_by_node.depend_on.Remove(node);
                if (depend_by_node.depend_on.Count == 0)
                {
                    queue.Enqueue(depend_by_node);
                }
            }
        }

        atoms_before_generate = sorted_atoms;
    }

    private enum AtomType
    {
        WordLibrary,
        RawText,
        Parameter
    }

    private class CN_NameTemplateAtom
    {
        public bool     AllParametersRequired;
        public string   Format;
        public string[] ParametersKey;
        public string[] ParametersValue;
        public string   Tag;
        public AtomType Type = AtomType.WordLibrary;

        [Hotfixable]
        public string GetFilledTemplate(Dictionary<string, string> pParameters)
        {
            for (int i = 0; i < ParametersKey.Length; i++)
            {
                if (!pParameters.TryGetValue(ParametersKey[i], out ParametersValue[i])                        &&
                    !ModClass.Instance.GlobalParameters.TryGetValue(ParametersKey[i], out ParametersValue[i]) &&
                    AllParametersRequired)
                {
                    return string.Empty;
                }
            }

            try
            {
                return string.Format(Format, ParametersValue);
            }
            catch (Exception)
            {
                ModClass.LogError(
                                  $"Failed to format '{Format}' with parameters '{string.Join(", ", ParametersValue)}'.");
                return string.Empty;
            }
        }
    }

    private class AtomNode
    {
        public CN_NameTemplateAtom atom;
        public HashSet<AtomNode>   depend_by = new();
        public HashSet<AtomNode>   depend_on = new();

        public AtomNode(CN_NameTemplateAtom atom)
        {
            this.atom = atom;
        }
    }
}