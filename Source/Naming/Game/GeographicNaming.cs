using System;

namespace Chinese_Name.Naming.Game;

/// <summary>Read-only geographic context for a tile in the current world.</summary>
public sealed class NamingRegion(long id, string name, string landform)
{
    public long Id { get; } = id;
    public string Name { get; } = name;
    public string Landform { get; } = landform;
}

public static class GeographicNaming
{
    private const int MountainDistance = 8;
    private const int MountainTiles = 8;
    private static Func<WorldTile, NamingRegion> _resolve;

    /// <summary>Supply geography on the game thread. Return null until the current world's regions are ready.</summary>
    public static void RegisterRegionResolver(Func<WorldTile, NamingRegion> resolve)
    {
        if (_resolve != null) throw new InvalidOperationException("A naming region resolver is already registered.");
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    internal static NamingRegion At(WorldTile tile)
    {
        var region = tile != null ? _resolve?.Invoke(tile) : null;
        // 原版不能在山体上建城，位于山脚的城区仍可形成山地命名。
        if (region != null && region.Landform is null or "plain" && NearMountain(tile))
            return new NamingRegion(region.Id, region.Name, "mountain");
        return region;
    }

    private static bool NearMountain(WorldTile tile)
    {
        int seen = 0;
        for (int x = -MountainDistance; x <= MountainDistance; x++)
        {
            int span = MountainDistance - Math.Abs(x);
            for (int y = -span; y <= span; y++)
                if (World.world.GetTile(tile.x + x, tile.y + y)?.Type.mountains == true && ++seen >= MountainTiles)
                    return true;
        }
        return false;
    }
}
