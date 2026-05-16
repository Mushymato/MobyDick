using System.Diagnostics.CodeAnalysis;
using MobyDick.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MobyDick.Model;

internal static class AssetManager
{
    private const string AssetName_MobyDickData = $"{ModEntry.ModId}/Data";
    private const string AssetName_TankData = $"{ModEntry.ModId}/Tanks";
    private static Dictionary<string, MobyDickData>? mbData = null;
    internal static Dictionary<string, MobyDickData> MBData
    {
        get
        {
            if (mbData != null)
                return mbData;
            mbData = Game1.content.Load<Dictionary<string, MobyDickData>>(AssetName_MobyDickData);
            Dictionary<string, string> aquariumFishData = DataLoader.AquariumFish(Game1.content);
            foreach ((string key, MobyDickData data) in mbData)
            {
                data.ParseAquariumFishData(aquariumFishData, key);
            }
            return mbData;
        }
    }

    private static Dictionary<string, LocationalTankData>? tankData = null;
    internal static Dictionary<string, LocationalTankData> TankData =>
        tankData ??= Game1.content.Load<Dictionary<string, LocationalTankData>>(AssetName_TankData);

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetInvalidated;
    }

    internal static bool TryGetFish(string? itemId, [NotNullWhen(true)] out MobyDickData? data)
    {
        data = null;
        if (itemId == null)
            return false;
        return MBData.TryGetValue(itemId, out data) && data.AquariumFish is AquariumFishData aqf && !aqf.IsErrorFish;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AssetName_MobyDickData))
        {
            e.LoadFrom(() => new Dictionary<string, MobyDickData>(), AssetLoadPriority.Exclusive);
        }
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(AssetName_MobyDickData)))
        {
            mbData = null;
            FishWatcher.ClearTankFishDrawOverrides();
        }
        else if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(AssetName_TankData)))
        {
            tankData = null;
        }
        else if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data\\AquariumFish")) && mbData != null)
        {
            FishWatcher.ClearTankFishDrawOverrides();
            DelayedAction.functionAfterDelay(
                () =>
                {
                    Dictionary<string, string> aquariumFishData = DataLoader.AquariumFish(Game1.content);
                    foreach ((string key, MobyDickData data) in mbData)
                    {
                        data.ParseAquariumFishData(aquariumFishData, key);
                    }
                },
                0
            );
        }
    }
}
