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
    private sealed class TemplateGenerationFailedException : Exception
    {
    }

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
        ParseNT();
        pParameters ??= new Dictionary<string, string>();

        var builder = StringBuilderPool.Rent(raw_format?.Length ?? 0);
        try
        {
            _root.ParseParamInto(builder, pParameters);
            return builder.ToString();
        }
        catch (TemplateGenerationFailedException)
        {
            return string.Empty;
        }
        catch (MissingRequiredWordLibraryException)
        {
            return string.Empty;
        }
        finally
        {
            StringBuilderPool.Return(builder);
        }
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
        public string CachedText;

        public override void AddChild(TemplateNode node)
        {
            throw new NotSupportedException("Raw Text Node should not have children");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void ParseParamInto(StringBuilder builder, Dictionary<string, string> parameters)
        {
            if (CachedText != null)
            {
                builder.Append(CachedText);
                return;
            }

            builder.Append(TextBuilder);
        }

        public override string ToString()
        {
            return $"Text[{TextBuilder.ToString()}]";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetText()
        {
            return CachedText ??= TextBuilder.ToString();
        }
    }

    class ComplexNode : TemplateNode
    {
        public bool IsBuildingParam = false;

        public ComplexNodeType Type;
        public List<TemplateNode> ParamChildren = new();
        public string FixedParamId;
        public string FixedContent;

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
            var param_id = FixedParamId ?? BuildNodeText(ParamChildren, parameters);
            if (!string.IsNullOrEmpty(param_id) && parameters.TryGetValue(param_id, out var param))
            {
                if (string.IsNullOrEmpty(param))
                {
                    throw new TemplateGenerationFailedException();
                }

                builder.Append(param);
                return;
            }

            var content_id = FixedContent ?? BuildNodeText(Children, parameters);

            string left_value = string.Empty;
            switch (Type)
            {
                case ComplexNodeType.Parameter:
                    if (!parameters.TryGetValue(content_id, out left_value) || string.IsNullOrEmpty(left_value))
                    {
                        throw new TemplateGenerationFailedException();
                    }

                    break;
                case ComplexNodeType.RequiredWordLibrary:
                    var library_1 = WordLibraryLibrary.Instance.get(content_id);
                    if (library_1 == null)
                    {
                        throw new MissingRequiredWordLibraryException(content_id);
                    }

                    left_value = library_1.GetRandom();
                    if (string.IsNullOrEmpty(left_value))
                    {
                        throw new TemplateGenerationFailedException();
                    }

                    break;
                case ComplexNodeType.OptionalWordLibrary:
                    left_value = WordLibraryLibrary.Instance.get(content_id)?.GetRandom() ?? string.Empty;
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

    class GroupNode : TemplateNode
    {
        public override string ToString()
        {
            return $"Group[{Children.Count}]";
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
            var builder = StringBuilderPool.Rent();
            try
            {
                base.ParseParamInto(builder, parameters);
            }
            finally
            {
                StringBuilderPool.Return(builder);
            }
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
            var old_length = builder.Length;
            if (old_length == 0)
            {
                return;
            }

            var step_length = StepConfigured ? (NegStep ? -StepLength : StepLength) : 1;
            if (step_length == 0)
            {
                builder.Clear();
                return;
            }

            if (step_length > 0)
            {
                var start_index = ResolvePositiveSliceIndex(old_length, StartConfigured, NegStart, StartIndex, 0);
                var end_index = ResolvePositiveSliceIndex(old_length, EndConfigured, NegEnd, EndIndex, old_length);
                for (int i = start_index; i < end_index; i += step_length)
                {
                    builder.Append(builder[i]);
                }
            }
            else
            {
                var start_index = ResolveNegativeSliceIndex(old_length, StartConfigured, NegStart, StartIndex, old_length - 1);
                var end_index = ResolveNegativeSliceIndex(old_length, EndConfigured, NegEnd, EndIndex, -1);
                for (int i = start_index; i > end_index; i += step_length)
                {
                    builder.Append(builder[i]);
                }
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
        raw_format ??= string.Empty;

        var atom_stack = new Stack<TemplateNode>();
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
                    if (atom_stack.Count == 0)
                    {
                        throw new InvalidCharException(ch, i, raw_format, "未匹配到对应的纯文本开始标记");
                    }

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
                    CloseCurrentNode<ComplexNode>(atom_stack, ref current_node, ch, i, raw_format,
                        "未匹配到对应的必填词库开始标记", node => node.Type == ComplexNodeType.RequiredWordLibrary);
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
                    CloseCurrentNode<ComplexNode>(atom_stack, ref current_node, ch, i, raw_format,
                        "未匹配到对应的可选词库开始标记", node => node.Type == ComplexNodeType.OptionalWordLibrary);
                    break;
                case '(':
                    tmp_node = new GroupNode();
                    if (current_node is RawTextNode)
                    {
                        current_node = atom_stack.Pop();
                    }
                    atom_stack.Push(current_node);
                    current_node.AddChild(tmp_node);
                    current_node = tmp_node;
                    break;
                case ')':
                    CloseCurrentNode<GroupNode>(atom_stack, ref current_node, ch, i, raw_format, "未匹配到对应的分组开始标记");
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
                    CloseCurrentNode<SliceNode>(atom_stack, ref current_node, ch, i, raw_format, "未匹配到对应的切片开始标记",
                        node =>
                        {
                            ValidateSliceNode(node, ch, i, raw_format);
                            return true;
                        });
                    break;
                case '$':
                    if (MatchesCurrentOrParent<ComplexNode>(current_node, atom_stack, node => node.Type == ComplexNodeType.Parameter))
                    {
                        CloseCurrentNode<ComplexNode>(atom_stack, ref current_node, ch, i, raw_format,
                            "未匹配到对应的参数开始标记", node => node.Type == ComplexNodeType.Parameter);
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
                    if (MatchesCurrentOrParent<PlaceholderNode>(current_node, atom_stack))
                    {
                        CloseCurrentNode<PlaceholderNode>(atom_stack, ref current_node, ch, i, raw_format,
                            "未匹配到对应的占位开始标记");
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
                        if (slice_node_tmp.ConfigureIndex > 2)
                        {
                            throw new InvalidCharException(ch, i, raw_format, "切片操作[]最多只支持开始、结束、步长三段");
                        }
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
                        if ((ch is < '0' or > '9') && ch != '-')
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

        if (current_node is ForceRawTextNode)
        {
            throw new FormatException($"模板\"{raw_format}\"存在未闭合的纯文本片段");
        }

        if (current_node is RawTextNode)
        {
            if (atom_stack.Count == 0)
            {
                throw new FormatException($"模板\"{raw_format}\"在解析结束时处于非法状态");
            }

            current_node = atom_stack.Pop();
        }

        if (!ReferenceEquals(current_node, root) || atom_stack.Count > 0)
        {
            throw new FormatException($"模板\"{raw_format}\"存在未闭合的结构");
        }

        #endregion

        #region 合并固定文本
        
        atom_stack.Clear();
        atom_stack.Push(root);
        while (atom_stack.Count > 0)
        {
            var node = atom_stack.Pop();

            MergeSingleNodeRawText(node);
            PrepareSingleNode(node);
            
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
        has_parsed = true;
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

    private static void PrepareSingleNode(TemplateNode node)
    {
        CacheRawText(node.Children);

        if (node is not ComplexNode complex_node)
        {
            return;
        }

        CacheRawText(complex_node.ParamChildren);
        complex_node.FixedContent = TryGetSingleRawText(node.Children);
        complex_node.FixedParamId = TryGetSingleRawText(complex_node.ParamChildren);
    }

    private static void CacheRawText(List<TemplateNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is RawTextNode raw_text_node)
            {
                _ = raw_text_node.GetText();
            }
        }
    }

    private static string TryGetSingleRawText(List<TemplateNode> nodes)
    {
        return nodes.Count == 1 && nodes[0] is RawTextNode raw_text_node ? raw_text_node.GetText() : null;
    }

    private static string BuildNodeText(List<TemplateNode> nodes, Dictionary<string, string> parameters)
    {
        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        if (nodes.Count == 1 && nodes[0] is RawTextNode raw_text_node)
        {
            return raw_text_node.GetText();
        }

        var builder = StringBuilderPool.Rent();
        try
        {
            foreach (var node in nodes)
            {
                node.ParseParamInto(builder, parameters);
            }

            return builder.ToString();
        }
        finally
        {
            StringBuilderPool.Return(builder);
        }
    }

    private static void CloseCurrentNode<TNode>(Stack<TemplateNode> atom_stack, ref TemplateNode current_node, char ch, int index,
        string format, string error_message, Func<TNode, bool> validator = null) where TNode : TemplateNode
    {
        if (current_node is RawTextNode)
        {
            if (atom_stack.Count == 0)
            {
                throw new InvalidCharException(ch, index, format, error_message);
            }

            current_node = atom_stack.Pop();
        }

        if (current_node is not TNode typed_node || validator != null && !validator(typed_node))
        {
            throw new InvalidCharException(ch, index, format, error_message);
        }

        if (atom_stack.Count == 0)
        {
            throw new InvalidCharException(ch, index, format, error_message);
        }

        current_node = atom_stack.Pop();
    }

    private static bool MatchesCurrentOrParent<TNode>(TemplateNode current_node, Stack<TemplateNode> atom_stack,
        Func<TNode, bool> validator = null) where TNode : TemplateNode
    {
        if (current_node is TNode typed_node && (validator == null || validator(typed_node)))
        {
            return true;
        }

        if (current_node is RawTextNode && atom_stack.Count > 0 && atom_stack.Peek() is TNode parent_node &&
            (validator == null || validator(parent_node)))
        {
            return true;
        }

        return false;
    }

    private static void ValidateSliceNode(SliceNode node, char ch, int index, string format)
    {
        if (node.ConfigureIndex > 2)
        {
            throw new InvalidCharException(ch, index, format, "切片操作[]最多只支持开始、结束、步长三段");
        }

        if (node.StepConfigured && node.StepLength == 0)
        {
            throw new InvalidCharException(ch, index, format, "切片操作[]中的步长不能为0");
        }
    }

    private static int ResolvePositiveSliceIndex(int length, bool configured, bool negative, int value, int default_value)
    {
        if (!configured)
        {
            return default_value;
        }

        var resolved = negative ? length - value : value;
        if (resolved < 0) return 0;
        if (resolved > length) return length;
        return resolved;
    }

    private static int ResolveNegativeSliceIndex(int length, bool configured, bool negative, int value, int default_value)
    {
        if (!configured)
        {
            return default_value;
        }

        var resolved = negative ? length - value : value;
        if (resolved < -1) return -1;
        if (resolved >= length) return length - 1;
        return resolved;
    }

    private static class StringBuilderPool
    {
        private const int MaxRetainedCapacity = 1024;
        private const int MaxRetainedCount = 8;

        [ThreadStatic]
        private static Stack<StringBuilder> _cache;

        public static StringBuilder Rent(int capacity = 0)
        {
            if (_cache is { Count: > 0 })
            {
                var builder = _cache.Pop();
                if (capacity > 0)
                {
                    builder.EnsureCapacity(capacity);
                }

                return builder;
            }

            return capacity > 0 ? new StringBuilder(capacity) : new StringBuilder();
        }

        public static void Return(StringBuilder builder)
        {
            builder.Clear();
            if (builder.Capacity > MaxRetainedCapacity)
            {
                builder.Capacity = MaxRetainedCapacity;
            }

            _cache ??= new Stack<StringBuilder>(4);
            if (_cache.Count < MaxRetainedCount)
            {
                _cache.Push(builder);
            }
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
