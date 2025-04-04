using System;

namespace Chinese_Name.Abstract;

public class AssetIdAttribute(string assetId) : Attribute
{
    public readonly string AssetId = assetId;
}