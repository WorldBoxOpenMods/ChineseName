using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Chinese_Name.Naming.Game;

internal static class AllianceNames
{
    private const string PartnerId = "chinesename.alliance-partner.v1";
    private const string PartnerShort = "chinesename.alliance-partner-short.v1";
    private const string PendingName = "chinesename.alliance-pending-name.v1";

    internal static string Generate(Alliance alliance, Kingdom founder = null, Kingdom partner = null,
        Func<string, bool> accept = null)
    {
        return Calculate(Request(alliance, founder, partner), request => NamingApi.Current.Generate(request, accept), accept);
    }

    internal static string Calculate(NameRequest request, Func<NameRequest, NameResult> generate, Func<string, bool> accept)
    {
        var result = generate(request);
        return result.Success ? result.Text : accept == null || accept("同盟") ? "同盟" : null;
    }

    internal static NameRequest Request(Alliance alliance, Kingdom founder = null, Kingdom partner = null,
        IReadOnlyDictionary<long, KingdomNames.Calculated> projected = null)
    {
        var world = World.world;
        var data = alliance.data;
        string savedFounder = data.founder_kingdom_name;
        var members = alliance.kingdoms_hashset.Count > 0 ? (IEnumerable<Kingdom>)alliance.kingdoms_hashset : alliance.kingdoms_list;
        if (founder == null)
        {
            founder = world.kingdoms.get(data.founder_kingdom_id);
            if (founder == null && string.IsNullOrWhiteSpace(savedFounder))
                founder = members.OrderBy(member => member.id).FirstOrDefault();
        }
        data.get(PartnerId, out long partnerId, -1L);
        data.get(PartnerShort, out string savedPartner);
        if (partner == founder) partner = null;
        if (partner == null && partnerId != founder?.id) partner = world.kingdoms.get(partnerId);
        if (partner == null && string.IsNullOrWhiteSpace(savedPartner))
            partner = members.Where(member => member != founder).OrderBy(member => member.id).FirstOrDefault();

        var inputs = new Dictionary<string, object>();
        var newFounder = Planned(founder);
        string founderName = newFounder?.Text ?? founder?.name;
        Add("origin", string.IsNullOrWhiteSpace(founderName) ? savedFounder : founderName);
        Add("founder-short", newFounder?.Short ?? KingdomNames.Short(founder, savedFounder));
        Add("partner-short", Planned(partner)?.Short ?? KingdomNames.Short(partner, savedPartner));
        return VanillaNames.Request(VanillaNames.Alliance, founder?.king, unchecked((ulong)IndependentNames.Seed(alliance.id)),
            culture: founder?.culture?.id, language: founder?.language?.id, inputs: inputs, subject: alliance);

        KingdomNames.Calculated Planned(Kingdom kingdom) =>
            kingdom != null && projected?.TryGetValue(kingdom.id, out var name) == true ? name : null;

        void Add(string field, string value)
        {
            if (value is { Length: > 0 and <= 256 } && !string.IsNullOrWhiteSpace(value)) inputs[field] = value;
        }
    }

    private static void FillMissing(Alliance alliance, Kingdom founder = null, Kingdom partner = null, bool provisional = false)
    {
        if (alliance.data.custom_name) return;
        alliance.data.get(PendingName, out string pending);
        if (!string.IsNullOrWhiteSpace(alliance.name) && alliance.name != pending) return;
        string name = Generate(alliance, founder, partner);
        if (alliance.name != name) alliance.setName(name, pTrack: !provisional && pending == null);
        alliance.data.set(PendingName, provisional ? name : null);
    }

    [HarmonyPatch(typeof(AllianceManager), nameof(AllianceManager.newAlliance))]
    internal static class ManagedCreation
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            IndependentNames.ReplaceCall(codes, AccessTools.Method(typeof(Alliance), nameof(Alliance.createNewAlliance)),
                AccessTools.Method(typeof(AllianceNames), nameof(InitializeManaged)), 1, 2);
    }

    private static void InitializeManaged(Alliance alliance, Kingdom founder, Kingdom partner)
    {
        FillMissing(alliance, founder, partner);
        alliance.generateNewMetaObject();
    }

    [HarmonyPatch(typeof(Alliance), nameof(Alliance.createNewAlliance))]
    internal static class DirectCreation
    {
        private static bool Prefix(Alliance __instance)
        {
            FillMissing(__instance, provisional: __instance.kingdoms_hashset.Count < 2 && __instance.kingdoms_list.Count < 2);
            __instance.generateNewMetaObject();
            return false;
        }
    }

    [HarmonyPatch(typeof(Alliance), nameof(Alliance.addFounders))]
    internal static class Founders
    {
        private static void Prefix(Alliance __instance, Kingdom pKingdom1, Kingdom pKingdom2)
        {
            if (pKingdom2 != null && pKingdom2 != pKingdom1)
            {
                __instance.data.set(PartnerId, pKingdom2.id);
                __instance.data.set(PartnerShort, KingdomNames.Short(pKingdom2));
            }
            FillMissing(__instance, pKingdom1, pKingdom2);
        }
    }

    [HarmonyPatch]
    internal static class MemberFallback
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Alliance), nameof(Alliance.join));
            yield return AccessTools.Method(typeof(Alliance), nameof(Alliance.loadData));
            yield return AccessTools.Method(typeof(Alliance), nameof(Alliance.update));
        }

        private static void Postfix(Alliance __instance)
        {
            if (__instance.kingdoms_hashset.Count >= 2 || __instance.kingdoms_list.Count >= 2) FillMissing(__instance);
        }
    }
}
