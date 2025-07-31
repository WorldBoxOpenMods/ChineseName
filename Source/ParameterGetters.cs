using System.Collections.Generic;

namespace Chinese_Name;

public delegate void ChineseNameParameterGetter(Actor actor, Kingdom kingdom, Dictionary<string, string> parameters);
public static class ParameterGetters
{
    private static readonly Dictionary<string, ChineseNameParameterGetter> _getters =
        new Dictionary<string, ChineseNameParameterGetter>();
    public static void RegisterNewParameterGetter(string id, ChineseNameParameterGetter getter)
    {
        _getters.Add(id, getter);
    }

    public static ChineseNameParameterGetter Get(string id)
    {
        return _getters.TryGetValue(id, out var getter) ? getter : null;
    }
}