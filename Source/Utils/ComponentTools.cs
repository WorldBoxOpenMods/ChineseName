using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Chinese_Name.Utils;

public static class ComponentTools
{
    public static TComponent Get<TComponent>(this object obj) where TComponent : class, new()
    {
        if (obj == null) return null;
        return ComponentContainer<TComponent>.Get(obj);
    }
    public static void Set<TComponent>(this object obj, TComponent component) where TComponent : class, new()
    {
        if (obj == null) return;
        ComponentContainer<TComponent>.Set(obj, component);
    }
    public static bool Has<TComponent>(this object obj) where TComponent : class, new()
    {
        if (obj == null) return false;
        return ComponentContainer<TComponent>.Has(obj);
    }
    public static void Remove<TComponent>(this object obj) where TComponent : class, new()
    {
        if (obj == null) return;
        ComponentContainer<TComponent>.Remove(obj);
    }

    private static class ComponentContainer<TComponent> where TComponent : class, new()
    {
        private static readonly ConditionalWeakTable<object, TComponent> Container = new();
        public static void Remove(object obj)
        {
            Container.Remove(obj);
        }
        public static void Set(object obj, TComponent component)
        {
            Container.Remove(component);
            Container.Add(obj, component);
        }
        public static bool Has(object obj)
        {
            return Container.TryGetValue(obj, out _);
        }
        public static TComponent Get(object obj, bool create = true)
        {
            if (!Container.TryGetValue(obj, out var component))
            {
                if (!create) return null;
                component = new TComponent();
                Container.Add(obj, component);
            }
            return component;
        }
    }
}