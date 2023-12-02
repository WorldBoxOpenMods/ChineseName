using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Chinese_Name;
[Serializable]
public class CN_NameGeneratorAsset : Asset
{
    [JsonProperty("parameter_getter")] public string parameter_getter { get; private set; } = "default";

    [JsonProperty("templates")]
    public List<CN_NameTemplate> templates { get; private set; }

    private float[] weights = null;
    private float total_weight = 0f;
    
    public CN_NameTemplate GetRandomTemplate()
    {
        if (weights == null || weights.Length != templates.Count)
        {
            weights = new float[templates.Count];
            weights[0] = templates[0].weight;
            for (int i = 1; i < templates.Count; i++)
            {
                weights[i] = weights[i-1] + templates[i].weight;
            }
            total_weight = weights[weights.Length - 1];
        }
        // 总不能有人写出几十上百个模板吧
        float random = UnityEngine.Random.Range(0f, total_weight);
        for (int i = 0; i < weights.Length; i++)
        {
            if (random < weights[i]) return templates[i];
        }
        return templates[templates.Count - 1];
    }

    public string GenerateName(Dictionary<string, string> pParameters)
    {
        int max_try = 10;
        while (max_try-- > 0)
        {
            string name = GetRandomTemplate().GenerateName(pParameters);
            if (!string.IsNullOrEmpty(name)) return name;
        }
    }
}