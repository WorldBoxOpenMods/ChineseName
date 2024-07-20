using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace Chinese_Name;

[Serializable]
public class CN_NameGeneratorAsset : Asset
{
    private float current_total_weight;
    private float[] current_weights = null;
    private float total_weight = 0f;

    private float[] weights = null;
    [JsonProperty("parameter_getter")] public string parameter_getter { get; protected set; } = "default";
    internal List<string> param_getters { get; private set; }

    [JsonProperty("default_template")]
    public CN_NameTemplate default_template { get; protected set; } = CN_NameTemplate.Create("#NO_NAME#", 1);

    [JsonProperty("templates")] public List<CN_NameTemplate> templates { get; protected set; } = new();
    internal void MergeWith(CN_NameGeneratorAsset asset)
    {
        if (asset == null) return;

        var new_param_getters = asset.param_getters.Except(param_getters);
        param_getters.InsertRange(0, new_param_getters);

        templates.AddRange(asset.templates);
        InitializeWeight();
    }
    /// <summary>
    /// 按权重随机获取一个模板
    /// </summary>
    /// <remarks>你也可以override这个方法, 然后用单个提交的方式:Submit, 来提交派生的<see cref="CN_NameGeneratorAsset"/></remarks>
    public virtual CN_NameTemplate GetTemplate(Dictionary<string, string> pParameters = null)
    {
        InitializeWeight();
        // 总不能有人写出几十上百个模板吧
        var random = Random.Range(0f, current_total_weight);
        for (int i = 0; i < weights.Length; i++)
        {
            random -= current_weights[i];
            if (random > 0) continue;
            current_total_weight -= current_weights[i];
            current_weights[i] = 0;
            return templates[i];
        }

        return templates[templates.Count - 1];
    }

    private void InitializeWeight()
    {
        if (weights != null && weights.Length == templates.Count) return;
        weights = new float[templates.Count];
        total_weight = 0;
        for (var i = 0; i < templates.Count; i++)
        {
            weights[i] = templates[i].weight;
            total_weight += weights[i];
        }

        current_weights = new float[templates.Count];
    }

    public virtual void ClearTemplateGetter()
    {
        InitializeWeight();
        weights.CopyTo(current_weights, 0);
        current_total_weight = total_weight;
    }

    /// <summary>
    /// 根据参数, 尝试10次随机获取模板并生成名字
    /// </summary>
    /// <remarks>你也可以override这个方法, 然后用单个提交的方式:Submit, 来提交派生的<see cref="CN_NameGeneratorAsset"/></remarks>
    public virtual string GenerateName(Dictionary<string, string> pParameters)
    {
        ClearTemplateGetter();
        int max_try = 10;
        while (max_try-- > 0)
        {
            string name = GetTemplate(pParameters).GenerateName(pParameters);
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return default_template.GenerateName(pParameters);
    }
    internal bool SelfCheck()
    {
        if (string.IsNullOrEmpty(parameter_getter))
        {
            ModClass.LogWarning($"No valid parameter getter in {id}");
            ModClass.LogInfo($"It is set to default.");
            parameter_getter = "default";
        }
        if (param_getters==null || param_getters.Count == 0)
        {
            param_getters = new List<string>() { parameter_getter };
        }
        for (int i = 0; i < templates.Count; i++)
        {
            var name_template = templates[i];
            try
            {
                name_template.Parse();
            }
            catch (Exception e)
            {
                ModClass.LogWarning($"Failed to parse name template '{name_template.raw_format}' in {id}");
                ModClass.LogWarning(e.Message);
                ModClass.LogInfo($"Just skip it now.");

                templates.RemoveAt(i);
                i--;
            }
        }

        if (templates.Count == 0)
        {
            ModClass.LogWarning($"No valid name template in {id}");
            ModClass.LogInfo($"Just skip it now.");

            return false;
        }
        return true;
    }
}