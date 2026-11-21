using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Chinese_Name.Naming;

namespace ChineseName.CultureExonyms;

internal static class ResidentContact
{
    private const string ResidenceKey = "chinesename.residence.v1";
    private const int ResidenceYears = 2;

    private sealed class Residence
    {
        public long City { get; set; }
        public long Culture { get; set; }
        public long HostCulture { get; set; }
        public long Kingdom { get; set; }
        public double Since { get; set; }
    }

    private static bool IsForeignResident(Actor actor, City city) => actor.city == city &&
        actor.asset?.is_boat == false && actor.isAlive() && actor.isSapient() &&
        actor.culture != null && city?.culture != null && actor.culture != city.culture;

    private static Residence Start(Actor actor, City city)
    {
        var residence = new Residence
        {
            City = city.id, Culture = actor.culture.id, HostCulture = city.culture.id,
            Kingdom = city.kingdom?.id ?? -1, Since = World.world.getCurWorldTime()
        };
        actor.data.set(ResidenceKey, NamingJson.Write(residence));
        return residence;
    }

    [HarmonyPatch]
    internal static class ChangeResidence
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Actor), nameof(Actor.setCity));
            yield return AccessTools.Method(typeof(Actor), nameof(Actor.setCulture));
        }

        private static void Prefix(Actor __instance, out (City City, Culture Culture) __state) =>
            __state = (__instance.city, __instance.culture);

        private static void Postfix(Actor __instance, (City City, Culture Culture) __state)
        {
            if (SmoothLoader.isLoading() ||
                __state.City == __instance.city && __state.Culture == __instance.culture) return;
            if (IsForeignResident(__instance, __instance.city)) Start(__instance, __instance.city);
            else __instance.data.removeString(ResidenceKey);
        }
    }

    [HarmonyPatch(typeof(City), nameof(City.updateAge))]
    internal static class YearlyResidence
    {
        private static void Postfix(City __instance)
        {
            var city = __instance;
            if (city.culture == null || SmoothLoader.isLoading()) return;
            var known = new HashSet<long>();
            foreach (var actor in city.units)
            {
                if (!IsForeignResident(actor, city) || known.Contains(actor.culture.id)) continue;
                actor.data.get(ResidenceKey, out string saved);
                var residence = saved == null ? null : NamingJson.Read<Residence>(saved);
                if (residence == null || residence.City != city.id || residence.Culture != actor.culture.id ||
                    residence.HostCulture != city.culture.id || residence.Kingdom != (city.kingdom?.id ?? -1))
                    residence = Start(actor, city);
                if (Date.getYearsSince(residence.Since) < ResidenceYears) continue;
                known.Add(actor.culture.id);
                ForeignNames.LearnFromResidence(actor, city);
            }
        }
    }
}
