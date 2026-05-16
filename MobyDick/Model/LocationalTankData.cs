using Microsoft.Xna.Framework;

namespace MobyDick.Model;

public sealed class LocationalTankData
{
    public bool DrawInBackground { get; set; } = false;
    public float SortTileOffset { get; set; } = 0f;
    public Point TankPosition { get; set; } = Point.Zero;
    public Rectangle TankBounds { get; set; } = Rectangle.Empty;
}
