using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace MobyDick.Framework.AltFishing;

public enum FishingState
{
    Escaped,
    Catching,
    Caught,
}

public sealed class DredgeBar : BobberBar
{
    private readonly double healthMax;
    private double health;
    private double hookPos = 0.5;
    private double fishPos = 0;
    private double treasurePos = -1;
    private bool hasSonar = true;
    private float fade = 0f;
    private double healthChange;
    private double healthChangeTimer = 0;

    #region ui consts
    private const float SCALE = 4f;
    private const float BAR_WIDTH = 166 * SCALE;
    private const float HEALTH_BAR_MARGIN_X = 3 * SCALE;
    private const float HEALTH_BAR_MARGIN_Y = 4 * SCALE;
    private const float ROD_BAR_MARGIN_X = 3 * SCALE;
    private const double HEATLTH_CHANGE_TIMER_MAX = 200;
    private readonly Texture2D minigameTx;
    private static Rectangle rectHealthBarOverlay = new(0, 0, 176, 16);
    private static Rectangle rectHealthBar = new(0, 16, 176, 16);
    private static Rectangle rectHealthBarFill = new(192, 4, 1, 8);
    private static Rectangle rectSonarPanel = new(176, 32, 32, 32);
    private static Rectangle rectRodBar = new(0, 32, 176, 48);
    private static Rectangle rectRodHook = new(0, 80, 16, 48);
    private static Rectangle rectRodFish = new(16, 80, 16, 32);
    private static Rectangle rectRodTreasure = new(32, 80, 16, 32);
    private static Rectangle rectRodTreasureGolden = new(48, 80, 16, 32);
    private static Rectangle rectRodIconFish = new(64, 80, 32, 32);
    private static Rectangle rectRodIconFishKing = new(96, 80, 32, 32);
    private static Rectangle rectRodIconTreasure = new(128, 80, 32, 32);
    private static Rectangle rectRodIconTreasureGolden = new(160, 80, 32, 32);
    #endregion

    internal static IClickableMenu FromBobberBar(BobberBar bobberBar)
    {
        return new DredgeBar(
            bobberBar.whichFish,
            bobberBar.fishSize,
            bobberBar.treasure,
            bobberBar.bobbers,
            bobberBar.setFlagOnCatch,
            bobberBar.bossFish,
            bobberBar.challengeBaitFishes == 3 ? "(O)ChallengeBait" : string.Empty,
            bobberBar.goldenTreasure
        );
    }

    public DredgeBar(
        string whichFish,
        float fishSize,
        bool treasure,
        List<string> bobbers,
        string? setFlagOnCatch,
        bool isBossFish,
        string baitID = "",
        bool goldenTreasure = false
    )
        : base(whichFish, fishSize, treasure, bobbers, setFlagOnCatch, isBossFish, baitID, goldenTreasure)
    {
        this.healthMax = this.difficulty * 3;
        this.health = this.healthMax;
        ModEntry.Log($"DredgeBar {this.health}");
        minigameTx = ModEntry.help!.ModContent.Load<Texture2D>("assets/minigame.png");
        fishPos = RandP05ToP95();
        treasurePos = RandP05ToP95();
        SetHookPos();
    }

    private static double RandP05ToP95()
    {
        return 0.05 + Random.Shared.NextDouble() * 0.90;
    }

    private static Vector2 BarPosToVec(Vector2 pos, double portion)
    {
        return new(pos.X + ROD_BAR_MARGIN_X + (float)(portion * BAR_WIDTH), pos.Y + rectRodFish.Height / 2 * SCALE);
    }

    public override void update(GameTime time)
    {
        if (fadeIn)
        {
            ModEntry.Log($"fadeIn {fade}");
            fade += 0.1f;
            if (fade >= 1f)
            {
                fade = 1f;
                fadeIn = false;
            }
        }
        else if (fadeOut)
        {
            ModEntry.Log($"fadeOut {fade}");
            fade -= 0.1f;
            if (fade <= 0f)
            {
                scale = 0f;
                everythingShakeTimer = 0f;
                sparkleText = null;
                distanceFromCatching = 1f;
                base.update(time);
            }
        }
        if (!fadeOut)
        {
            Reposition();
            // hook
            SetHookPos();
            if (healthChangeTimer > 0)
            {
                healthChangeTimer -= time.ElapsedGameTime.TotalMilliseconds;
                if (healthChangeTimer <= 0)
                {
                    ChangeFishHealth(healthChange);
                    healthChange = 0;
                }
            }
            Game1.player.CurrentTool?.tickUpdate(time, Game1.player);
        }
    }

    private void SetHookPos()
    {
        double theta = Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 1200.0;
        hookPos = 0.5 + Math.Sin(Math.PI * theta) / 2.0;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (healthChange == 0)
        {
            if (Math.Abs(hookPos - fishPos) <= 0.1)
            {
                StartChangeFishHealth(-15);
            }
            else
            {
                // StartChangeFishHealth(15);
            }
        }
    }

    public void StartChangeFishHealth(double change)
    {
        double healthAfterChange = health + change;
        if (healthAfterChange > healthMax)
        {
            healthChange = healthMax - change;
        }
        else if (healthAfterChange < 0)
        {
            healthChange = -health;
        }
        else
        {
            healthChange = change;
            fishPos = RandP05ToP95();
        }
        ModEntry.Log($"Fish will change {health} + {change} = {healthAfterChange}");
        if (healthChange != 0)
            healthChangeTimer = HEATLTH_CHANGE_TIMER_MAX;
    }

    public void ChangeFishHealth(double change)
    {
        ModEntry.Log($"Fish {health} + {change}");
        health += change;
        if (health <= 0)
        {
            // caught
            Game1.playSound("jingle1");
            distanceFromCatching = 1f;
            fadeOut = true;
            handledFishResult = true;
            if (perfect)
            {
                // sparkleText = new SparklingText(
                //     Game1.dialogueFont,
                //     Game1.content.LoadString("Strings\\UI:BobberBar_Perfect"),
                //     Color.Yellow,
                //     Color.White,
                //     rainbow: false,
                //     0.1,
                //     1500
                // );
                // if (Game1.isFestival())
                // {
                //     Game1.CurrentEvent.perfectFishing();
                // }
            }
            else if (fishSize == maxFishSize)
            {
                fishSize--;
            }
        }
        else if (health >= healthMax)
        {
            // escaped
            Game1.playSound("fishEscape");
            distanceFromCatching = 0f;
            fadeOut = true;
            handledFishResult = true;
        }
        if (change > 0)
        {
            perfect = false;
        }
    }

    public override void Reposition()
    {
        xPositionOnScreen = (int)(Game1.player.Position.X - rectHealthBar.Width / 2 * SCALE + 64) - Game1.viewport.X;
        yPositionOnScreen = (int)(Game1.player.Position.Y + 64) - Game1.viewport.Y;
    }

    public override void draw(SpriteBatch b)
    {
        Game1.StartWorldDrawInUI(b);

        Vector2 pos = new(xPositionOnScreen, yPositionOnScreen);

        // health bar
        DrawMinigamePart(b, pos, rectHealthBar);
        b.Draw(
            minigameTx,
            new(pos.X + HEALTH_BAR_MARGIN_X, pos.Y + HEALTH_BAR_MARGIN_Y),
            rectHealthBarFill,
            Color.White * fade,
            0f,
            Vector2.Zero,
            new Vector2(
                (float)(
                    BAR_WIDTH
                    * ((health + healthChange * (1 - (healthChangeTimer / HEATLTH_CHANGE_TIMER_MAX))) / healthMax)
                ),
                SCALE
            ),
            SpriteEffects.None,
            0.91f
        );
        DrawMinigamePart(b, pos, rectHealthBarOverlay, layerDepth: 0.92f);

        pos = new(pos.X, pos.Y + rectHealthBar.Height * SCALE);

        // rod bar
        DrawMinigamePart(b, pos, rectRodBar);
        if (hasSonar)
        {
            Vector2 sonarPos = new(pos.X + (rectRodBar.Width - 4) * SCALE, pos.Y);
            DrawMinigamePart(b, sonarPos, rectSonarPanel);
            fishObject.drawInMenu(b, new(sonarPos.X + 24, sonarPos.Y + 16), 1f);
        }

        // fish & treasure bars
        Vector2 treasurePosVec = Vector2.Zero;
        if (!treasureCaught)
        {
            treasurePosVec = BarPosToVec(pos, treasurePos);
            DrawMinigamePartCentered(
                b,
                treasurePosVec,
                goldenTreasure ? rectRodTreasureGolden : rectRodTreasure,
                layerDepth: 0.91f
            );
        }
        Vector2 fishPosVec = BarPosToVec(pos, fishPos);
        DrawMinigamePartCentered(b, fishPosVec, rectRodFish, layerDepth: 0.915f);

        // hook
        DrawMinigamePartCentered(b, BarPosToVec(pos, hookPos), rectRodHook, layerDepth: 0.92f);

        // fish icon
        bool wouldHit = Math.Abs(hookPos - fishPos) <= 0.1;
        if (!treasureCaught)
        {
            DrawMinigamePartCentered(
                b,
                treasurePosVec,
                goldenTreasure ? rectRodIconTreasureGolden : rectRodIconTreasure,
                drawScale: 2f,
                layerDepth: 0.93f
            );
        }
        DrawMinigamePartCentered(
            b,
            fishPosVec,
            bossFish ? rectRodIconFishKing : rectRodIconFish,
            drawScale: wouldHit ? 3f : 2f,
            layerDepth: 0.935f
        );

        Game1.EndWorldDrawInUI(b);
    }

    private void DrawMinigamePart(
        SpriteBatch b,
        Vector2 pos,
        Rectangle rect,
        float drawScale = SCALE,
        float layerDepth = 0.9f
    )
    {
        b.Draw(minigameTx, pos, rect, Color.White * fade, 0f, Vector2.Zero, drawScale, SpriteEffects.None, layerDepth);
    }

    private void DrawMinigamePartCentered(
        SpriteBatch b,
        Vector2 pos,
        Rectangle rect,
        float drawScale = SCALE,
        float layerDepth = 0.9f
    )
    {
        b.Draw(
            minigameTx,
            pos,
            rect,
            Color.White * fade,
            0f,
            new Vector2(rect.Width / 2, rect.Height / 2),
            drawScale,
            SpriteEffects.None,
            layerDepth
        );
    }
}
