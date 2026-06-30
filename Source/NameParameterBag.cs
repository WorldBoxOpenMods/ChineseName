using System.Collections.Generic;

namespace Chinese_Name;

public sealed class NameParameterBag
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _resolving = new();

    public NameGenerationContext Context { get; }

    public NameParameterBag(NameGenerationContext context = null)
        : this(context, null)
    {
    }

    public NameParameterBag(NameGenerationContext context, IDictionary<string, string> values)
    {
        Context = context ?? NameGenerationContextScope.Current ?? new NameGenerationContext();
        _values = values == null ? new Dictionary<string, string>() : new Dictionary<string, string>(values);
    }

    private NameParameterBag(NameGenerationContext context, Dictionary<string, string> values)
    {
        Context = context ?? NameGenerationContextScope.Current ?? new NameGenerationContext();
        _values = new Dictionary<string, string>(values);
    }

    public string this[string key]
    {
        get
        {
            TryGetValue(key, out var value);
            return value;
        }
        set => Set(key, value);
    }

    public NameParameterBag Fork()
    {
        return new NameParameterBag(Context, _values);
    }

    public void Set(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        _values[key] = value ?? string.Empty;
    }

    public bool TryGetValue(string key, out string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = string.Empty;
            return false;
        }

        if (_values.TryGetValue(key, out value))
        {
            return true;
        }

        if (_resolving.Contains(key))
        {
            value = string.Empty;
            return false;
        }

        _resolving.Add(key);
        try
        {
            if (NameParameterProviderRegistry.TryProvide(key, this, out value))
            {
                Set(key, value);
                return true;
            }
        }
        finally
        {
            _resolving.Remove(key);
        }

        value = string.Empty;
        return false;
    }
}
