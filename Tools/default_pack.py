#!/usr/bin/env python3
"""Build the bundled v1 pack from the curated corpus and structured rules."""
import argparse
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "ChineseNamePackages/word_libraries/default"
DEST = ROOT / "Content/default.naming.json"
pack = {"format": 1, "id": "chinesename:classic", "label": "华语地名与人名", "humanCultureDefault": True,
        "credits": ["一米（Inmny） · 原作与现版词库整理"],
        "poolCredits": {
            "chinesename:surname": ["《百家姓》 · 传统姓氏资料", "daybreak1929 · 旧版词表增补"],
            "chinesename:places": ["daybreak1929 · 旧版词表增补"],
            "chinesename:countries": ["daybreak1929 · 旧版词表增补"],
            "chinesename:boat": ["daybreak1929 · 旧版词表增补"],
        },
        "rules": {}, "words": [], "pools": {}, "poolLabels": {}, "wordGroups": {}}


def words(path):
    return (CORPUS / (path + ".txt")).read_text(encoding="utf-8-sig").splitlines()


def pool(name, entries, label=None):
    ref = f"chinesename:{name}"
    candidates = pack["pools"][ref] = {}
    if label:
        pack["poolLabels"][ref] = label
    for entry in dict.fromkeys(entries):
        text, chinese = entry if isinstance(entry, tuple) else (entry, None)
        text = text.strip()
        if not text or any(ord(c) < 32 for c in text):
            continue
        # A stable ID is independent of insertion and changes only when the word does.
        key = f"chinesename:{name}." + "-".join(f"{ord(c):x}" for c in text)
        if len(key.split(":")[1]) > 64:
            import hashlib
            key = f"chinesename:{name}." + hashlib.sha256(text.encode()).hexdigest()[:20]
        if key not in candidates:
            candidates[key] = 1
            pack["words"].append({"id": key, "text": text, **({"chinese": chinese} if chinese else {})})
    return ref


def part(id, ref, **options):
    return {"id": id, "pool": ref, **options}


def text(id, value):
    return {"id": id, "literal": value}


def bilingual(entries):
    return [tuple(entry.split("=")) for entry in entries.split("|")]


def word_group(id, ref, terms):
    terms = set(terms)
    members = [word["id"] for word in pack["words"]
               if word["id"] in pack["pools"][ref] and word["text"] in terms]
    if not members:
        raise ValueError(f"Empty word group: {id}")
    pack["wordGroups"][id] = {"pool": ref, "words": members}


def rule(type_id, id, parts, priority=0, starts_with=None, use_native_word_groups=False, **conditions):
    value = {"id": id, "parts": parts}
    if priority:
        value["priority"] = priority
    if conditions:
        value["when"] = conditions
    if starts_with:
        value["whenStartsWith"] = starts_with
    if use_native_word_groups:
        value["useNativeWordGroups"] = True
    pack["rules"].setdefault("worldbox:" + type_id, []).append(value)


surname = pool("surname", words("百家姓") + ["欧阳", "司马", "上官"], "常见中文姓氏")
first = pool("given-first", list("青明清景怀静知远安修子云临嘉承书含溪若宜和映望素长宁星雨晏春竹松行向归南闻江砚岚"), "中文给名首字")
last = pool("given-last", list("川山文宁霖秋羽衡岚舟深川远白歌安真恒尘渊华阳初庭野竹书月明朗泽林石心光荷容信恩"), "中文给名末字")
rule("person", "sapient", [part("surname", surname), part("first", first),
                          part("given", last, optional=True, chance=.84)], 10, kind="sapient")
english_given = bilingual("John=约翰|Mary=玛丽|Alice=艾丽斯|Arthur=亚瑟|Clara=克拉拉|Edward=爱德华|Eleanor=埃莉诺|Emily=艾米莉|George=乔治|Henry=亨利|Jane=简|Lucy=露西|Oliver=奥利弗|Robin=罗宾|William=威廉")
french_given = bilingual("Jean=让|Marie=玛丽|Pierre=皮埃尔|Camille=卡米耶|Claire=克莱尔|Émile=埃米尔|Élise=埃莉丝|Henri=亨利|Jacques=雅克|Julien=朱利安|Louise=路易丝|Luc=吕克|Madeleine=玛德莱娜|René=勒内|Sophie=索菲")
english_surnames = bilingual("Smith=史密斯|Miller=米勒|Brown=布朗|Clarke=克拉克|Cooper=库珀|Hill=希尔|Reed=里德|Taylor=泰勒|Walker=沃克|Ward=沃德|Wells=韦尔斯|Wood=伍德")
french_surnames = bilingual("Martin=马丁|Dupont=迪蓬|Bernard=贝尔纳|Dubois=迪布瓦|Fournier=富尼耶|Laurent=洛朗|Lefèvre=勒费弗尔|Moreau=莫罗|Petit=珀蒂|Rousseau=卢梭|Roux=鲁|Simon=西蒙")
western_given = pool("western-given", english_given + french_given + [("Ainsley", "安斯利")], "西式给名")
western_surname = pool("western-surname", english_surnames + french_surnames, "西式姓氏")
western_middle = pool("western-middle", bilingual("James=詹姆斯|Anne=安娜|Rose=罗丝|Grace=格蕾丝|Louis=路易|Paul=保罗|Hélène=埃莱娜|Noël=诺埃尔"), "西式中间名")
for kind, ref, pairs in [
    ("english-given", western_given, english_given), ("french-given", western_given, french_given),
    ("english-surnames", western_surname, english_surnames), ("french-surnames", western_surname, french_surnames),
]:
    word_group(kind, ref, (original for original, _ in pairs))
rule("person", "western", [part("given", western_given), part("surname", western_surname)], kind="sapient")
pack["rules"]["worldbox:person"][-1].update(presetOnly=True, writing="Chinese",
                                             chineseSeparator="·", originalSeparator=" ")
for species, terms in {
    "elf": "风栖 星汀 月弦 木眠 鹿语 岚羽 藤灯 银叶 流萤 霜蕨",
    "orc": "獠牙 岩吼 铁颚 雷蹄 黑棘 凿岩 猛脊 断岳 红獠 碎骨",
    "dwarf": "石炉 铜砧 錾星 金铎 铸岩 玄锤 红铁 炉心 钻岩 铁镐",
}.items():
    rule("person", species, [text("surname", ""), part("given", pool(species, terms.split()))],
         30, species=species, kind="sapient")

creatures = {}
for file in sorted((CORPUS / "other_creatures").glob("*名号.txt")):
    species = file.name.removesuffix("名号.txt")
    if not re.fullmatch("[a-z][a-z0-9_]*", species):
        continue
    # Split into independently selected parts. The name corpus supplies the species suffix.
    prefixes = words(f"other_creatures/{species}前缀")
    terms = words(f"other_creatures/{species}名号")
    if not any(term.strip() for term in terms):
        continue
    core = pool(f"mob-{species}", terms)
    creatures[species] = core
    leading = pool(f"lead-{species}", prefixes)
    rule("person", f"mob-{species}", [text("surname", ""), part("first", leading, optional=True, chance=.52),
                                     part("given", core)], 20, species=species, kind="creature")
boat = pool("boat", words("mobs/船只名"))
rule("person", "boat", [text("surname", ""), part("given", boat), text("suffix", "号")], 10, kind="boat")
for species, suffix in {"boat_fishing": "渔舟", "boat_transport": "渡船", "boat_trading": "商舶", "battle_boat": "战舰"}.items():
    rule("person", species.replace("_", "-"), [text("surname", ""), part("given", boat), text("suffix", suffix)],
         30, kind="boat", species=species)

for species, terms in {
    "dragon": "烛鳞 赤霄 玄渊 烬云 苍焰 盘岳 逐风",
    "unicorn": "雪鬃 月角 晨星 云蹄 银穗 雾行",
    "crabzilla": "裂岸钳 镇潮甲 伏浪王 碎礁巨螯 挽海甲",
    "dog": "旺财 雪团 小黄 栗子 花鼻 黑豆 巷尾 风耳",
    "ostrich": "长腿 沙跑 风翎 旱地飞奔 鸵影",
    "seal": "浪团 斑雪 冰须 海绒 白肚",
    "bee": "采蜜 金翅 杏花 针尾 花铃",
    "beetle": "铜甲 黑豆 树铠 翠壳 铁背",
    "butterfly": "粉翅 素绢 云蝶 残霞 银斑",
    "fly": "灰翅 旋针 浮尘 麻背",
    "grasshopper": "跃芒 草笛 青足 露跳 秋鸣",
    "sand_spider": "沙织 沙潜 八砂 尘网",
    "flower_bud": "花信 茸苞 晨蕊 枝心",
    "lemon_snail": "酸壳 檬涡 慢露 青檬",
    "smore": "焦糖 糖芯 炉绒 甜云",
    "garl": "蒜衣 白瓣 辛芽 霜头",
    "printer": "复写者 铸像机 拓影炉 墨核",
    "UFO": "星巡 银碟 天梭 光轮 远炬",
    "god_finger": "天指 神触 金印 天痕",
    "druid": "苔衣 枝杖 林客 木誓 森谕",
    "plague_doctor": "乌喙 灰诊 夜药 归烬 白炉",
    "worm": "土游 泥线 砂环 地潜",
    "living_plants": "攀枝 根行 青蔓 叶语",
}.items():
    ref = pool("creature-" + species.lower(), terms.split())
    creatures[species] = ref
    rule("person", "creature-" + species.lower(), [text("surname", ""), part("given", ref)], 24, kind="creature", species=species)

for prefix, terms in {
    "zombie": "迟行 冷骨 灰眸 残衣 暮影 空舌 陈灰 骨铃",
    "skeleton": "霜髓 白刃 枯骸 寒骨 骸灯 骨牙",
    "mush_": "菌冠 菇伞 孢夜 菌斑 灰蕈",
    "tumor_": "赤蚀 溃芽 病核 徒长 肿脉",
    "fire_elemental": "焰落 明烬 火魄 炉灵 赤星 浮焱",
    "ant_": "细颚 链行 巢卫 玄足 蚁锋",
}.items():
    group = prefix.rstrip("_").replace("_", "-")
    ref = pool("group-" + group, terms.split())
    rule("person", "group-" + group, [text("surname", ""), part("given", ref)], 16,
         starts_with={"species": prefix})

for species, ref in creatures.items():
    if species == species.lower():
        rule("person", "civ-" + species, [text("surname", ""), part("given", ref), part("tail", last, optional=True, chance=.30)],
             22, kind="sapient", species="civ_" + species)
rule("person", "uplifted", [text("surname", ""), part("first", first), part("given", last)], 16,
     starts_with={"species": "civ_"}, kind="sapient")

rule("person", "creature", [text("surname", ""), part("given", pool("nickname",
     "团子 阿灰 小栗 圆圆 花尾 青团 点点 阿圆 小风 暖暖 松果 小团".split()))])

places = pool("places", words("真实城名"), "完整地名")
river_roots = "澄江 临川 云津 南浦 烟浦 清沅 平津 鹭汀 芦汀 兰溪 栖鹤渡 青溪 石梁 洛川 鹿溪 清漳 双溪 浣溪".split()
hill_roots = "栖岭 白岳 景山 南陵 松岭 青岩 北岳 鹿岭 松岩 翠屏 金岫 石门 龙脊 云岫 鹿原 翠峰 青崖 翠谷".split()
coastal_roots = "潮生 东湾 白湾 青湾 望潮 听潮 海陵 浦阳 白沙 鹭洲 鹿屿 长屿 银沙 沧屿 漱潮 灵屿 椰汀 霞湾".split()
frontier_roots = "朔原 安边 锁云 镇朔 靖边 归塞 北塞 鹿塞 怀朔 逐风 雁门 驻云 白垣 凉原 望塞 北原 定远 长垣".split()
place_root = pool("place-root", river_roots + hill_roots + coastal_roots + frontier_roots + ["建安"], "地名主体")
place_suffix = pool("place-suffix", "府 州 关 城 县 镇 邑".split(), "城市后缀")
river_places = pool("river-places", river_roots, "江河地名")
# Preserve the old named settlement and its word ID for existing language edits.
hill_places = pool("hill-places", hill_roots + ["崖州"], "山岭地名")
coastal_places = pool("coastal-places", coastal_roots, "沿海地名")
frontier_places = pool("frontier-places", frontier_roots, "边塞地名")
pack["poolAliases"] = {
    "地名": places,
    "完整地名": places,
    "地名主体": place_root,
    "江河地名": river_places,
    "山岭地名": hill_places,
    "沿海地名": coastal_places,
    "港口地名": coastal_places,
    "边塞地名": frontier_places,
    "关塞地名": frontier_places,
    "城市后缀": place_suffix,
    "姓氏": surname,
    "给名首字": first,
    "给名尾字": last,
    "西式给名": western_given,
    "西式姓氏": western_surname,
    "西式中间名": western_middle,
    "船只名": boat,
}
pack["cultureVariants"] = {
    "short-personal-names": {"type": "worldbox:person", "rule": "sapient", "part": "given", "chance": .53},
    "long-personal-names": {"type": "worldbox:person", "rule": "sapient", "part": "given", "chance": .95},
    "vernacular-language-names": {"type": "worldbox:language", "rule": "default", "part": "suffix", "literal": "话"},
    "hall-family-names": {"type": "worldbox:family", "rule": "default", "part": "suffix", "literal": "堂"},
    "tradition-religion-names": {"type": "worldbox:religion", "rule": "default", "part": "suffix", "literal": "道"},
    "gazetteer-book-names": {"type": "worldbox:book", "rule": "history", "part": "suffix", "literal": "志"},
}
for key, ref, match in [
    ("clear-given-names", first, "青明清景映素星雨晏春竹松"),
    ("quiet-given-names", first, "怀静知远安修临嘉承书宁"),
    ("river-places", places, "江河川津泽浦湖海溪"),
    ("hill-places", places, "山岭峰岳岩谷原崖陵"),
    ("root-waterways", place_root, "江川津浦溪汀"),
    ("root-inland", place_root, "岭安岳原"),
    ("river-streams", river_places, "江川沅溪漳"),
    ("river-fords", river_places, "津浦汀渡"),
    ("hill-ridges", hill_places, "岭岳山岫峰"),
    ("hill-settlements", hill_places, "州陵岩门原"),
    ("coastal-bays", coastal_places, "潮湾沙浦"),
    ("coastal-islands", coastal_places, "屿洲汀"),
    ("frontier-ramparts", frontier_places, "垣门塞"),
    ("frontier-steppes", frontier_places, "原朔边"),
    ("city-administrative-endings", place_suffix, "府州县邑"),
    ("city-fortified-endings", place_suffix, "关城镇"),
]:
    candidates = [entry["id"] for entry in pack["words"]
                  if entry["id"] in pack["pools"][ref] and any(c in match for c in entry["text"])]
    if candidates:
        pack["wordGroups"][key] = {"pool": ref, "words": candidates}
countries = pool("countries", words("真实国名"), "完整国名")
country_root = pool("country-root", "云津 松岭 丹阳 河西 拱辰 澄江 北辰 南川 江陵 长宁 云岚 望川 岚原 照海 扶风 南衡 青岫 澄海 阳朔 朔原 瑞庭 玉垣".split(), "国号主体")
country_ending = pool("country-ending", "国 王国 邦 公国 帝国 诸邦".split(), "国号后缀")
pack["poolAliases"].update({"国名": countries, "国号主体": country_root, "国号后缀": country_ending})
for key, terms in [("river-countries", ("云津", "澄江", "江陵", "南川")),
                   ("inland-countries", ("松岭", "丹阳", "河西", "北辰"))]:
    pack["wordGroups"][key] = {"pool": country_root, "words": [
        entry["id"] for entry in pack["words"] if entry["id"] in pack["pools"][country_root] and entry["text"] in terms
    ]}

# Older packs and saved rules can still refer to this pool by ID.
pool("land", "长风 岚泽 朝云 沧溟 白石 云川 漱月 太微 雁回 河洛 星落 松风 玄原 照野 春潮 南岭 归鸿 霁月 白露 朔风 紫云 野渡 寒江 重明 赤霞".split(), "通用号名")
army_ending = pool("army-formation", "军 卫 营 团".split(), "军队编制")
species_traits = pool("subspecies-trait", [word for word in words("亚种生态特征") if word != "高地"], "亚种生态特征")
species_ending = pool("subspecies-ending", words("亚种支系后缀"), "亚种支系后缀")
pack["poolAliases"].update({"军队编制": army_ending, "亚种生态特征": species_traits, "亚种支系后缀": species_ending})
for name, (label, stems, suffix) in {
    "culture": ("文化名主体", "河洛 江渚 百工 旷原 立木 海桑 山泽 牧野 礼耕 舟居 听潮 制陶 煮盐 引水 拓垦 羽织 茶陇 山居 炉铸 栖谷", "之风"),
    "language": ("语言名主体", "吴越 南音 河汊 北原 海汀 峡川 鹿鸣 桑谷 秦川 朔庭 东湾 芦汀 山阴 雁北 关西 隔水 雨林 白沙 临川 长屿", "语"),
    "religion": ("宗教名主体", "奉火 观星 归真 崇山 守泉 敬日 敬月 听潮 司禾 明心 续灯 持烛 星誓 青坛 逐光 玄水 山灵 守誓 长明 净泉", "教"),
    "family": ("家族名主体", "松庭 积善 耕读 三槐 杏林 怀远 承礼 双桂 继明 守拙 承荫 永叙 崇德 归耕 听松 敦仁 静和 安棠 栖梧 晋修", "一门"),
    "clan": ("氏族名主体", "黑石 赤松 青竹 长弓 河源 北原 松岭 江湾 云岫 临川 白垣 长堤 同舟 礼耕 守约 继明 承志 怀远 崇德 敦仁", "氏族"),
    "army": ("军队名主体", "朔骑 镇海 羽林 平野 护城 玄甲 靖边 赤旗 磐石 飞骑 崤关 河防 云骑 鹰旗 白旄 鹿野 息戈 雁门 守望 照夜 镇关 长锋 玄戟 青旌 卫河 守隘 横江 凌霄", "军"),
    "subspecies": ("亚种名主体", "高地 平原 苔原 林栖 沙地 潮汐 穴居 河谷 山麓 沼泽", "支系"),
    "alliance": ("联盟名主体", "三川 四水 山海 北境 归舟 东陆 守土 同心 长堤 诸城 河谷 海门 长垣 青原 北辰 白塔 星火 扶风 连山 同舟", "盟"),
    "world": ("世界名主体", "九野 云陆 瀚海 万屿 天衡 星河 洪荒 苍梧 八荒 远洲 朝晖 连海 邃境 长昼 星陲 四野 群岛 玄原 泽国 云山", "界"),
    "war": ("战争地名", "河口 渡口 断桥 白石关 松岭 北境 长堤 灰岩堡 东海 古道", "之战"),
    "item": ("装备名", "拂晓 寒芒 照雪 云阙 岁星 断潮 藏锋 溪石 龙渊 清霜 赤羽 长明 听雷 昭光 逐月 云铠 流火", ""),
    "book": ("通用书名主体", "州郡 山川 岁时 百物 远游 草木 异闻 旧事 四海 列国", "志"),
    "player": ("玩家号名主体", "玄穹 太初 观潮 三光 守望 执衡 开阳 临渊 扶桑 望舒 九章 持灯 定川 司晨 观山 揽星 晏清 守夜 牧云 问天", "君"),
}.items():
    pool_id = "equipment" if name == "item" else name + "-name"
    ref = pool(pool_id, stems.split(), label)
    pack["poolAliases"][label] = ref
    parts = [part("name", ref)]
    if name == "family":
        parts = [{"id": "name", "input": "founder-surname", "fallbackPool": ref}, text("suffix", suffix)]
    elif name == "army":
        parts.append(part("suffix", army_ending))
    elif name == "subspecies":
        parts += [part("trait", species_traits, optional=True, chance=.65), part("suffix", species_ending)]
    else:
        parts.append(text("suffix", suffix))
    rule(name, "default", parts)

for id, label, terms in [
    ("alliance-emblems", "联盟象征", words("联盟象征")),
    ("alliance-ideals", "联盟理念", words("联盟理念")),
    ("alliance-ending", "联盟后缀", words("联盟名后缀")),
    ("complete-alliances", "完整联盟名", words("联盟名号")),
    ("personal-names", "完整中文给名", "静姝 明远 景行 安澜 望舒 青岚 正则 修远 怀瑾 知新 照雪 云舟 听竹 宁秋 雨棠 含章".split()),
]:
    pack["poolAliases"][label] = pool(id, terms, label)
rule("alliance", "founding-pair", [
    {"id": "founder-short", "input": "founder-short"},
    {"id": "partner-short", "input": "partner-short"},
    part("suffix", "chinesename:alliance-ending"),
], 5, kind="founding-pair", use_native_word_groups=True)
for id, ref, members in [
    ("river-cultures", "culture-name", "江渚 海桑 舟居 听潮 煮盐 引水"),
    ("craft-cultures", "culture-name", "百工 礼耕 制陶 羽织 炉铸 拓垦"),
    ("river-language-names", "language-name", "河汊 海汀 峡川 东湾 芦汀 长屿"),
    ("northern-language-names", "language-name", "北原 朔庭 雁北 关西 山阴 桑谷"),
    ("sky-religions", "religion-name", "奉火 观星 敬日 敬月 星誓 逐光 长明"),
    ("land-religions", "religion-name", "崇山 守泉 听潮 司禾 玄水 山灵 净泉"),
    ("scholarly-halls", "family-name", "耕读 三槐 杏林 怀远 继明 晋修"),
    ("pastoral-halls", "family-name", "松庭 双桂 归耕 听松 安棠 栖梧"),
    ("landscape-clans", "clan-name", "黑石 赤松 青竹 河源 北原 松岭 江湾 云岫 临川 白垣 长堤"),
    ("civic-clans", "clan-name", "长弓 同舟 礼耕 守约 继明 承志 怀远 崇德 敦仁"),
    ("guard-armies", "army-name", "护城 靖边 河防 守望 镇关 卫河 守隘"),
    ("mobile-armies", "army-name", "朔骑 飞骑 云骑 鹰旗 长锋 横江 凌霄"),
    ("terrain-alliances", "alliance-name", "三川 四水 山海 北境 河谷 海门 长垣"),
    ("common-cause-alliances", "alliance-name", "归舟 守土 同心 诸城 扶风 同舟"),
    ("classical-given-names", "personal-names", "静姝 景行 望舒 正则 修远 怀瑾 含章"),
    ("landscape-given-names", "personal-names", "安澜 青岚 照雪 云舟 听竹 宁秋 雨棠"),
    ("maritime-worlds", "world-name", "瀚海 万屿 远洲 连海 群岛 泽国"),
    ("sky-worlds", "world-name", "云陆 天衡 星河 朝晖 星陲 云山"),
    ("celestial-titles", "player-name", "玄穹 太初 三光 开阳 望舒 司晨 揽星"),
    ("landscape-titles", "player-name", "观潮 临渊 扶桑 定川 观山 牧云"),
]:
    word_group(id, "chinesename:" + ref, members.split())

for file in sorted(CORPUS.glob("subspecies_biome_biome_*.txt")):
    biome = file.stem.removeprefix("subspecies_biome_")
    key = biome.replace("_", "-")
    ref = pool("subspecies-" + key, words(file.stem))
    rule("subspecies", key, [part("name", ref), part("trait", species_traits, optional=True, chance=.65),
                             part("suffix", species_ending)], 5, kind=biome)

for kind, pool_id, label, source, extra in [
    ("normal", "war-conquest", "征伐战称谓", "征服战争称谓", "攻防战 争地之战"),
    ("spite", "war-vengeance", "复仇战称谓", "怨恨战争称谓", "复仇之战 雪耻之役"),
    ("inspire", "war-movement", "思潮战称谓", "思潮战争称谓", "变法之战"),
    ("rebellion", "war-uprising", "起事称谓", "起义称谓", "起事 独立之战"),
    ("whisper_of_war", "war-intrigue", "密谋战称谓", "密谋战争称谓", "争端之战 离间之战"),
]:
    ref = pool(pool_id, words(source) + extra.split(), label)
    pack["poolAliases"][label] = ref
    rule("war", kind.replace("_", "-"), [{"id": "name", "input": "origin"}, part("suffix", ref)], 5, kind=kind)

for kind, label, stems, other, suffix in [
    ("love_story", "爱情故事", "归雁 连理 花信 月下书 长亭", "旧约 相思 渡口 冬日信 千里月 桃扇 重逢", "记"),
    ("bad_story", "轶闻杂谈", "荒村 空屋 纸灯 夜渡 怪客", "远巷 市井 碎影 旧宅 鬼市 雨夜 孤舟", "杂谈"),
    ("fable", "寓言", "井蛙 牧童 白鹿 老樵 沙鸥", "野狐 双雀 黑熊 山羊 蜗牛 鸿雁 狡兔", "寓言"),
    ("warfare_manual", "兵书", "守城 行阵 边防 兵势 训骑", "旗令 军粮 守隘 斥候 兵船 远征 伏阵", "略"),
    ("economy_manual", "经济著作", "市易 粮储 盐铁 农桑 泉货", "田赋 舟商 货殖 茶引 矿税 织币 公廪", "论"),
    ("stewardship_manual", "治理著作", "仓廪 治水 州县 户籍 田畴", "乡约 卫生 桥津 曹署 巡边 园圃 疏渠", "要"),
    ("diplomacy_manual", "外交著作", "通使 会盟 列国 邦交 往聘", "修约 信使 睦邻 息兵 互市 议和 盟誓", "策"),
    ("math", "算学著作", "方田 勾股 分率 测圆 九数", "正负 开方 立方 重差 观日 比例 桥高", "算经"),
    ("biology", "博物著作", "草木 羽族 水族 虫鱼 山兽", "苔藓 菌蕈 海藻 翼禽 爬兽 甲虫 药草", "谱"),
    ("history", "史书", "列国 岁时 王朝 三川 边地", "城邑 山河 世家 王都 远征 海邦 诸侯", "纪"),
]:
    key = kind.replace("_", "-")
    ref = pool("book-" + key, stems.split() + other.split(), label)
    pack["poolAliases"][label + "主题"] = ref
    word_group("early-" + key + "-titles", ref, stems.split())
    word_group("alternate-" + key + "-titles", ref, other.split())
    rule("book", key, [part("name", ref), text("suffix", suffix)], 5, kind="book_name_" + kind)

plot_action = pool("plot-action", "筹议 密议 合谋".split(), "通用谋划称谓")
for kind, names in {
    "rebellion": "举旗起事 联众起义",
    "new-war": "起兵谋议 议定出征",
    "alliance-create": "筹立盟约 议结同盟",
    "alliance-join": "商议入盟 请入盟约",
    "alliance-destroy": "谋解盟约 商议退盟",
    "attacker-stop-war": "商议停战 议定休兵",
    "new-book": "立说著书 编撰新篇",
    "new-language": "创制新语 创立新话",
    "new-religion": "立教传信 创立新教",
    "new-culture": "立制兴俗 肇建新俗",
    "clan-ascension": "擢升氏族 谋立族望",
    "culture-divide": "文化分支 另立新俗",
    "religion-schism": "教派分裂 宗教分支",
    "language-divergence": "语言分化 乡音分支",
    "summon-meteor-rain": "唤落星雨 引降流星",
    "summon-earthquake": "引发地震 祈动山川",
    "summon-thunderstorm": "召来雷暴 引动风雷",
    "summon-stormfront": "召来风暴 引风成暴",
    "summon-hellstorm": "招引炼狱风暴 召来地狱风暴",
    "summon-demons": "召来恶魔 招引魔众",
    "summon-angles": "召来四角生灵 招引四角生灵",
    "summon-skeletons": "召来骷髅 招引白骨",
    "summon-living-plants": "唤醒草木 招引活化草木",
    "big-cast-coffee": "广施醒神 咖啡赐福",
    "big-cast-bubble-shield": "广施气泡护盾 召来护身气泡",
    "big-cast-madness": "播下癫狂 广施狂乱",
    "big-cast-slowness": "广施迟缓 缓行咒仪",
    "cause-rebellion": "策动叛乱 煽动起事",
}.items():
    options = names.split()
    ref = pool("plot-action." + kind, options, options[0] + "称谓")
    word_group("plot-" + kind, ref, options[:1])
rule("plot", "action", [part("name", plot_action, poolInput="plot", fallbackPool=plot_action)],
     use_native_word_groups=True)

for kind, label, endings, names in [
    ("sword", "剑", "剑 长剑", "断潮 照雪 藏锋 龙渊 青锋 逐月 承影"),
    ("axe", "斧", "斧 战斧", "裂碑 开山 破城 赤纹 伐林 断脊 撼地"),
    ("hammer", "锤", "锤 战锤", "震岳 镇石 鼓雷 碎砧 撼城 扣关 崩岩"),
    ("spear", "矛", "矛 长枪", "寒芒 破雾 照野 裂云 追星 穿林 刺霜"),
    ("bow", "弓", "弓 长弓", "揽月 鸣镝 落雁 飞羽 逐日 霜弦 惊鸿"),
    ("staff", "杖", "杖 法杖", "听雷 引星 长明 归烬 枝心 催雨 引灵"),
    ("firearm", "火器", "火铳 火枪", "轰雷 流火 闪雷 赤焰 星火 镇海 断山"),
    ("helmet", "盔", "盔 战盔", "镇关 铁穹 护额 驻云 明顶 鹰目 虎冠"),
    ("armor", "甲", "甲 胸甲", "云纹 磐石 玄鳞 层岩 吞光 承岳 雁翎"),
    ("boots", "靴", "靴 战靴", "踏雪 渡水 追风 登云 越岭 行沙 越野"),
    ("ring", "戒", "戒 指环", "岁星 照月 同心 凝光 白虹 回声 环照"),
    ("amulet", "坠", "坠 护符", "护心 宁神 长安 安宁 祛寒 避尘 镇魄"),
]:
    pack["poolAliases"][label + "类称谓"] = pool("equipment-kind." + kind, endings.split(), label + "类称谓")
    pack["poolAliases"][label + "类名号"] = pool("equipment-name." + kind, names.split(), label + "类名号")
pool("equipment-kind", ["器"], "通用装备种类")
materials = {
    "basic": "精工", "wood": "木 硬木", "stone": "石 青石", "bone": "骨 坚骨", "leather": "皮 韧革",
    "copper": "铜 赤铜", "bronze": "青铜 古铜", "silver": "银 白银", "iron": "铁 玄铁",
    "steel": "钢 精钢", "mythril": "秘银 月银", "adamantine": "精金 星金",
}
for material, values in materials.items():
    pool("equipment-material." + material, values.split(), values.split()[0] + "材质称谓")
pool("equipment-material", ["精工"], "通用材质称谓")
rule("item", "legendary", [part("name", "chinesename:equipment-name", poolInput="equipment"),
                           part("suffix", "chinesename:equipment-kind", poolInput="equipment")], 5, stage="legendary")
rule("item", "reforged", [part("name", "chinesename:equipment-name", poolInput="equipment"),
                          part("suffix", "chinesename:equipment-kind", poolInput="equipment")], 5, stage="reforge")

rule("city", "places", [part("name", places)])
rule("kingdom", "countries", [part("name", countries)])
motto = pool("motto", "兼爱非攻 厚德载物 与民同心 格物致知 协和万邦 守正知新 允执厥中 山海同舟 慎终如始 修身立德 和而不同 山川共守 言必有信 任贤使能 并肩而立 同舟共济 众志成城 见贤思齐 居安思危 行稳致远".split(), "通用格言")
pack["poolAliases"]["通用格言"] = motto
rule("motto", "default", [part("name", motto)])
for kind, label, sayings, other in [
    ("kingdom", "国家格言", "与民同心 邦以民为本 四海安宁 海晏河清 协和万邦", "勤政爱民 仰天守土 安邦济世 持法以公 宁静致远 开疆守业"),
    ("clan", "氏族格言", "敬祖睦族 守望相助 慎终如始 世守其志 修身立德", "一脉同心 先义后利 长念故土 山岳同守 不坠先声 代代相济"),
    ("alliance", "联盟格言", "山海同舟 山川共守 并肩御敌 和而不同 同心守约", "信如金石 风雨与共 诸城共议 休兵息争 一约长存 守信同舟"),
]:
    ref = pool("motto-" + kind, sayings.split() + other.split(), label)
    pack["poolAliases"][label] = ref
    word_group(kind + "-traditional-mottos", ref, sayings.split())
    word_group(kind + "-alternative-mottos", ref, other.split())
    rule("motto", kind, [part("name", ref)], 5, kind=kind)
rule("subspecies", "skeleton", [{"id": "origin", "input": "origin"}, text("suffix", "·骨裔")], 20, kind="skeleton")
rule("subspecies", "uplift", [{"id": "origin", "input": "origin"}, text("suffix", "·升灵")], 20, kind="uplift")

western = {"format": 1, "id": "chinesename:western-localized", "label": "西式本地化",
           "credits": ["一米（Inmny） · 西式译名排列整理"], "requires": {pack["id"]: 1},
           "rules": {}}


def west_name(type_id, rule_id, label, entries, baseline=None):
    ref = pool("western-" + type_id + ("-" + rule_id if rule_id != "default" else ""), bilingual(entries), label)
    pack["poolAliases"][label] = ref
    west_rule = {"id": rule_id, "writing": "Chinese", "parts": [part("name", ref)]}
    if baseline:
        for key in ("when", "priority"):
            if key in baseline:
                west_rule[key] = baseline[key]
    western["rules"].setdefault("worldbox:" + type_id, []).append(west_rule)


western["rules"]["worldbox:person"] = [{
    "id": "sapient", "priority": 10, "when": {"kind": "sapient"}, "writing": "Chinese",
    "chineseSeparator": "·", "originalSeparator": " ",
    "parts": [part("given", western_given), part("surname", western_surname)],
}]
for type_id, rule_id, label, entries in [
    ("city", "places", "西式城名", "Blackwater=布莱克沃特|Greyford=格雷福德|Eastmere=东密尔|Rosehaven=罗斯黑文|Stonebridge=斯通布里奇|Westport=韦斯特波特|Ashfield=阿什菲尔德|Silverbrook=银溪"),
    ("kingdom", "countries", "西式国号", "Alderland=奥尔德兰|Valmere=瓦尔梅尔|Northreach=北境王国|Highmarch=高地侯国|Whitewater=白水公国|Arden=阿尔登|Crownvale=冠谷|Redmoor=雷德穆尔"),
    ("culture", "default", "西式文化名", "Alder Coast=桤木海岸|Northmarch=北境文化|Rosewood=蔷薇丘|Riverfolk=河居民|Highlanders=高地民|Iron Coast=铁岸文化"),
    ("language", "default", "西式语言名", "Valmerian=瓦尔梅尔语|Ardenic=阿尔登语|Alderish=奥尔德语|Northspeak=北境语|Old Greyford=古格雷福德语|Moorish=沼原话"),
    ("religion", "default", "西式宗教名", "Radiant Order=辉光教团|Keepers of the Flame=守火教团|The Old Grove=古林信仰|Church of Dawn=黎明教会|The Silver Star=银星教|Faith of the Tide=潮汐信仰"),
    ("family", "default", "西式家族名", "House of Reed=里德家族|House of Ash=阿什家族|House of the Oak=橡木家族|House of Laurent=洛朗家族|House of Wells=韦尔斯家族|House of Clarke=克拉克家族"),
    ("clan", "default", "西式氏族名", "White Stag Clan=白鹿氏族|Iron Raven Clan=铁鸦氏族|Grey Wolf Clan=灰狼氏族|Storm Elk Clan=风角氏族|Marsh Fox Clan=沼狐氏族|Snow Owl Clan=雪鸮氏族"),
    ("army", "default", "西式军队名", "Dawn Guard=黎明卫队|River Watch=河畔守军|Alder Lancers=桤木枪骑兵|Stone Wall Guard=石垣卫队|White Banner=白旗军团|Frost Legion=霜原军团"),
    ("alliance", "default", "西式联盟名", "Alder Compact=桤木协约|Northern League=北方同盟|Council of Rivers=诸河议盟|League of Ports=港口同盟|Old Crown Pact=旧冠盟约|Silver Harbour League=银港同盟"),
    ("world", "default", "西式世界名", "Ardwell=阿德维尔|Elderreach=埃尔德里奇|Asterfell=星落界|Brightwater=明水界|Avenor=阿维诺尔|Morrowvale=晨谷"),
    ("player", "default", "西式造物主名", "Aster the Watcher=守望者阿斯特|Iris of the Dawn=晨曦之伊莉丝|The Alder Keeper=桤木守护者|Celestine=塞莱斯廷|Lightbearer=持光者|The Mapmaker=制图者"),
    ("subspecies", "default", "西式支系名", "Northland Kin=北陆支系|Tideborn=潮生支系|Frostborn=霜生支系|Deepwood Kin=深林支系|Highland Strain=高地支系|Marshborn=泽生支系"),
    ("war", "default", "西式通用战名", "War of the Old Bridge=古桥之战|Battle of Eastmere=东密尔之战|The Harbour Campaign=港湾战役|Siege of Whitewall=白墙围城"),
    ("book", "default", "西式通用书名", "Annals of the Coast=海岸编年史|Tales of the Far Shore=遥岸故事|The Natural Treasury=博物宝库|Chronicles of Highmarch=高地边地纪"),
    ("item", "default", "西式装备名", "Dawnward=曙光之誓|Frostward=霜守|Starfall=坠星|Oathkeeper=守誓|Moonwhisper=月语|Ironheart=铁心|Stonesong=石歌|Seawatch=望海"),
    ("motto", "default", "西式通用格言", "Hold the Line=守住阵线|Faith and Courage=信念与勇气|As One We Stand=同心而立|In Light We Trust=笃信光明"),
]:
    west_name(type_id, rule_id, label, entries)

west_name("clan", "human", "西式人类氏族名",
          "Alder Clan=桤木氏族|Silverbrook Clan=银溪氏族|Stonebridge Clan=石桥氏族|"
          "River Clan=河湾氏族|Whitehall Clan=白厅氏族|Northgate Clan=北门氏族",
          {"priority": 5, "when": {"culture-species": "human"}})

for type_id, by_id in {
    "war": {
        "normal": ("西式征伐战名", "The Northward Campaign=北境远征|War of the River Gates=河门争夺战|Battle for the Crown=王冠之战"),
        "spite": ("西式复仇战名", "War of Broken Oaths=断誓之战|The War of Vengeance=复仇战争|The Red Reckoning=赤色清算"),
        "inspire": ("西式思潮战名", "War of the New Dawn=新曙之战|The Reform Campaign=改革远征|War of the Banners=新旗之战"),
        "rebellion": ("西式起义名", "The Free Cities Rising=自由城起义|The Hill Country Revolt=高地叛乱|The Uncrowned Rising=无冠者起义"),
        "whisper-of-war": ("西式密谋战名", "The Hidden War=暗中战争|The Veiled Campaign=帷幕战役|War of Silent Envoys=默使之战"),
    },
    "book": {
        "love-story": ("西式爱情故事", "Letters from Rosehaven=罗斯黑文来信|A Winter Promise=冬日之约|Meeting at the Old Bridge=古桥重逢"),
        "bad-story": ("西式轶闻", "The Empty House=空屋异闻|The Night Ferry=夜渡杂谈|Voices from the Moor=荒原之声"),
        "fable": ("西式寓言", "The Fox and the Bell=狐狸与铃铛|The Sparrow's Lesson=麻雀的教诲|The Vain Stag=虚荣的鹿"),
        "warfare-manual": ("西式兵书", "On Keeping a Fortress=论守城|The Field Commander's Guide=指挥官指南|Campaigning by Sea=海战方略"),
        "economy-manual": ("西式经济著作", "Treatise on Grain and Trade=粮食与贸易论|The Coin of the Realm=王国铸币|Markets of the Coast=海岸商市"),
        "stewardship-manual": ("西式治理著作", "A Guide to the Shires=郡县治理指南|The Public Granary=公共粮仓|Bridges and Canals=桥渠营造"),
        "diplomacy-manual": ("西式外交著作", "The Envoy's Handbook=使节手册|The Practice of Treaties=缔约实务|Among the Courts=列国往来"),
        "math": ("西式算学著作", "Elements of Geometry=几何原理|The Surveyor's Table=测绘用表|A Treatise on Ratio=比例论"),
        "biology": ("西式博物著作", "Herbs of the North=北地草药志|A Book of Sea Birds=海鸟图鉴|The Forest Bestiary=林地兽谱"),
        "history": ("西式史书", "Annals of the First Crown=初冠编年史|History of the River Cities=河畔诸城史|The Long Regency=漫长的摄政年代"),
    },
    "motto": {
        "kingdom": ("西式国家格言", "Justice and Stewardship=公正与守成|Crown in Service=王冠为民|Courage Defends Peace=勇气护和平"),
        "clan": ("西式氏族格言", "Keep the Hearth=守护炉火|Remember the Old Names=勿忘祖名|Kin Before Gold=亲族高于黄金"),
        "alliance": ("西式联盟格言", "Unbroken in Council=议盟不渝|Many Shores One Sea=诸岸同海|Together We Keep the Faith=共同守信"),
    },
}.items():
    for original in pack["rules"]["worldbox:" + type_id]:
        if original["id"] in by_id:
            label, names = by_id[original["id"]]
            west_name(type_id, original["id"], label, names, original)

# Keep the item stages distinct and share the western stock of complete titles.
for original in pack["rules"]["worldbox:item"]:
    if original["id"] == "default":
        continue
    western["rules"]["worldbox:item"].append({
        "id": original["id"], "when": original["when"], "priority": original["priority"],
        "writing": "Chinese", "parts": [part("name", "chinesename:western-item")],
    })
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    western_dest = ROOT / "Examples/Packages/western-localized.naming.json"
    for path, value in [(DEST, pack), (western_dest, western)]:
        result = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != result:
                parser.exit(1, f"Regenerate {path.relative_to(ROOT)} with Tools/default_pack.py\n")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(result, encoding="utf-8")
