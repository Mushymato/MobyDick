using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;

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
};

public sealed class MobyDickTextureConditionalData
{
    public string Id
    {
        get => field ??= "MobyDickTextureConditionalData";
        set => field = value;
    }
    public Rectangle? TextureRect { get; set; } = null;
    public Season? Season { get; set; } = null;
    public string? Condition { get; set; } = null;
    public int Precedence { get; set; } = 0;
}

public sealed class MobyDickData
{
    #region content_pack
    public Point SpriteSize { get; set; } = new(24, 24);
    public bool RotateByVelocity { get; set; } = false;
    public int WiggleSegmentLength { get; set; } = 0;
    public float DrawScaleInTank { get; set; } = 4f;
    public string? AquariumTextureOverride { get; set; } = null;
    public List<MobyDickTextureConditionalData>? AquariumTexturesConditional { get; set; } = null;
    public Rectangle AquariumTextureRect { get; set; } = Rectangle.Empty;
    public Vector2 HeldItemOriginOffset { get; set; } = Vector2.Zero;
    public float SwimVelocityMin { get; set; } = -1f;
    public float SwimVelocityMax { get; set; } = -1f;
    public float SwimCooldownMin { get; set; } = -1f;
    public float SwimCooldownMax { get; set; } = -1f;
    public float MinimumVelocity { get; set; } = -1f;
    public float MinimumVelocityVariance { get; set; } = 0.1f;

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int>? SwimAnimation { get; set; } = null;
    public float SwimAnimationInterval { get; set; } = 125f;
    #endregion

    internal string? Key { get; set; } = null;
    internal AquariumFishData? AquariumFish { get; set; } = null;

    internal MobyDickTextureConditionalData? GetTextureConditionalData(GameLocation? location)
    {
        location ??= Game1.currentLocation;
        Season season = location.GetSeason();
        GameStateQueryContext context = new(location, null, null, null, Game1.random);
        if (AquariumTexturesConditional != null && AquariumTexturesConditional.Count > 0)
        {
            IEnumerable<MobyDickTextureConditionalData> matchingCondTx = AquariumTexturesConditional
                .Where(condTx =>
                {
                    if (condTx.Season != null && condTx.Season != season)
                        return false;
                    if (!GameStateQuery.CheckConditions(condTx.Condition, context))
                        return false;
                    return true;
                })
                .OrderBy(condTx => condTx.Precedence);
            if (matchingCondTx.FirstOrDefault() is MobyDickTextureConditionalData firstCondTx)
            {
                return Random.Shared.ChooseFrom(
                    matchingCondTx.Where(condTx => condTx.Precedence == firstCondTx.Precedence).ToList()
                );
            }
        }
        return null;
    }

    internal void GetTextureConditionalDataFields(
        GameLocation? location,
        out Texture2D texture,
        out Rectangle textureRect
    )
    {
        MobyDickTextureConditionalData? condTx = GetTextureConditionalData(location);
        // string textureName = condTx?.Texture ?? AquariumFish?.TextureName ?? "LooseSprites\\AquariumFish";
        string textureName = AquariumFish?.TextureName ?? "LooseSprites\\AquariumFish";
        texture = Game1.content.Load<Texture2D>(textureName);
        textureRect = condTx?.TextureRect ?? AquariumTextureRect;
        if (textureRect.Width == 0)
            textureRect = texture.Bounds;
    }

    internal void GetTextureConditionalDataFields_Frame0(
        GameLocation? location,
        out Texture2D texture,
        out Rectangle sourceRect
    )
    {
        GetTextureConditionalDataFields(location, out texture, out Rectangle textureRect);
        sourceRect = GetAquariumSourceRect(textureRect);
    }

    internal Rectangle GetAquariumSourceRect(Rectangle textureRect, int currentFrame = -1)
    {
        if (AquariumFish == null || AquariumFish.IsErrorFish || SpriteSize.X == 0 || SpriteSize.Y == 0)
        {
            return new(0, 0, 16, 16);
        }
        currentFrame = currentFrame == -1 ? AquariumFish.FishIndex : currentFrame;
        int framePerRow = textureRect.Width / SpriteSize.X;
        if (framePerRow == 0)
        {
            return new(0, 0, 16, 16);
        }
        int x = currentFrame % framePerRow * SpriteSize.X + textureRect.X;
        int y = currentFrame / framePerRow * SpriteSize.Y + textureRect.Y;
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
                animFrames.Add(frame);
            else
                return null;
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
            tankFishTxName = ArgUtility.Get(tankFishParts, 6) ?? "LooseSprites\\AquariumFish";
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
