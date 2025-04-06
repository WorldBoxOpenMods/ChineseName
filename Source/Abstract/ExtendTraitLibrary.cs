using System.Collections.Generic;

namespace Chinese_Name.Abstract;

public abstract class ExtendTraitLibrary<TTrait, TLibrary> : ExtendLibrary<TTrait, TLibrary> 
    where TTrait : BaseTrait<TTrait>, new()
    where TLibrary : ExtendLibrary<TTrait, TLibrary>
{
    public override void PostInit(TTrait asset)
    {
        base.PostInit(asset);
        if (asset.opposite_list != null && asset.opposite_list.Count > 0)
        {
            asset.opposite_traits = new HashSet<TTrait>(asset.opposite_list.Count);
            foreach (string tID in asset.opposite_list)
            {
                TTrait tOppositeTrait = Get(tID);
                asset.opposite_traits.Add(tOppositeTrait);
            }
        }
        if (asset.traits_to_remove_ids != null)
        {
            int tCount = asset.traits_to_remove_ids.Length;
            asset.traits_to_remove = new TTrait[tCount];
            for (int i = 0; i < tCount; i++)
            {
                string tID2 = asset.traits_to_remove_ids[i];
                TTrait tTraitToAdd = this.Get(tID2);
                asset.traits_to_remove[i] = tTraitToAdd;
            }
        }
        if (asset.decision_ids != null)
        {
            asset.decisions_assets = new DecisionAsset[asset.decision_ids.Count];
            for (int i = 0; i < asset.decision_ids.Count; i++)
            {
                string tDecisionID = asset.decision_ids[i];
                DecisionAsset tDecisionAsset = AssetManager.decisions_library.get(tDecisionID);
                asset.decisions_assets[i] = tDecisionAsset;
            }
        }
        asset.linkCombatActions();
        asset.linkSpells();

        var cached_trait_library = (BaseTraitLibrary<TTrait>)cached_library;
        foreach (ActorAsset tActorAsset in AssetManager.actor_library.list)
        {
            List<string> tTraits = cached_trait_library.getDefaultTraitsForMeta(tActorAsset);
            if (tTraits != null)
            {
                foreach (string tTraitId in tTraits)
                {
                    var tTrait = this.Get(tTraitId);
                    if (tTrait.default_for_actor_assets == null)
                    {
                        tTrait.default_for_actor_assets = new List<ActorAsset>();
                    }
                    tTrait.default_for_actor_assets.Add(tActorAsset);
                }
            }
        }

        if (string.IsNullOrEmpty(asset.path_icon))
        {
            asset.path_icon = cached_trait_library.icon_path + asset.getLocaleID();
        }
        
        
        
        if (asset.unlocked_with_achievement)
        {
            asset.rarity = Rarity.R3_Legendary;
        }
        else
        {
            int tCount = 0;
            if ( asset.action_death != null || asset.action_special_effect != null || asset.action_get_hit != null || asset.action_birth != null || asset.action_attack_target != null || asset.action_on_add != null || asset.action_on_remove != null || asset.action_on_load != null)
            {
                tCount++;
            }
            if (asset.decision_ids != null)
            {
                tCount++;
            }
            if (asset.spells_ids != null)
            {
                tCount++;
            }
            if ( asset.combat_actions_ids != null)
            {
                tCount++;
            }
            if (asset.base_stats.hasTags())
            {
                tCount++;
            }
            if (!string.IsNullOrEmpty(asset.plot_id))
            {
                tCount++;
            }
            if (tCount > 0)
            {
                if (tCount == 1)
                {
                    asset.rarity = Rarity.R1_Rare;
                }
                else
                {
                    asset.rarity = Rarity.R2_Epic;
                }
                asset.needs_to_be_explored = true;
            }
            else if (asset.rarity == Rarity.R0_Normal)
            {
                asset.needs_to_be_explored = false;
            }
        }
    }
}