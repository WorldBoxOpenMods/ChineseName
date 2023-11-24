using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
namespace Chinese_Name
{
    public class AllPatch
    {
        public const string patch_id = "inmny.Chinese_Name";
        public static void patch_all()
        {
            Harmony.CreateAndPatchAll(typeof(AllPatch), patch_id);
        }
        public static void unpatch_all()
        {
            Harmony.UnpatchID(patch_id);
        }
        #region 文化
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Culture), nameof(Culture.createCulture))]
        public static void modify_culture_name(Culture __instance, Race pRace, City pCity)
        {
            NameGenerator generator = Main.instance.name_generators.get(pRace.name_template_culture);
            if (generator == null) return;
            __instance.data.name = generator.generate(__instance, pRace, pCity);
        }
        #endregion
        #region 家族
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Clan), nameof(Clan.createClan))]
        public static void modify_clan_name(Clan __instance, Actor pFounder)
        {
            //__instance.data.name = string.Empty;
            if(pFounder==null)Main.log("NOT FOUND FOUNDER");
            NameGenerator generator = Main.instance.name_generators.get(pFounder == null ? SK.elf : pFounder.race.name_template_clan);
            if (generator == null) return;
            __instance.data.name = generator.generate(__instance, pFounder);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Clan), nameof(Clan.getMotto))]
        public static bool set_clan_motto(Kingdom __instance)
        {
            if (!string.IsNullOrEmpty(__instance.data.motto)) return true;
            NameGenerator generator = Main.instance.name_generators.get("clan_mottos");
            if (generator == null) return true;
            __instance.data.motto = generator.generate(__instance);
            return true;
        }
        #endregion
        #region 城市
        [HarmonyPrefix]
        [HarmonyPatch(typeof(City), nameof(City.generateName))]
        public static bool modify_city_name(City __instance)
        {
            NameGenerator generator = Main.instance.name_generators.get(__instance.race.name_template_city);
            if (generator == null) return true;
            __instance.data.name = generator.generate(__instance);
            return false;
        }
        #endregion
        #region 国家
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.addCity))]
        public static bool set_kingdom_name(Kingdom __instance, City pCity)
        {
            if (!string.IsNullOrEmpty(__instance.data.name)) return true;
            NameGenerator generator = Main.instance.name_generators.get(__instance.race.name_template_kingdom);
            if (generator == null) return true;
            __instance.data.name = generator.generate(__instance, pCity);
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getMotto))]
        public static bool set_kingdom_motto(Kingdom __instance)
        {
            if (!string.IsNullOrEmpty(__instance.data.motto)) return true;
            NameGenerator generator = Main.instance.name_generators.get("kingdom_mottos");
            if (generator == null) return true;
            __instance.data.motto = generator.generate(__instance);
            return true;
        }
        #endregion
        #region 联盟
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldLog), nameof(WorldLog.logAllianceCreated))]
        public static bool modify_alliance_name(Alliance pAlliance)
        {
            NameGenerator generator = Main.instance.name_generators.get("alliance_name");
            if (generator == null) return true;
            pAlliance.data.name = generator.generate(pAlliance);
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Alliance), nameof(Alliance.getMotto))]
        public static bool set_alliance_motto(Alliance __instance)
        {
            if (!string.IsNullOrEmpty(__instance.data.motto)) return true;
            NameGenerator generator = Main.instance.name_generators.get("alliance_mottos");
            if (generator == null) return true;
            __instance.data.motto = generator.generate(__instance);
            return true;
        }
        #endregion
        #region 战争
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
        public static void modify_war_name(War __result, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
        {
            if(pDefender != null && pDefender.getAge() <= 1)
            {
                pType = WarTypeLibrary.rebellion;
            }
            NameGenerator generator = Main.instance.name_generators.get(pType.name_template);
            if(generator == null) return;
            
            __result.data.name = generator.generate(__result, pAttacker, pDefender, pType);
            Main.log($"{pType.name_template}:{__result.data.name}");
        }
        #endregion
        #region 生物
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActorBase), nameof(ActorBase.getName))]
        public static bool set_actor_name(ActorBase __instance)
        {
            if (!string.IsNullOrEmpty(__instance.data.name)) return true;
            NameGenerator generator = Main.instance.name_generators.get(__instance.asset.nameTemplate);
            if(generator == null) return true;
            __instance.data.name = generator.generate(__instance);
            return true;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Clan), nameof(Clan.addUnit))]
        public static void set_actor_family_name(Clan __instance, Actor pActor)
        {
            string tmp = "";
            foreach(Actor unit in __instance.units.Values)
            {
                unit.data.get(Main.family_name, out tmp, "");
                if(!string.IsNullOrEmpty(tmp))
                {
                    pActor.data.set(Main.family_name, tmp);
                    return;
                }
            }
            foreach(Actor unit in __instance.units.Values)
            {
                NameGenerator generator = Main.instance.name_generators.get(unit.asset.nameTemplate);
                if(generator!=null){ generator.generate(unit); unit.data.get(Main.family_name, out tmp, "");}
                
                pActor.data.set(Main.family_name, tmp);
                return;
            }
        }
        #endregion
        #region 装备
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemGenerator),nameof(ItemGenerator.checkModName))]
        public static bool set_item_name(ref ItemData pItemData, ItemAsset pModAsset, ItemAsset pItemAsset, ActorBase pActor)
        {
            if (pModAsset.quality == ItemQuality.Legendary)
            {
                string name = null;
                int num = 0;
                while (string.IsNullOrEmpty(name) || ItemGenerator.unique_legendary_names.Contains(name))
                {
                    string randomNameTemplate = pItemAsset.getRandomNameTemplate(pActor);
                    NameGenerator generator = Main.instance.name_generators.get(randomNameTemplate);

                    if (generator == null) return true;

                    name = generator.generate(pItemData, pActor);
                    if (++num > 100)
                    {
                        ItemGenerator.unique_legendary_names.Clear();
                    }
                }
                pItemData.name = name;
                ItemGenerator.unique_legendary_names.Add(name);
            }
            return false;
        }
        #endregion
    }
}
