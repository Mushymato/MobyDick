using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MobyDick.Model;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley.Objects;
using StardewValley.Tools;

namespace MobyDick.Framework;

internal record PickedCondTx(string Id, Texture2D Texture, Rectangle SourceRect);

internal static class FishWatcher
{
    private static readonly ConditionalWeakTable<TankFish, TankFishDrawOverride?> TankFishDrawOverrides = [];

    internal static TankFishDrawOverride? GetTankFishDrawOverride(TankFish fish) =>
        TankFishDrawOverrides.GetValue(fish, TankFishDrawOverride.Create);

    private static readonly ConditionalWeakTable<SObject, PickedCondTx?> FishObjectPickedCondTx = [];

    internal static PickedCondTx? GetSmokedFishPickedCondTx(ColoredObject fish)
    {
        return FishObjectPickedCondTx.GetValue(
            fish,
            static (fish) =>
            {
                if (
                    fish.GetPreservedItemId() is not string preserveId
                    || !AssetManager.TryGet(preserveId, out MobyDickData? data)
                )
                    return null;
                data.GetTextureConditionalDataFields_Frame0(
                    fish.Location,
                    out Texture2D texture,
                    out Rectangle sourceRect
                );
                return new(preserveId, texture, sourceRect);
            }
        );
    }

    internal static PickedCondTx? GetSObjectPickedCondTx(SObject fish)
    {
        return FishObjectPickedCondTx.GetValue(
            fish,
            static (fish) =>
            {
                if (fish.TypeDefinitionId != "(O)")
                    return null;
                if (!AssetManager.TryGet(fish.ItemId, out MobyDickData? data))
                    return null;
                data.GetTextureConditionalDataFields_Frame0(
                    fish.Location,
                    out Texture2D texture,
                    out Rectangle sourceRect
                );
                return new(fish.ItemId, texture, sourceRect);
            }
        );
    }

    private static readonly PerScreen<PickedCondTx?> fishingRodHeldUp = new();

    internal static PickedCondTx? GetFishingRodHeldUp(FishingRod rod)
    {
        if (
            rod.whichFish.TypeIdentifier != "(O)"
            || !AssetManager.TryGet(rod.whichFish.LocalItemId, out MobyDickData? data)
        )
            return null;
        string id = string.Concat(rod.GetHashCode().ToString(), rod.whichFish.LocalItemId);
        if (fishingRodHeldUp.Value is PickedCondTx pickTx)
        {
            if (pickTx.Id == id)
                return pickTx;
            fishingRodHeldUp.Value = null;
        }
        data.GetTextureConditionalDataFields_Frame0(
            rod.lastUser?.currentLocation,
            out Texture2D texture,
            out Rectangle sourceRect
        );
        return new(id, texture, sourceRect);
    }

    public static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
    }

    private static void OnDayEnding(object? sender, DayEndingEventArgs e) => ClearTankFishDrawOverrides();

    private static void OnWarped(object? sender, WarpedEventArgs e) => ClearTankFishDrawOverrides();

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => ClearTankFishDrawOverrides();

    internal static void ClearTankFishDrawOverrides()
    {
        if (!Context.IsSplitScreen || Context.IsMainPlayer)
        {
            TankFishDrawOverrides.Clear();
            FishObjectPickedCondTx.Clear();
            fishingRodHeldUp.Value = null;
        }
    }
}
