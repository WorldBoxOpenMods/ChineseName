using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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

    [JsonProperty("default_template")]
    public CN_NameTemplate default_template { get; protected set; } = CN_NameTemplate.Create("#NO_NAME#", 1);

    [JsonProperty("templates")] public List<CN_NameTemplate> templates { get; protected set; } = new();
    
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
}