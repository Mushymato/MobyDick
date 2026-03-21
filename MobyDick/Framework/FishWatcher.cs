using System.Runtime.CompilerServices;
using MobyDick.Model;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace MobyDick.Framework;

internal static class FishWatcher
{
    public static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
    }

    private static void OnDayEnding(object? sender, DayEndingEventArgs e) => ClearTankFishDrawOverrides();

    private static void OnWarped(object? sender, WarpedEventArgs e) => ClearTankFishDrawOverrides();

    private static readonly ConditionalWeakTable<TankFish, TankFishDrawOverride?> TankFishDrawOverrides = [];

    internal static TankFishDrawOverride? GetTankFishDrawOverrideOnCtor(TankFish fish, Item item)
    {
        TankFishDrawOverride? tankFishDrawOverride = TankFishDrawOverrides.GetValue(fish, TankFishDrawOverride.Create);
        tankFishDrawOverride?.FishItem = item;
        return tankFishDrawOverride;
    }

    internal static TankFishDrawOverride? GetTankFishDrawOverride(TankFish fish) =>
        TankFishDrawOverrides.GetValue(fish, TankFishDrawOverride.Create);

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        ClearTankFishDrawOverrides();
    }

    internal static void ClearTankFishDrawOverrides()
    {
        if (!Context.IsSplitScreen || Context.IsMainPlayer)
            TankFishDrawOverrides.Clear();
    }
}
