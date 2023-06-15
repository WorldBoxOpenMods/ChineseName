using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chinese_Name
{
    public static class NameGenerateFuncs
    {
        private static StringBuilder string_builder = new StringBuilder();
        public static string default_generator(NameGenerator generator, object[] @params)
        {
            return generator.get_random_template().get_template();
        }
        /// <summary>
        /// 智慧生物的名字生成器
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="params">0: Actor</param>
        /// <returns></returns>
        public static string unit_name_generator(NameGenerator generator, object[] @params)
        {
            string_builder.Clear();
            NameGenerateTemplate template = generator.get_random_template();

            Actor actor = (Actor)@params[0];
            int i; string tmp; string id;
            for (i = 0; i < template.list.Count; i++)
            {
                id = template.list[i].id;
                if (id.Contains("family") || id.Contains("clan"))
                {
                    actor.data.get(Main.family_name, out tmp, "");
                    if (string.IsNullOrEmpty(tmp))
                    {
                        tmp = template.get_part_step_by_step(i);
                        actor.data.set(Main.family_name, tmp);
                    }
                    string_builder.Append(tmp);
                }
                else
                {
                    string_builder.Append(template.get_part_step_by_step(i));
                }
            }

            return string_builder.ToString();
        }
        /// <summary>
        /// 氏族名字生成器
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="params">0: Clan, 1: Actor(null)</param>
        /// <returns></returns>
        public static string clan_name_generator(NameGenerator generator, object[] @params)
        {
            string_builder.Clear();
            if (@params[1] == null)
            {
                return generator.templates[0].get_template();
            }
            NameGenerateTemplate template = generator.get_random_template();
            Clan clan = (Clan)@params[0];
            Actor actor = (Actor)@params[1];
            bool has_city = !string.IsNullOrEmpty(clan.data.founder_home);

            int i; string tmp; string _tmp;
            for (i = 0; i < template.list.Count; i++)
            {
                tmp = template.get_part_step_by_step(i);
                if(tmp.Contains("$founder_home$"))
                {
                    tmp = tmp.Replace("$founder_home$", has_city ? clan.data.founder_home : clan.data.founder_kingdom);
                }
                actor.data.get(Main.family_name, out _tmp, "");
                if (string.IsNullOrEmpty(_tmp)) _tmp = actor.data.name[0].ToString();
                tmp = tmp.Replace("$founder_family_name$", _tmp);
                string_builder.Append(tmp);
            }
            return string_builder.ToString();
        }
        /// <summary>
        /// 联盟名字生成器 
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="params">0: Alliance</param>
        /// <returns></returns>
        public static string alliance_name_generator(NameGenerator generator, object[] @params)
        {
            Alliance alliance = (Alliance)@params[0];
            return generator.get_random_template().get_template()
                .Replace("$alliance_kingdom_union_name$", get_alliance_kingdom_union_name(alliance))
                .Replace("$alliance_pos$", get_alliance_pos(alliance))
                .Replace("$alliance_kingdom_name$", alliance.kingdoms_list.GetRandom().data.name)
                .Replace("$age$", LocalizedTextManager.getText(World.world.eraManager.getCurrentEra().id+ "_title").Replace("纪元", "").Replace("時代",""));
        }
        private static readonly string[] region_word_library_ids = new string[] { "tile_ground_names", "tile_ocean_names", "tile_lava_names", "tile_block_names", "tile_goo_names" };
        private static string get_alliance_pos(Alliance alliance)
        {
            Vector3 pos = Vector3.zero;
            foreach(Kingdom kingdom in alliance.kingdoms_hashset)
            {
                pos += kingdom.location;
            }
            pos /= alliance.kingdoms_hashset.Count;
            WorldTile pos_tile = World.world.GetTile((int)(pos.x + 0.5f), (int)(pos.y + 0.5f));

            WordLibrary word_library;
            if(pos_tile == null)
            {
                Main.log($"NULL POS:({(int)(pos.x + 0.5f)},{(int)(pos.y + 0.5f)})");
            }
            Main.log($"To get word library:'{"tile_" + pos_tile.cur_tile_type.id.Replace("_low", "").Replace("_high", "")+"_names"}'");

            if ((word_library = Main.instance.word_libraries.get("tile_"+pos_tile.cur_tile_type.id.Replace("_low","").Replace("_high", "") + "_names")) == null)
            {
                word_library = Main.instance.word_libraries.get(region_word_library_ids[(int)pos_tile.region.type]);
            }
            return word_library.get_random();
        }
        private static string get_alliance_kingdom_union_name(Alliance alliance)
        {
            Main.log($"Alliance Kingdoms Count: {alliance.kingdoms_list.Count}");
            if (alliance.kingdoms_list.Count < 2)
            {
                return alliance.kingdoms_list[0].capital.name;
            }
            Kingdom kingdom_1 = alliance.kingdoms_list[0];
            Kingdom kingdom_2 = alliance.kingdoms_list[1];
            Main.log($"Kingdom '{kingdom_1.name}' and Kingdom '{kingdom_2.name}'");
            if(kingdom_1.king!=null && kingdom_2.king!=null && Toolbox.randomBool())
            {
                string tmp_1, tmp_2;
                kingdom_1.king.data.get(Main.family_name, out tmp_1, kingdom_1.name[0].ToString());
                kingdom_2.king.data.get(Main.family_name, out tmp_2, kingdom_2.name[0].ToString());
                return tmp_1 + tmp_2;
            }
            return new String(new char[] { kingdom_1.data.name[0], kingdom_2.data.name[0] });
        }
        /// <summary>
        /// 战争名字生成器
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="params">0: War, 1: Kingdom(Attacker), 2: Kingdom(Defender), 3: WarTypeAsset(Type)</param>
        /// <returns></returns>
        public static string war_name_generator(NameGenerator generator, object[] @params)
        {
            Kingdom attacker = (Kingdom)@params[1];
            Kingdom defender = (Kingdom)@params[2];
            if (defender == null)
            {
                return generator.templates[0].get_template();
            }
            WarTypeAsset war_type = (WarTypeAsset)@params[3];
            string template = generator.get_random_template().get_template();
            // 虽然这么写很暴力，但是抛开剂量谈毒性纯nt
            return template
                .Replace("$war_defender_kingdom_name_abb$", get_kingdom_name_abb(defender))
                .Replace("$war_attacker_kingdom_name_abb$", get_kingdom_name_abb(attacker))
                .Replace("$war_defender_kingdom_name$", defender.name)
                .Replace("$war_attacker_kingdom_name$", attacker.name)
                .Replace("$war_kingdom_union_name$", get_kingdom_name_abb(attacker) + get_kingdom_name_abb(defender))
                .Replace("$war_defender_capital_name$", 
                defender.capital==null?
                    (defender.king==null || defender.king.city == null ? 
                        (defender.cities.Count == 0 ? 
                            defender.name 
                            : defender.cities[0].name)
                        :defender.king.city.name)
                    :defender.capital.name)
                .Replace("$war_defender_leader_name$", 
                (defender.king == null) ? 
                    (defender.capital==null || defender.capital.leader==null ? 
                        (defender.cities.Count==0 || defender.cities[0].leader==null ? 
                            defender.name 
                            : defender.cities[0].leader.data.name)
                        :defender.capital.leader.data.name) 
                    : defender.king.data.name)
                .Replace("$age$", LocalizedTextManager.getText(World.world.eraManager.getCurrentEra().id+ "_title").Replace("纪元", "").Replace("時代",""));
        }

        private static string get_kingdom_name_abb(Kingdom defender)
        {
            return defender.name[0].ToString();
        }
    }
}
