using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MobyDick.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MobyDick.Model;

internal sealed record AquariumFishData(
    int FishIndex,
    string Movement,
    List<int>? IdleAnimation = null,
    List<int>? DartStartAnimation = null,
    List<int>? DartHoldAnimation = null,
    List<int>? DartEndAnimation = null,
    string? TextureName = null,
    string? HatPosition = null
)
{
    internal bool IsErrorFish =>
        string.IsNullOrEmpty(TextureName) || !Game1.content.DoesAssetExist<Texture2D>(TextureName);

    internal Texture2D GetTexture()
    {
        if (IsErrorFish)
            return Game1.content.Load<Texture2D>("LooseSprites\\AquariumFish");
        return Game1.content.Load<Texture2D>(TextureName);
    }
};

public sealed class MobyDickData
{
    #region content_pack
    public Point SpriteSize { get; set; } = Point.Zero;
    public bool RotateByVelocity { get; set; } = false;
    public int WiggleSegmentLength { get; set; } = 0;
    public float DrawScaleInTank { get; set; } = 4f;
    public string? AquariumTextureOverride { get; set; } = null;
    public Rectangle AquariumTextureRect { get; set; } = Rectangle.Empty;
    public Vector2 HeldItemOriginOffset { get; set; } = Vector2.Zero;

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int>? SwimAnimation { get; set; } = null;
    public float SwimAnimationInterval { get; set; } = 125f;
    #endregion

    internal string? Key { get; set; } = null;
    internal AquariumFishData? AquariumFish { get; set; } = null;

    internal Rectangle GetAquariumSourceRect(int currentFrame = -1, Texture2D? texture = null)
    {
        if (AquariumFish == null || AquariumFish.IsErrorFish)
        {
            return new(0, 0, 16, 16);
        }
        texture ??= AquariumFish.GetTexture();
        currentFrame = currentFrame == -1 ? AquariumFish.FishIndex : currentFrame;
        int framePerRow = (AquariumTextureRect.Width > 0 ? AquariumTextureRect.Width : texture.Width) / SpriteSize.X;
        int x = currentFrame % framePerRow * SpriteSize.X + AquariumTextureRect.X;
        int y = currentFrame / framePerRow * SpriteSize.Y + AquariumTextureRect.Y;
        return new(x, y, SpriteSize.X, SpriteSize.Y);
    }

    private static List<int>? ParseAnimation(string? animStr)
    {
        if (animStr == null)
            return null;
        List<int> animFrames = [];
        foreach (string f in ArgUtility.SplitBySpace(animStr))
        {
            if (int.TryParse(f, out int frame))
            {
                animFrames.Add(frame);
            }
            else
            {
                return null;
            }
        }
        return animFrames;
    }

    internal void ParseAquariumFishData(Dictionary<string, string> aquariumFishData, string key)
    {
        if (!aquariumFishData.TryGetValue(key, out string? tankFishStr))
        {
            return;
        }
        Key = key;
        string[] tankFishParts = tankFishStr.Split('/');
        int sourceIdx;
        string tankFishTxName;
        if (string.IsNullOrEmpty(AquariumTextureOverride))
        {
            sourceIdx = ArgUtility.GetInt(tankFishParts, 0, 0);
            tankFishTxName = ArgUtility.Get(tankFishParts, 6);
        }
        else
        {
            sourceIdx = 0;
            tankFishTxName = AquariumTextureOverride;
        }
        AquariumFish = new(
            sourceIdx,
            ArgUtility.Get(tankFishParts, 1) ?? "float",
            ParseAnimation(ArgUtility.Get(tankFishParts, 2)),
            ParseAnimation(ArgUtility.Get(tankFishParts, 3)),
            ParseAnimation(ArgUtility.Get(tankFishParts, 4)),
            ParseAnimation(ArgUtility.Get(tankFishParts, 5)),
            tankFishTxName,
            ArgUtility.Get(tankFishParts, 7)
        );
    }
}

internal static class AssetManager
{
    private const string AssetName_MobyDickData = $"{ModEntry.ModId}/Data";
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

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetInvalidated;
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
            FishPatches.ClearTankFishDrawOverrides();
        }
        else if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data\\AquariumFish")) && mbData != null)
        {
            FishPatches.ClearTankFishDrawOverrides();
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
