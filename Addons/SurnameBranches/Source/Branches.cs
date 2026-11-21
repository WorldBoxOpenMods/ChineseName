using System;
using Chinese_Name.Naming.Game;
using HarmonyLib;

namespace ChineseName.SurnameBranches;

internal static class Branches
{
    private const string StateKey = "chinesename.surname-branches.v1";
    private const int Available = 1;
    private const int Closed = 2;
    private const int Chance = 8;

    private static int State(BaseSystemData data)
    {
        if (data == null) return 0;
        data.get(StateKey, out int value);
        return value;
    }

    private static void Offer(BaseSystemData data, long id, long salt)
    {
        if (State(data) != 0) return;
        long worldSeed = World.world.map_stats.life_dna;
        int seed = unchecked((int)(worldSeed ^ (id * 2654435761L) ^ salt));
        data.set(StateKey, new Random(seed).Next(Chance) == 0 ? Available : Closed);
    }

    internal static bool Pending(Actor actor) =>
        State(actor.culture?.data) == Available || State(actor.city?.data) == Available;

    internal static void Consume(Actor actor)
    {
        if (State(actor.culture?.data) == Available) actor.culture.data.set(StateKey, Closed);
        if (State(actor.city?.data) == Available) actor.city.data.set(StateKey, Closed);
    }

    [HarmonyPatch(typeof(Culture), nameof(Culture.cloneAndEvolveOnomastics))]
    internal static class CultureSplit
    {
        private static void Postfix(Culture __instance) => Offer(__instance.data, __instance.id, 0x43554c54);
    }

    [HarmonyPatch(typeof(City), nameof(City.newCityEvent))]
    internal static class CityFounded
    {
        private static void Postfix(City __instance)
        {
            if (WorldNamingInputs.CityOrigin(__instance) != null)
                Offer(__instance.data, __instance.id, 0x43495459);
        }
    }
}
