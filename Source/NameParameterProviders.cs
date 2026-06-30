using System;
using System.Collections.Generic;

namespace Chinese_Name;

public interface INameParameterProvider
{
    bool TryProvide(string key, NameGenerationContext context, NameParameterBag parameters, out string value);
}

public static class NameParameterProviderRegistry
{
    private static readonly List<INameParameterProvider> _providers = new();

    static NameParameterProviderRegistry()
    {
        Register(new BasicNameParameterProvider());
        Register(new EquipmentNameParameterProvider());
    }

    public static void Register(INameParameterProvider provider)
    {
        if (provider == null)
        {
            return;
        }

        _providers.Add(provider);
    }

    public static bool TryProvide(string key, NameParameterBag parameters, out string value)
    {
        var context = parameters?.Context ?? NameGenerationContextScope.Current;
        foreach (var provider in _providers)
        {
            try
            {
                if (provider.TryProvide(key, context, parameters, out value))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                ModClass.LogAllException(e);
            }
        }

        value = string.Empty;
        return false;
    }
}

internal sealed class BasicNameParameterProvider : INameParameterProvider
{
    public bool TryProvide(string key, NameGenerationContext context, NameParameterBag parameters, out string value)
    {
        value = string.Empty;
        if (context == null)
        {
            return false;
        }

        switch (key)
        {
            case "source":
                return TrySet(context.Source, out value);
            case "asset":
            case "asset_id":
            case "name_generator":
                return TrySet(context.VanillaAsset?.id ?? context.ChineseAsset?.id, out value);
            case "unit":
            case "actor":
                return TrySet(context.Actor?.getName(), out value);
            case "city":
                return TrySet(context.Actor != null && context.Actor.hasCity() ? context.Actor.city.name : null, out value);
            case "kingdom":
                if (context.Actor != null && context.Actor.hasKingdom())
                {
                    return TrySet(context.Actor.kingdom.name, out value);
                }
                return TrySet(context.Kingdom?.name, out value);
            case "culture":
                return TrySet(context.Actor != null && context.Actor.hasCulture() ? context.Actor.culture.name : null, out value);
            case "king":
                return TrySet(GetOwnKingName(context.Actor), out value);
            case "enemy_king":
                return TrySet(GetEnemyKingName(context.Actor), out value);
            case "enemy_kingdom":
                return TrySet(GetEnemyKingdomName(context.Actor), out value);
        }

        return false;
    }

    private static string GetOwnKingName(Actor actor)
    {
        if (actor == null || !actor.hasKingdom() || !actor.kingdom.hasKing())
        {
            return null;
        }

        return actor.kingdom.king.getName();
    }

    private static string GetEnemyKingName(Actor actor)
    {
        if (actor == null || actor.kingdom == null || !actor.kingdom.hasEnemies())
        {
            return null;
        }

        using (ListPool<Kingdom> enemyKingdoms = actor.kingdom.getEnemiesKingdoms())
        {
            foreach (Kingdom kingdom in enemyKingdoms.LoopRandom<Kingdom>())
            {
                if (kingdom != null && kingdom.hasKing())
                {
                    var name = kingdom.king.getName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
        }

        return null;
    }

    private static string GetEnemyKingdomName(Actor actor)
    {
        if (actor == null || actor.kingdom == null || !actor.kingdom.hasEnemies())
        {
            return null;
        }

        using (ListPool<Kingdom> enemyKingdoms = actor.kingdom.getEnemiesKingdoms())
        {
            foreach (Kingdom kingdom in enemyKingdoms.LoopRandom<Kingdom>())
            {
                if (!string.IsNullOrEmpty(kingdom?.name))
                {
                    return kingdom.name;
                }
            }
        }

        return null;
    }

    private static bool TrySet(string candidate, out string value)
    {
        value = candidate;
        return !string.IsNullOrEmpty(value);
    }
}

internal sealed class EquipmentNameParameterProvider : INameParameterProvider
{
    public bool TryProvide(string key, NameGenerationContext context, NameParameterBag parameters, out string value)
    {
        value = string.Empty;
        if (context == null)
        {
            return false;
        }

        var asset = context.EquipmentAsset ?? context.Item?.getAsset();
        switch (key)
        {
            case "item":
            case "item_id":
                return TrySet(asset?.id ?? context.Item?.data?.asset_id, out value);
            case "item_mod":
            case "item_mod_id":
                return TrySet(context.ItemModAsset?.id, out value);
            case "material":
                return TrySet(asset?.material ?? context.Item?.data?.material, out value);
            case "type":
            case "equipment_type":
                return TrySet(asset?.equipment_subtype ?? asset?.group_id, out value);
            case "locale":
                return TrySet(GetLocale(context.Item, asset), out value);
        }

        return false;
    }

    private static string GetLocale(Item item, EquipmentAsset asset)
    {
        if (item != null)
        {
            var itemName = item.getName(false);
            if (!string.IsNullOrEmpty(itemName))
            {
                return itemName;
            }
        }

        if (asset == null)
        {
            return null;
        }

        ItemTools.getTooltipTitle(asset, out var name, out var material);
        var result = material + name;
        if (!string.IsNullOrEmpty(result))
        {
            return result;
        }

        return asset.getTranslatedName();
    }

    private static bool TrySet(string candidate, out string value)
    {
        value = candidate;
        return !string.IsNullOrEmpty(value);
    }
}
