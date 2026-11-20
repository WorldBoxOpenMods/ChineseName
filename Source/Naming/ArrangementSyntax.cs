using System;
using System.Collections.Generic;

namespace Chinese_Name.Naming;

internal enum ConditionSymbol { Clause, And, Or, Not, Open, Close }

internal static class ArrangementSyntax
{
    internal static List<(ConditionSymbol Symbol, ArrangementCondition Clause)> Tokens(ArrangementCondition root)
    {
        var result = new List<(ConditionSymbol, ArrangementCondition)>();
        Append(root, top: true);
        return result;

        void Append(ArrangementCondition node, bool top)
        {
            if (node.Negated) result.Add((ConditionSymbol.Not, null));
            if (node.Children == null)
            {
                var operand = NamingJson.Copy(node);
                operand.Negated = false;
                result.Add((ConditionSymbol.Clause, operand));
                return;
            }
            bool bracket = !top || node.Negated;
            if (bracket) result.Add((ConditionSymbol.Open, null));
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0) result.Add((node.Any ? ConditionSymbol.Or : ConditionSymbol.And, null));
                Append(node.Children[i], top: false);
            }
            if (bracket) result.Add((ConditionSymbol.Close, null));
        }
    }

    internal static ArrangementCondition Parse(IReadOnlyList<(ConditionSymbol Symbol, ArrangementCondition Clause)> tokens)
    {
        if (tokens.Count == 0) return new ArrangementCondition { Children = new() };
        if (tokens.Count > ArrangementCondition.MaxNodes * 4) return null;

        int at = 0;
        var root = Chain(ConditionSymbol.Or, 0);
        if (root == null || at != tokens.Count) return null;
        if (root.Children == null) root = new ArrangementCondition { Children = new() { root } };
        return WithinLimits(root, 0) && root.NodeCount <= ArrangementCondition.MaxNodes ? root : null;

        ArrangementCondition Chain(ConditionSymbol join, int depth)
        {
            bool any = join == ConditionSymbol.Or;
            ArrangementCondition ReadOperand() => any ? Chain(ConditionSymbol.And, depth) : Unary(depth);
            var left = ReadOperand();
            if (left == null || !Take(join)) return left;
            var group = new ArrangementCondition { Any = any, Children = new() { left } };
            do
            {
                var item = ReadOperand();
                if (item == null) return null;
                group.Children.Add(item);
            } while (Take(join));
            return group;
        }

        ArrangementCondition Unary(int depth)
        {
            if (depth > ArrangementCondition.MaxDepth) return null;
            bool negated = false;
            while (Take(ConditionSymbol.Not)) negated = !negated;
            ArrangementCondition node;
            if (Take(ConditionSymbol.Open))
            {
                node = Chain(ConditionSymbol.Or, depth + 1);
                if (node == null || !Take(ConditionSymbol.Close)) return null;
                if (node.Children == null) node = new ArrangementCondition { Children = new() { node } };
            }
            else
            {
                if (!Take(ConditionSymbol.Clause)) return null;
                node = NamingJson.Copy(tokens[at - 1].Clause);
            }
            if (node != null && negated) node.Negated = !node.Negated;
            return node;
        }

        bool Take(ConditionSymbol symbol)
        {
            if (at >= tokens.Count || tokens[at].Symbol != symbol) return false;
            at++;
            return true;
        }
    }

    private static bool WithinLimits(ArrangementCondition node, int depth)
    {
        if (depth > ArrangementCondition.MaxDepth) return false;
        if (node.Children == null) return node.Field != null;
        foreach (var child in node.Children)
            if (!WithinLimits(child, depth + 1)) return false;
        return true;
    }
}
