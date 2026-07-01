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
        Register(new SubspeciesNameParameterProvider());
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
            case "id":
            case "actor_asset":
            case "actor_asset_id":
                return TrySet(GetActorAsset(context)?.id ?? context.VanillaAsset?.id ?? context.ChineseAsset?.id, out value);
            case "asset":
            case "asset_id":
            case "name_generator":
                return TrySet(context.VanillaAsset?.id ?? context.ChineseAsset?.id, out value);
            case "locale":
                return TrySet(GetActorLocale(context), out value);
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

    private static ActorAsset GetActorAsset(NameGenerationContext context)
    {
        return context?.ActorAsset ?? context?.Actor?.asset;
    }

    private static string GetActorLocale(NameGenerationContext context)
    {
        var asset = GetActorAsset(context);
        if (asset == null)
        {
            return null;
        }

        if (TryGetLocalizedText(asset.getLocaleID(), out var localizedName))
        {
            return localizedName;
        }

        return asset.id;
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

    private static bool TryGetLocalizedText(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            if (!LocalizedTextManager.stringExists(key))
            {
                return false;
            }

            value = LocalizedTextManager.getText(key, null, false);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
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
        if (!string.IsNullOrEmpty(item?.data?.name))
        {
            return item.data.name;
        }

        if (asset == null)
        {
            return null;
        }

        if (TryGetLocalizedText(asset.getLocaleID(), out var localizedName))
        {
            return localizedName;
        }

        return asset.equipment_subtype ?? asset.id;
    }

    private static bool TryGetLocalizedText(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            if (!LocalizedTextManager.stringExists(key))
            {
                return false;
            }

            value = LocalizedTextManager.getText(key, null, false);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static bool TrySet(string candidate, out string value)
    {
        value = candidate;
        return !string.IsNullOrEmpty(value);
    }
}

internal sealed class SubspeciesNameParameterProvider : INameParameterProvider
{
    public bool TryProvide(string key, NameGenerationContext context, NameParameterBag parameters, out string value)
    {
        value = string.Empty;
        if (context == null)
        {
            return false;
        }

        var actorAsset = context.ActorAsset ?? context.Subspecies?.getActorAsset();
        var tileType = context.Tile?.Type;
        var biomeAsset = context.BiomeAsset ?? (tileType != null && tileType.is_biome ? tileType.biome_asset : null);

        switch (key)
        {
            case "subspecies":
            case "subspecies_name":
                return TrySet(context.Subspecies?.name, out value);
            case "species":
            case "race":
            case "race_name":
                return TrySet(GetSpeciesName(actorAsset), out value);
            case "species_id":
            case "race_id":
                return TrySet(actorAsset?.id ?? context.Subspecies?.species_id, out value);
            case "taxonomic_genus":
                return TrySet(actorAsset?.name_taxonomic_genus, out value);
            case "taxonomic_species":
                return TrySet(actorAsset?.name_taxonomic_species, out value);
            case "biome":
            case "biome_name":
            case "biome_prefix":
                return TrySet(GetBiomeName(biomeAsset), out value);
            case "biome_suffix":
                return TrySet(GetBiomeSuffix(biomeAsset), out value);
            case "biome_id":
                return TrySet(biomeAsset?.id ?? tileType?.biome_id, out value);
            case "biome_variant":
                return TrySet(context.Subspecies?.data?.biome_variant ?? tileType?.biome_id, out value);
            case "subspecies_index":
                return TrySet(actorAsset?.countSubspecies().ToString(), out value);
        }

        return false;
    }

    private static string GetSpeciesName(ActorAsset actorAsset)
    {
        if (actorAsset == null)
        {
            return null;
        }

        if (TryGetLocalizedText($"subspecies_species_{actorAsset.id}", out var configuredName))
        {
            return configuredName;
        }

        if (TryGetLocalizedText(actorAsset.getLocaleID(), out var localizedName))
        {
            return localizedName;
        }

        return null;
    }

    private static string GetBiomeName(BiomeAsset biomeAsset)
    {
        if (biomeAsset == null)
        {
            return null;
        }

        return GetWordLibraryValue($"subspecies_biome_{biomeAsset.id}") ??
               GetWordLibraryValue("subspecies_biome_default");
    }

    private static string GetBiomeSuffix(BiomeAsset biomeAsset)
    {
        if (biomeAsset == null)
        {
            return null;
        }

        return GetWordLibraryValue($"subspecies_biome_suffix_{biomeAsset.id}") ??
               GetWordLibraryValue("subspecies_biome_suffix_default") ??
               GetBiomeName(biomeAsset);
    }

    private static string GetWordLibraryValue(string libraryId)
    {
        if (string.IsNullOrEmpty(libraryId))
        {
            return null;
        }

        return WordLibraryLibrary.Instance.get(libraryId)?.GetRandom();
    }

    private static bool TryGetLocalizedText(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            if (!LocalizedTextManager.stringExists(key))
            {
                return false;
            }

            value = LocalizedTextManager.getText(key, null, false);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static bool TrySet(string candidate, out string value)
    {
        value = candidate;
        return !string.IsNullOrEmpty(value);
    }
}
