using Microsoft.Xna.Framework;
using StardewValley.GameData;
using StardewValley.Internal;

namespace MobyDick.Model;

public sealed class TankFishSpawnData : GenericSpawnItemDataWithCondition
{
    public ItemQuerySearchMode SearchMode { get; set; } = ItemQuerySearchMode.AllOfTypeItem;
    public int RepeatCount { get; set; } = 1;
    public int TakeCount { get; set; } = -1;
}

public sealed class LocationalTankData
{
    public bool DrawInBackground { get; set; } = false;
    public bool DrawBubbles { get; set; } = true;
    public float SortTileOffset { get; set; } = 0f;
    public Rectangle TankBounds { get; set; } = Rectangle.Empty;

    public string? ForegroundTexture { get; set; } = null;
    public Rectangle ForegroundSourceRect { get; set; } = Rectangle.Empty;
    public Rectangle ForegroundTargetRect { get; set; } = Rectangle.Empty;
    public string? BackgroundTexture { get; set; } = null;
    public Rectangle BackgroundSourceRect { get; set; } = Rectangle.Empty;
    public Rectangle BackgroundTargetRect { get; set; } = Rectangle.Empty;

    public List<TankFishSpawnData>? Fishes { get; set; } = null;
}
