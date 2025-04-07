using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Chinese_Name.Exceptions;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;

namespace Chinese_Name;

/// <summary>
/// 单个的命名模板
/// </summary>
[Serializable]
public class ChineseNameTemplate
{
    private          bool                      has_parsed            = false;

    ChineseNameTemplate(string pFormat, float pWeight)
    {
        raw_format = pFormat;
        weight = pWeight;
        ParseNT();
    }

    ChineseNameTemplate()
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
    public static ChineseNameTemplate Create(string pFormat, float pWeight)
    {
        return new ChineseNameTemplate(pFormat, pWeight);
    }

    /// <summary>
    /// 通过参数生成名字
    /// </summary>
    [Hotfixable]
    public string GenerateName(Dictionary<string, string> pParameters)
    {
        var builder = new StringBuilder();
        try
        {
            _root.ParseParamInto(builder, pParameters);
        }
        catch (MissingRequiredWordLibraryException)
        {
            return string.Empty;
        }
        return builder.ToString();
    }

    private TemplateNode _root = null;
    internal void ReParse()
    {
        has_parsed = false;
        ParseNT();
    }

    class TemplateNode
    {
        public TemplateNode Parent;
        public List<TemplateNode> Children = new();

        public virtual void AddChild(TemplateNode node)
        {
            Children.Add(node);
            node.Parent = this;
        }

        public override string ToString()
        {
            return $"Root[{Children.Count}]";
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void ParseParamInto(StringBuilder builder, Dictionary<string, string> parameters)
        {
            foreach (var child in Children)
            {
                child.ParseParamInto(builder, parameters);
            }
        }
    }

    class RawTextNode : TemplateNode
    {
        public StringBuilder TextBuilder = new();

        public override void AddChild(TemplateNode node)
        {
            throw new NotSupportedException("Raw Text Node should not have children");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void ParseParamInto(StringBuilder builder, Dictionary<string, string> parameters)
        {
            builder.Append(TextBuilder);
        }

        public override string ToString()
        {
            return $"Text[{TextBuilder.ToString()}]";
        }
    }

    class ComplexNode : TemplateNode
    {
        public bool IsBuildingParam = false;

        public ComplexNodeType Type;
        public List<TemplateNode> ParamChildren = new();

        public override void AddChild(TemplateNode node)
        {
            if (IsBuildingParam)
            {
                ParamChildren.Add(node);
            }
            else
            {
                base.AddChild(node);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void ParseParamInto(StringBuilder builder, Dictionary<string, string> parameters)
        {
            var local_builder = new StringBuilder();
            foreach (var node in ParamChildren)
            {
                node.ParseParamInto(local_builder, parameters);
            }

            var param_id = local_builder.ToString();
            if (parameters.TryGetValue(param_id, out var param))
            {
                builder.Append(param);
                return;
            }

            local_builder.Clear();
            base.ParseParamInto(local_builder, parameters);
            
            string left_value = string.Empty;
            switch (Type)
            {
                case ComplexNodeType.Parameter:
                    if (!parameters.TryGetValue(local_builder.ToString(), out left_value))
                    {
                        left_value = string.Empty;
                    }
                    break;
                case ComplexNodeType.RequiredWordLibrary:
                    var library_1 = WordLibraryLibrary.Instance.get(local_builder.ToString());
                    if (library_1 == null)
                    {
                        throw new MissingRequiredWordLibraryException(local_builder.ToString());
                    }
                    left_value = library_1.GetRandom();
                    break;
                case ComplexNodeType.OptionalWordLibrary:
                    left_value = WordLibraryLibrary.Instance.get(local_builder.ToString())?.GetRandom() ?? string.Empty;
                    break;
            }

            if (!string.IsNullOrEmpty(param_id))
            {
                parameters[param_id] = left_value;
            }
            builder.Append(left_value);
            return;
        }

        public override string ToString()
        {
            return $"{Type}[{Children.Count}:{ParamChildren.Count}]";
        }
    }

    class ForceRawTextNode : RawTextNode
    {
        public override string ToString()
        {
            return $"ForceText[{TextBuilder.ToString()}]";
        }
    }

    class PlaceholderNode : TemplateNode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void ParseParamInto(StringBuilder _, Dictionary<string, string> parameters)
        {
            var builder = new StringBuilder();
            base.ParseParamInto(builder, parameters);
        }

        public override string ToString()
        {
            return $"Placeholder[{Children.Count}]";
        }
    }
    class SliceNode : TemplateNode
    {
        /// <summary>
        /// 开始(包含)
        /// </summary>
        public int StartIndex;
        public bool NegStart;
        public bool StartConfigured;
        /// <summary>
        /// 结束(不包含)
        /// </summary>
        public int EndIndex;
        public bool NegEnd;
        public bool EndConfigured;
        /// <summary>
        /// 步长
        /// </summary>
        public int StepLength;
        public bool NegStep;
        public bool StepConfigured;

        public int ConfigureIndex;
        public override void AddChild(TemplateNode node)
        {
            throw new NotSupportedException("Slice Node should not have children");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void ParseParamInto(StringBuilder builder, Dictionary<string, string> parameters)
        {
            var start_index = NegStart ? -StartIndex : StartIndex;
            var end_index = NegEnd ? -EndIndex : EndIndex;
            var step_length = NegStep ? -StepLength : StepLength;
            if (!StartConfigured) start_index = 0;
            if (!EndConfigured) end_index = builder.Length;
            if (!StepConfigured) step_length = 1;

            start_index = (start_index + builder.Length) % builder.Length;
            end_index = (end_index + builder.Length) % builder.Length;

            if (end_index < start_index)
            {
                end_index += builder.Length;
            }
            
            int old_length = builder.Length;
            for (int i = start_index; i < end_index; i += step_length)
            {
                builder.Append(builder[i % old_length]);
            }

            builder.Remove(0, old_length);
        }
    }
    enum ComplexNodeType
    {
        RequiredWordLibrary,
        OptionalWordLibrary,
        Parameter
    }
    internal void ParseNT()
    {
        if (has_parsed) return;
        has_parsed = true;

        var atom_stack = new Stack<TemplateNode>();

        var example =
            "($prefix_library$[-2:])甲子<{动物-$type{现有国名:actor_kingdom}:prefix_library$-词库类型}#纯文本乱码)}({><#>{国名后缀}";
        // 甲子<{动物-$type{现有国名:actor_kingdom}:prefix_library$-词库类型}#纯文本乱码)}({><#>{国名后缀}($prefix_library$[-2:])
        #region 解析为表达树

        var root = new TemplateNode();
        var current_node = root;
        for (int i = 0; i < raw_format.Length; i++)
        {
            char ch = raw_format[i];
            TemplateNode tmp_node = null;
            if (current_node is ForceRawTextNode force_raw_text_node)
            {
                if (ch == '#')
                {
                    current_node = atom_stack.Pop();
                }
                else
                {
                    force_raw_text_node.TextBuilder.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '{':
                    tmp_node = new ComplexNode()
                    {
                        Type = ComplexNodeType.RequiredWordLibrary
                    };
                    if (current_node is RawTextNode)
                    {
                        current_node = atom_stack.Pop();
                    }

                    atom_stack.Push(current_node);
                    current_node.AddChild(tmp_node);
                    current_node = tmp_node;
                    break;
                case '}':
                    if (current_node is RawTextNode)
                    {
                        _ = atom_stack.Pop();
                    }
                    current_node = atom_stack.Pop();
                    break;
                case '<':
                    tmp_node = new ComplexNode()
                    {
                        Type = ComplexNodeType.OptionalWordLibrary
                    };
                    if (current_node is RawTextNode)
                    {
                        current_node = atom_stack.Pop();
                    }

                    atom_stack.Push(current_node);
                    current_node.AddChild(tmp_node);
                    current_node = tmp_node;
                    break;
                case '>':
                    if (current_node is RawTextNode)
                    {
                        _ = atom_stack.Pop();
                    }
                    current_node = atom_stack.Pop();
                    break;
                case '(':
                    tmp_node = new TemplateNode();
                    if (current_node is RawTextNode)
                    {
                        current_node = atom_stack.Pop();
                    }
                    atom_stack.Push(current_node);
                    current_node.AddChild(tmp_node);
                    current_node = tmp_node;
                    break;
                case ')':
                    if (current_node is RawTextNode)
                    {
                        _ = atom_stack.Pop();
                    }
                    current_node = atom_stack.Pop();
                    break;
                case '[':
                    tmp_node = new SliceNode();
                    if (current_node is RawTextNode)
                    {
                        current_node = atom_stack.Pop();
                    }
                    atom_stack.Push(current_node);
                    current_node.AddChild(tmp_node);
                    current_node = tmp_node;
                    break;
                case ']':
                    if (current_node is RawTextNode)
                    {
                        _ = atom_stack.Pop();
                    }
                    current_node = atom_stack.Pop();
                    break;
                case '$':
                    if (current_node is ComplexNode { Type: ComplexNodeType.Parameter })
                    {
                        current_node = atom_stack.Pop();
                    }
                    else
                    {
                        tmp_node = new ComplexNode()
                        {
                            Type = ComplexNodeType.Parameter
                        };
                        if (current_node is RawTextNode)
                        {
                            current_node = atom_stack.Pop();
                        }

                        atom_stack.Push(current_node);
                        current_node.AddChild(tmp_node);
                        current_node = tmp_node;
                    }

                    break;
                case '^':
                    if (current_node is PlaceholderNode)
                    {
                        current_node = atom_stack.Pop();
                    }
                    else
                    {
                        tmp_node = new PlaceholderNode();
                        if (current_node is RawTextNode)
                        {
                            current_node = atom_stack.Pop();
                        }

                        atom_stack.Push(current_node);
                        current_node.AddChild(tmp_node);
                        current_node = tmp_node;
                    }

                    break;
                case ':':
                    if (current_node is ComplexNode complex_node)
                    {
                        complex_node.IsBuildingParam = true;
                    }
                    else if (current_node is SliceNode slice_node_tmp)
                    {
                        slice_node_tmp.ConfigureIndex++;
                    }

                    break;
                case '#':
                    if (current_node is RawTextNode raw_text_node_1)
                    {
                        current_node = new ForceRawTextNode()
                        {
                            TextBuilder = raw_text_node_1.TextBuilder
                        };
                    }
                    else
                    {
                        atom_stack.Push(current_node);
                        tmp_node = new ForceRawTextNode();
                        current_node.AddChild(tmp_node);
                        current_node = tmp_node;
                    }

                    break;
                default:
                    if (current_node is SliceNode slice_node)
                    {
                        if ((ch is < '0' or > '9') && ch != '-' )
                        {
                            throw new InvalidCharException(ch, i, raw_format, "切片操作[]中应当只有数字或者冒号");
                        }

                        if (ch == '-')
                        {
                            if (i == raw_format.Length - 1 || raw_format[i + 1] is < '0' or > '9')
                            {
                                throw new InvalidCharException(ch, i, raw_format, "切片操作[]中负号不允许出现在末尾或者下一个字符不是数字");
                            }
                            switch (slice_node.ConfigureIndex)
                            {
                                case 0:
                                    if (slice_node.NegStart) throw new InvalidCharException(ch, i, raw_format, "切片操作[]中不允许两个负号叠加");
                                    slice_node.NegStart = true;
                                    break;
                                case 1:
                                    if (slice_node.NegEnd) throw new InvalidCharException(ch, i, raw_format, "切片操作[]中不允许两个负号叠加");
                                    slice_node.NegEnd = true;
                                    break;
                                case 2:
                                    if (slice_node.NegStep) throw new InvalidCharException(ch, i, raw_format, "切片操作[]中不允许两个负号叠加");
                                    slice_node.NegStep = true;
                                    break;
                                default:
                                    throw new InvalidCharException(ch, i, raw_format);
                            }

                            break;
                        }
                        switch (slice_node.ConfigureIndex)
                        {
                            case 0:
                                slice_node.StartIndex = slice_node.StartIndex * 10 + (ch - '0');
                                slice_node.StartConfigured = true;
                                break;
                            case 1:
                                slice_node.EndIndex = slice_node.EndIndex * 10 + (ch - '0');
                                slice_node.EndConfigured = true;
                                break;
                            case 2:
                                slice_node.StepLength = slice_node.StepLength * 10 + (ch - '0');
                                slice_node.StepConfigured = true;
                                break;
                        }
                        break;
                    }
                    if (current_node is not RawTextNode raw_text_node)
                    {
                        raw_text_node = new RawTextNode();
                        current_node.AddChild(raw_text_node);
                        atom_stack.Push(current_node);

                        current_node = raw_text_node;
                    }

                    raw_text_node.TextBuilder.Append(ch);
                    break;
            }
        }

        #endregion

        #region 合并固定文本
        
        atom_stack.Clear();
        atom_stack.Push(root);
        while (atom_stack.Count > 0)
        {
            var node = atom_stack.Pop();

            MergeSingleNodeRawText(node);
            
            foreach (var child in node.Children)
            {
                atom_stack.Push(child);
            }

            if (node is ComplexNode complex_node)
            {
                foreach (var param in complex_node.ParamChildren)
                {
                    atom_stack.Push(param);
                }
            }
        }

        #endregion
        
        _root = root;
    }

    private static void MergeSingleNodeRawText(TemplateNode node)
    {
        RawTextNode raw_text_node_tmp = null;
        List<TemplateNode> new_children = new();
        foreach (var child_node in node.Children)
        {
            if (child_node is RawTextNode raw_text_node)
            {
                if (raw_text_node_tmp is null)
                {
                    raw_text_node_tmp = raw_text_node;
                    new_children.Add(raw_text_node);
                }
                else
                {
                    raw_text_node_tmp.TextBuilder.Append(raw_text_node.TextBuilder);
                }
            }
            else
            {
                raw_text_node_tmp = null;
                new_children.Add(child_node);
            }
        }
        node.Children = new_children;
        
        
        

        if (node is ComplexNode complex_node)
        {
            raw_text_node_tmp = null;
            new_children = new();
            foreach (var child_node in complex_node.ParamChildren)
            {
                if (child_node is RawTextNode raw_text_node)
                {
                    if (raw_text_node_tmp is null)
                    {
                        raw_text_node_tmp = raw_text_node;
                        new_children.Add(raw_text_node);
                    }
                    else
                    {
                        raw_text_node_tmp.TextBuilder.Append(raw_text_node.TextBuilder);
                    }
                }
                else
                {
                    raw_text_node_tmp = null;
                    new_children.Add(child_node);
                }
            }
            complex_node.ParamChildren = new_children;
        }
    }
    private static void CheckSingleNodeParamFixedRequired(Dictionary<string, HashSet<ComplexNode>> dict, ComplexNode check_node)
    {
        var stack = new Stack<ComplexNode>();
        stack.Push(check_node);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Children.Count == 1 && node.Children[0] is RawTextNode raw_text_node_1)
            {
                AppendParamFixedRequired(raw_text_node_1.TextBuilder.ToString());
            }

            if (node.ParamChildren.Count == 1 && node.ParamChildren[0] is RawTextNode raw_text_node_2)
            {
                AppendParamFixedRequired(raw_text_node_2.TextBuilder.ToString());
            }

            foreach (var child in node.Children.OfType<ComplexNode>())
            {
                stack.Push(child);
            }

            foreach (var param in node.ParamChildren.OfType<ComplexNode>())
            {
                stack.Push(param);
            }
        }

        void AppendParamFixedRequired(string param)
        {
            if (!dict.ContainsKey(param))
            {
                dict.Add(param, new HashSet<ComplexNode>());
            }
            dict[param].Add(check_node);
        }
    }
}