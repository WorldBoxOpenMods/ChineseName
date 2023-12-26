using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Random = UnityEngine.Random;

namespace Chinese_Name;

[Serializable]
public class CN_NameGeneratorAsset : Asset
{
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
        if (weights == null || weights.Length != templates.Count)
        {
            weights = new float[templates.Count];
            weights[0] = templates[0].weight;
            for (int i = 1; i < templates.Count; i++)
            {
                weights[i] = weights[i - 1] + templates[i].weight;
            }

            total_weight = weights[weights.Length - 1];
        }

        // 总不能有人写出几十上百个模板吧
        float random = Random.Range(0f, total_weight);
        for (int i = 0; i < weights.Length; i++)
        {
            if (random < weights[i]) return templates[i];
        }

        return templates[templates.Count - 1];
    }

    /// <summary>
    /// 根据参数, 尝试10次随机获取模板并生成名字
    /// </summary>
    /// <remarks>你也可以override这个方法, 然后用单个提交的方式:Submit, 来提交派生的<see cref="CN_NameGeneratorAsset"/></remarks>
    public virtual string GenerateName(Dictionary<string, string> pParameters)
    {
        int max_try = 10;
        while (max_try-- > 0)
        {
            string name = GetTemplate(pParameters).GenerateName(pParameters);
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return default_template.GenerateName(pParameters);
    }
}