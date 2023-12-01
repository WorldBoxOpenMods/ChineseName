using Newtonsoft.Json;

namespace Chinese_Name;

public class CN_NameGeneratorAsset : Asset
{
    [JsonProperty("templates")]
    public List<CN_NameTemplate> templates;

    private float[] weights = null;
    private float total_weight = 0f;
    
    internal CN_NameTemplate GetRandomTemplate()
    {
        if (weights == null || weights.Length != templates.Count)
        {
            weights = new float[templates.Count];
            weights[0] = templates[0].weight;
            for (int i = 0; i < templates.Count; i++)
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
}