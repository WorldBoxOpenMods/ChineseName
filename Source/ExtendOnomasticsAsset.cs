using UnityEngine.Pool;

namespace Chinese_Name;

public delegate string ChineseOnomasticsNameMakerDelegate(OnomasticsAsset pAsset, OnomasticsData pData, StringBuilderPool pLocalBuilder, StringBuilderPool pGlobalBuilder, string pLastPart, int pIndex, ActorSex pSex, DictionaryPool<string, string> pParameters, Actor namer);
public class ExtendOnomasticsAsset
{
    public ChineseOnomasticsNameMakerDelegate ChineseNameMakerDelegate;
}