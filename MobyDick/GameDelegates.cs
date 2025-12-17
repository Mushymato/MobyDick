using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Triggers;

namespace MobyDick;

internal static class GameDelegates
{
    private const string Action_FishFrenzy = $"{ModEntry.ModId}_FishFrenzy";
    private static FieldInfo? fishSplashPointTime = null;

    internal static void Register(IModHelper helper)
    {
        TriggerActionManager.RegisterAction(Action_FishFrenzy, DoFishFrenzy);
        fishSplashPointTime = typeof(GameLocation).GetField("fishSplashPointTime");
    }

    private static bool DoFishFrenzy(string[] args, TriggerActionContext context, out string error)
    {
        // debug action mushymato.MobyDick_FishFrenzy (O)debug.Shork_shork 45 15
        if (!Context.IsWorldReady || Game1.currentLocation is not GameLocation location)
        {
            error = "Null location";
            return false;
        }
        if (
            !ArgUtility.TryGet(args, 2, out string fishId, out error, name: "string fishId")
            || !ArgUtility.TryGetPoint(args, 3, out Point point, out error, "Point point")
        )
        {
            return false;
        }
        if (ItemRegistry.GetData(fishId) is not ParsedItemData data)
        {
            error = $"'{fishId}' is not an item";
            return false;
        }
        if (
            !location.isOpenWater(point.X, point.Y)
            || location.doesTileHaveProperty(point.X, point.Y, "NoFishing", "Back") != null
        )
        {
            error = $"Cannot fish on tile {point}";
            return false;
        }
        // int distance = FishingRod.distanceToLand(point.X, point.Y, location);
        // if (distance <= 1 || distance >= 5)
        // {
        //     error = $"{point} too far or too close to land (distance={distance})";
        //     return false;
        // }
        fishSplashPointTime?.SetValue(location, Game1.timeOfDay);
        location.fishSplashPoint.Value = point;
        location.fishFrenzyFish.Value = data.QualifiedItemId;
        return true;
    }
}
