using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MobyDick.Model;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Internal;
using StardewValley.Mods;
using StardewValley.Objects;
using StardewValley.Triggers;
#if SDV17
using StardewValley.Objects.FishTanks;
#endif

namespace MobyDick.Framework;

internal sealed record TankLayer(Texture2D Tx, Rectangle Source, Rectangle Target)
{
    public static TankLayer? TryMake(string? textureName, Rectangle source, Rectangle target, Rectangle boundingBox)
    {
        if (!string.IsNullOrEmpty(textureName) && Game1.content.DoesAssetExist<Texture2D>(textureName))
        {
            Texture2D layerTx = Game1.content.Load<Texture2D>(textureName);
            return new(
                layerTx,
                source.IsEmpty ? layerTx.Bounds : source,
                target.IsEmpty
                    ? boundingBox
                    : new(target.X + boundingBox.X, target.Y + boundingBox.Y, target.Width, target.Height)
            );
        }
        return null;
    }

    public void Draw(SpriteBatch b, float alpha, float layerDepth)
    {
        b.Draw(
            Tx,
            Game1.GlobalToLocal(Game1.viewport, Target),
            Source,
            Color.White * alpha,
            0,
            Vector2.Zero,
            SpriteEffects.None,
            layerDepth
        );
    }
}

internal sealed class LocationalFishTank : FishTankFurniture
{
    public const string DummyTankId = $"{ModEntry.ModId}_LocationalTank";

    public readonly string key;
    private readonly LocationalTankData data;
    public bool DrawInBackground => data.DrawInBackground;

    private readonly TankLayer? background = null;
    private readonly TankLayer? foreground = null;

    public LocationalFishTank(GameLocation location, string key, LocationalTankData data)
        : base(DummyTankId, new(data.TankBounds.X, data.TankBounds.Y))
    {
        this.Location = location;
        this.key = key;
        this.data = data;

        boundingBox.Value = new(
            data.TankBounds.X,
            data.DrawInBackground ? 0 : (int)(data.TankBounds.Y - data.SortTileOffset * 64f),
            data.TankBounds.Width,
            data.TankBounds.Height
        );

        foreground = TankLayer.TryMake(
            data.ForegroundTexture,
            data.ForegroundSourceRect,
            data.ForegroundTargetRect,
            boundingBox.Value
        );
        background = TankLayer.TryMake(
            data.BackgroundTexture,
            data.BackgroundSourceRect,
            data.BackgroundTargetRect,
            boundingBox.Value
        );

        RespawnFish();
    }

    public void RespawnFish()
    {
        heldItems.Clear();
        if (data.Fishes == null)
            return;
        ItemQueryContext context = new(
            Location,
            Game1.player,
            Random.Shared,
            $"{ModEntry.ModId}/{nameof(LocationalFishTank)}.{nameof(RespawnFish)})"
        );
        foreach (TankFishSpawnData spawn in data.Fishes)
        {
            if (!GameStateQuery.CheckConditions(spawn.Condition, Location, null, null, null, Random.Shared))
                continue;
            for (int i = 0; i < spawn.RepeatCount; i++)
            {
                IList<ItemQueryResult> results = ItemQueryResolver.TryResolve(
                    spawn,
                    context,
                    filter: spawn.SearchMode,
                    false
                );
                if (results.Count == 0)
                    continue;
                foreach (
                    ItemQueryResult result in spawn.TakeCount > 0
                        ? Random.Shared.ShuffleInPlace(results).Take(Math.Min(spawn.TakeCount, results.Count))
                        : results
                )
                {
                    if (result.Item is Item fish)
                    {
                        heldItems.Add(fish);
                    }
                }
            }
        }
        refreshFishEvent.Fire();
    }

    public override Rectangle GetTankBounds()
    {
        return data.TankBounds;
    }

    public override void draw(SpriteBatch spriteBatch, int x, int y, float alpha = 1)
    {
        Rectangle tankBounds = GetTankBounds();
        Vector2 fishSortRegion = GetFishSortRegion();
        for (int i = 0; i < this.tankFish.Count; i++)
        {
            TankFish tankFish = this.tankFish[i];
            float drawLayer = Utility.Lerp(fishSortRegion.Y, fishSortRegion.X, tankFish.zPosition / 20f);
            drawLayer += drawLayer += 1E-07f * i;
            tankFish.Draw(spriteBatch, alpha, drawLayer);
        }
#if SDV17
        SpriteEffects spriteEffectsWhenPlaced = GetSpriteEffectsWhenPlaced();
        foreach (TankDecoration decoration in decorations)
        {
            float layerDepth = Utility.Lerp(fishSortRegion.Y, fishSortRegion.X, decoration.Position.Y / 20f) - 1E-06f;
            Vector2 position = Game1.GlobalToLocal(
                new Vector2(
                    tankBounds.Left + decoration.Position.X * 4f,
                    tankBounds.Bottom - 4 - decoration.Position.Y * 4f
                )
            );
            if (decoration.Texture == null)
            {
                Utility.DrawErrorTexture(
                    spriteBatch,
                    new Rectangle(
                        (int)position.X,
                        (int)position.Y,
                        decoration.SourceRect.Width,
                        decoration.SourceRect.Height
                    ),
                    layerDepth
                );
            }
            else
            {
                spriteBatch.Draw(
                    decoration.Texture,
                    position,
                    decoration.SourceRect,
                    Color.White * alpha,
                    0f,
                    new Vector2(decoration.SourceRect.Width / 2, decoration.SourceRect.Height - 4),
                    4f,
                    spriteEffectsWhenPlaced,
                    layerDepth
                );
            }
        }
#else
        for (int j = 0; j < floorDecorations.Count; j++)
        {
            KeyValuePair<Rectangle, Vector2>? floorDeco = floorDecorations[j];
            if (floorDeco.HasValue)
            {
                Vector2 value2 = floorDeco.Value.Value;
                Rectangle key = floorDeco.Value.Key;
                float layerDepth = Utility.Lerp(fishSortRegion.Y, fishSortRegion.X, value2.Y / 20f) - 1E-06f;
                spriteBatch.Draw(
                    GetAquariumTexture(),
                    Game1.GlobalToLocal(
                        new Vector2(tankBounds.Left + value2.X * 4f, tankBounds.Bottom - 4 - value2.Y * 4f)
                    ),
                    key,
                    Color.White * alpha,
                    0f,
                    new Vector2(key.Width / 2, key.Height - 4),
                    4f,
                    SpriteEffects.None,
                    layerDepth
                );
            }
        }
#endif

        if (data.DrawBubbles)
        {
            foreach (Vector4 bubble in bubbles)
            {
                float layerDepth2 = Utility.Lerp(fishSortRegion.Y, fishSortRegion.X, bubble.Z / 20f) - 1E-06f;
                spriteBatch.Draw(
                    GetAquariumTexture(),
                    Game1.GlobalToLocal(
                        new Vector2(tankBounds.Left + bubble.X, tankBounds.Bottom - 4 - bubble.Y - bubble.Z * 4f)
                    ),
                    new Rectangle(0, 240, 16, 16),
                    Color.White * alpha,
                    0f,
                    new Vector2(8f, 8f),
                    4f * bubble.W,
                    SpriteEffects.None,
                    layerDepth2
                );
            }
        }

        background?.Draw(spriteBatch, alpha, GetBaseDrawLayer());
        foreground?.Draw(spriteBatch, alpha, GetGlassDrawLayer());
    }

    public override bool checkForAction(Farmer who, bool justCheckingForActivity = false)
    {
        return false;
    }

    public override bool CanBeDeposited(Item item)
    {
        return false;
    }

    public override bool canBeRemoved(Farmer who)
    {
        return false;
    }
}

/// <summary>
/// Non player tanks that are displayed in a particular location
/// </summary>
internal static class LocationalFishTankManager
{
    public const string Prop_LocationalFishTanks = $"{ModEntry.ModId}_FishTanks";
    public const string Action_ReloadFishTank = $"{ModEntry.ModId}_ReloadFishTank";

    public static readonly PerScreen<List<LocationalFishTank>> locationalTanks = new(() => []);

    public static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.SaveLoaded += static (sender, e) => TankSetup(Game1.currentLocation);
        helper.Events.GameLoop.Saving += static (sender, e) => TankSetup(Game1.currentLocation);
        helper.Events.Player.Warped += static (sender, e) => TankSetup(e.NewLocation);

        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.RenderedStep += OnRenderedStep;
        helper.Events.Display.RenderingStep += OnRenderingStep;

        TriggerActionManager.RegisterAction(Action_ReloadFishTank, TriggerReloadFishTank);
    }

    private static bool TriggerReloadFishTank(
        string[] args,
        TriggerActionContext context,
        [NotNullWhen(false)] out string? error
    )
    {
        if (ArgUtility.TryGet(args, 1, out string? tankId, out error) && tankId == "ALL")
        {
            foreach (LocationalFishTank tank in locationalTanks.Value)
            {
                tank.RespawnFish();
            }
        }
        else
        {
            foreach (LocationalFishTank tank in locationalTanks.Value)
            {
                if (args.Contains(tank.key))
                    tank.RespawnFish();
            }
        }
        return true;
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (LocationalFishTank tank in locationalTanks.Value)
        {
            tank.updateWhenCurrentLocation(Game1.currentGameTime);
        }
    }

    private static void OnRenderingStep(object? sender, RenderingStepEventArgs e)
    {
        if (e.Step == RenderSteps.World_Background)
        {
            foreach (LocationalFishTank tank in locationalTanks.Value)
            {
                if (tank.DrawInBackground)
                    tank.draw(e.SpriteBatch, -1, -1);
            }
        }
    }

    private static void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step == RenderSteps.World)
        {
            foreach (LocationalFishTank tank in locationalTanks.Value)
            {
                if (!tank.DrawInBackground)
                    tank.draw(e.SpriteBatch, -1, -1);
            }
        }
    }

    public static void TankSetup(GameLocation location)
    {
        List<LocationalFishTank> tanks = locationalTanks.Value;
        tanks.Clear();

        if (location != null)
        {
            if (
                !(location.GetData()?.CustomFields?.TryGetValue(Prop_LocationalFishTanks, out string? prop) ?? false)
                && !(
                    location.Map != null
                    && location.Map.Properties != null
                    && location.TryGetMapProperty(Prop_LocationalFishTanks, out prop)
                )
            )
            {
                return;
            }
            foreach (string tankId in ArgUtility.SplitBySpaceQuoteAware(prop))
            {
                if (AssetManager.TankData.TryGetValue(tankId, out LocationalTankData? tankData))
                {
                    ModEntry.Log($"Locational Tank: {tankId}");
                    LocationalFishTank tank = new(location, tankId, tankData);
                    tanks.Add(tank);
                }
            }
        }

        locationalTanks.Value = tanks;
    }

    public static IList<T> ShuffleInPlace<T>(this Random rand, IList<T> listToShuffle)
    {
        int n = listToShuffle.Count;
        while (n > 1)
        {
            // https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
            n--;
            int k = rand.Next(n + 1);
            (listToShuffle[n], listToShuffle[k]) = (listToShuffle[k], listToShuffle[n]);
        }
        return listToShuffle;
    }
}
