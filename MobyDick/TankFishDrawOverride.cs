using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;
using TankFishType = StardewValley.Objects.TankFish.FishType;

namespace MobyDick;

internal sealed record TankFishDrawOverride(TankFish Fish, MobyDickData Data)
{
    internal static TankFishDrawOverride? Create(TankFish key)
    {
        if (AssetManager.MBData.TryGetValue(key.fishItemId, out MobyDickData? data) && data.SpriteSize.X > 0)
        {
            return new(key, data);
        }
        return null;
    }

    private Vector2 origin = new Vector2(Data.SpriteSize.X / 2f, Data.SpriteSize.Y / 2f) + Data.DrawOriginOffset;

    internal void Draw(Texture2D texture, SpriteBatch b, float alpha, float draw_layer)
    {
        float scale = Fish.GetScale() * Data.DrawScaleInTank;
        float heightVariance = Fish.fishType switch
        {
            TankFishType.Eel or TankFishType.Crawl or TankFishType.Ground or TankFishType.Static => 0f,
            _ => (float)
                Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 1.25 + (double)(Fish.position.X / 32f))
                * 2f,
        };
        Rectangle sourceRect = Data.GetAquariumSourceRect(Fish.currentFrame, texture);

        Vector2 drawPos = Fish.GetWorldPosition();
        if (heightVariance > 0)
            drawPos += new Vector2(0f, heightVariance * scale);

        SpriteEffects flip = Fish.facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        bool isEel = Fish.fishType == TankFishType.Eel;

        int wiggleLen = Data.WiggleSegmentLength;

        if (wiggleLen <= 0 || wiggleLen > sourceRect.Width)
        {
            b.Draw(
                texture,
                Game1.GlobalToLocal(drawPos),
                sourceRect,
                Color.White * alpha,
                Data.RotateByVelocity ? Utility.Clamp(Fish.velocity.X, -0.5f, 0.5f) : 0f,
                origin,
                scale,
                flip,
                draw_layer
            );
        }
        else
        {
            Vector2 wiggleOrigin = new((Fish.facingLeft ? -1 : 1) * origin.X, origin.Y);
            for (int i = 0; i < sourceRect.Width / wiggleLen; i++)
            {
                float wiggleX = i * wiggleLen * (Fish.facingLeft ? -1 : 1);

                float num10 = (float)(i * wiggleLen) / (isEel ? 20f : 10f);
                num10 = 1f - num10;
                float value = Fish.velocity.Length() / 1f;
                float num11 = 1f;
                float num12 = 0f;
                value = Utility.Clamp(value, 0.2f, 1f);
                num10 = Utility.Clamp(num10, 0f, 1f);
                if (isEel)
                {
                    num10 = 1f;
                    value = 1f;
                    num11 = 0.1f;
                    num12 = 4f;
                }
                if (Fish.facingLeft)
                {
                    num12 *= -1f;
                }
                float wiggleY = (float)(
                    Math.Sin(
                        i * 20
                            + Game1.currentGameTime.TotalGameTime.TotalSeconds * 25.0 * (double)num11
                            + (double)(num12 * Fish.position.X / 16f)
                    )
                    * (double)num10
                    * (double)value
                );

                b.Draw(
                    texture,
                    Game1.GlobalToLocal(drawPos + new Vector2(wiggleX, wiggleY) * scale),
                    new(sourceRect.X + i * wiggleLen, sourceRect.Y, wiggleLen, sourceRect.Height),
                    Color.White * alpha,
                    0f,
                    wiggleOrigin,
                    scale,
                    flip,
                    draw_layer
                );
            }
        }
    }

    internal Rectangle GetBounds(Rectangle tankBounds)
    {
        Vector2 size = new(Data.SpriteSize.X, Data.SpriteSize.Y * 9f / 16);
        float scaleFactor = 4f * Fish.GetScale();
        size *= scaleFactor;
        TankFishType fishType = Fish.fishType;
        Vector2 position = Fish.position;
        float div =
            fishType == TankFishType.Crawl
            || fishType == TankFishType.Ground
            || fishType == TankFishType.Static
            || fishType == TankFishType.Hop
                ? 1f
                : 2f;
        return new Rectangle(
            (int)(position.X - size.X / 2),
            (int)(tankBounds.Height - position.Y - size.Y / div),
            (int)size.X,
            (int)size.Y
        );
    }
}
