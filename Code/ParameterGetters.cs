using System;
using System.Collections.Generic;
using Chinese_Name.constants;
using NeoModLoader.api.attributes;

namespace Chinese_Name;

public static class ParameterGetters
{
    private static Dictionary<string, Action<Actor, Dictionary<string, string>>> actor_parameter_getters = new()
    {
        { "default", default_actor_parameter_getter }
    };

    private static Dictionary<string, Action<City, Dictionary<string, string>>> city_parameter_getters = new()
    {
        { "default", default_city_parameter_getter }
    };

    private static Dictionary<string, Action<Kingdom, Dictionary<string, string>>> kingdom_parameter_getters = new()
    {
        { "default", default_kingdom_parameter_getter }
    };

    private static Dictionary<string, Action<Culture, Dictionary<string, string>>> culture_parameter_getters = new()
    {
        { "default", default_culture_parameter_getter }
    };

    private static Dictionary<string, Action<Clan, Actor, Dictionary<string, string>>> clan_parameter_getters = new()
    {
        { "default", default_clan_parameter_getter }
    };

    private static Dictionary<string, Action<Alliance, Dictionary<string, string>>> alliance_parameter_getters = new()
    {
        { "default", default_alliance_parameter_getter }
    };

    private static Dictionary<string, Action<War, Dictionary<string, string>>> war_parameter_getters = new()
    {
        { "default", default_war_parameter_getter }
    };

    private static Dictionary<string, Action<ItemData, ItemAsset, Actor, Dictionary<string, string>>>
        item_parameter_getters = new()
        {
            { "default", default_item_parameter_getter }
        };
    private static Dictionary<Type, Dictionary<string, Delegate>> custom_parameter_getters = new();
    [Hotfixable]
    private static void default_actor_parameter_getter(Actor pActor, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_city_parameter_getter(City pCity, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_kingdom_parameter_getter(Kingdom pKingdom, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_culture_parameter_getter(Culture pCulture, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_clan_parameter_getter(Clan pClan, Actor pActor, Dictionary<string, string> pParameters)
    {
        pParameters["founder_home"] = string.IsNullOrEmpty(pClan.data.founder_home) ? pClan.data.founder_home : pClan.data.founder_kingdom;
        
        pActor.data.get(DataS.family_name, out var family_name, "无名");
        pParameters["founder_family_name"] = family_name;
    }
    [Hotfixable]
    private static void default_alliance_parameter_getter(Alliance pAlliance, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_war_parameter_getter(War pWar, Dictionary<string, string> pParameters)
    {
        
    }
    [Hotfixable]
    private static void default_item_parameter_getter(ItemData pItemData, ItemAsset pItemAsset, Actor pActor, Dictionary<string, string> pParameters)
    {
        
    }
    public static Action<Actor, Dictionary<string, string>> GetActorParameterGetter(string pName)
    {
        if (actor_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return actor_parameter_getters["default"];
    }
    public static Action<City, Dictionary<string, string>> GetCityParameterGetter(string pName)
    {
        if (city_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return city_parameter_getters["default"];
    }
    public static Action<Kingdom, Dictionary<string, string>> GetKingdomParameterGetter(string pName)
    {
        if (kingdom_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return kingdom_parameter_getters["default"];
    }
    public static Action<Culture, Dictionary<string, string>> GetCultureParameterGetter(string pName)
    {
        if (culture_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return culture_parameter_getters["default"];
    }
    public static Action<Clan, Actor, Dictionary<string, string>> GetClanParameterGetter(string pName)
    {
        if (clan_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return clan_parameter_getters["default"];
    }
    public static Action<Alliance, Dictionary<string, string>> GetAllianceParameterGetter(string pName)
    {
        if (alliance_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return alliance_parameter_getters["default"];
    }
    public static Action<War, Dictionary<string, string>> GetWarParameterGetter(string pName)
    {
        if (war_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return war_parameter_getters["default"];
    }
    public static Action<ItemData, ItemAsset, Actor, Dictionary<string, string>> GetItemParameterGetter(string pName)
    {
        if (item_parameter_getters.TryGetValue(pName, out var getter)) return getter;
        return item_parameter_getters["default"];
    }
    public static T GetCustomParameterGetter<T>(string pName) where T : Delegate
    {
        if(custom_parameter_getters.TryGetValue(typeof(T), out var getters))
        {
            if (getters.TryGetValue(pName, out var getter)) return (T)getter;
            return getters["default"] as T;
        }
        return null;
    }
}