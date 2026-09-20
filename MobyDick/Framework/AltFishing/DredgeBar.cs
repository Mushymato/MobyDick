using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
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
    private readonly double regen;
    private readonly double attack;
    private double hookPos = 0.5;
    private double fishPos = 0;
    private double fishPosPrev = 0;
    private double fishPosNext = 0;
    private float fishScale = 0f;
    private readonly double treasurePos = -1;
    private readonly bool hasSonar = true;
    private float fade = 0f;
    private double hookTimer = 0;
    private double healthChange;
    private readonly double hookLeeway = 0.06;

    private double fishPosTimer = 0;
    private double healthChangeTimer = 0;
    private double fishEscapeTimer = -1;
    private double fishAppearTimer = FISH_APPEAR_TIMER_MAX;

    #region ui consts
    private const float SCALE = 4f;
    private const float BAR_WIDTH = 166 * SCALE;
    private const float HEALTH_BAR_MARGIN_X = 3 * SCALE;
    private const float HEALTH_BAR_MARGIN_Y = 4 * SCALE;
    private const float ROD_BAR_MARGIN_X = 3 * SCALE;
    private const double FISH_POS_TIMER_MAX = 1200;
    private const double FISH_POS_TIMER_MAX_QUART = FISH_POS_TIMER_MAX * 0.75;
    private const double REGEN_TIMER_BASE = 250;
    private const double HEATLTH_CHANGE_TIMER_MAX = 300;
    private const double FISH_APPEAR_TIMER_MAX = 300;
    private const double FISH_ESCAPE_TIMER_MAX = 2000;
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
    private static Color Greeeen = Utility.getRedToGreenLerpColor(1.0f);
    #endregion

    internal static IClickableMenu FromBobberBar(BobberBar bobberBar)
    {
        DredgeBar dredgeBar = new(
            bobberBar.whichFish,
            0,
            bobberBar.treasure,
            bobberBar.bobbers,
            bobberBar.setFlagOnCatch,
            bobberBar.bossFish,
            bobberBar.challengeBaitFishes == 3 ? "(O)ChallengeBait" : string.Empty,
            bobberBar.goldenTreasure
        )
        {
            fishSize = bobberBar.fishSize,
            fishQuality = bobberBar.fishQuality,
        };
        return dredgeBar;
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
        healthMax = difficulty * 2;
        health = healthMax;
        regen = Math.Max(REGEN_TIMER_BASE - (difficulty * 1.5), 50);
        attack = 12 + Game1.player.FishingLevel;
        if (baitID == "(O)DeluxeBait")
        {
            attack += 6;
        }
        ModEntry.Log(
            $"DredgeBar health={health} attack={attack} regen={regen} fishQuality={fishQuality} fishSize={fishSize}"
        );
        minigameTx = ModEntry.help!.ModContent.Load<Texture2D>("assets/minigame.png");
        fishPos = RandP05ToP95();
        fishPosPrev = fishPos;
        fishPosNext = fishPos;
        hasSonar = bobbers.Contains("(O)SonarBobber");
        if (treasure)
            treasurePos = RandP05ToP95();
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
        if (sparkleText != null)
        {
            if (sparkleText.update(time))
            {
                sparkleText = null;
            }
            return;
        }
        if (fadeIn)
        {
            fade += 0.1f;
            if (fade >= 1f)
            {
                fade = 1f;
                fadeIn = false;
            }
        }
        else if (fadeOut)
        {
            ModEntry.Log($"fadeOut {fade} perfect={perfect} fishQuality={fishQuality} fishSize={fishSize}");
            fade -= 0.1f;
            if (fade <= 0f)
            {
                unReelSound?.Stop(AudioStopOptions.Immediate);
                reelSound?.Stop(AudioStopOptions.Immediate);
                unReelSound = null;
                reelSound = null;
                scale = 0f;
                everythingShakeTimer = 0f;
                sparkleText = null;
                base.update(time);
            }
        }
        if (!fadeOut && !handledFishResult)
        {
            if (fishAppearTimer > 0)
            {
                fishAppearTimer -= time.ElapsedGameTime.TotalMilliseconds;
                if (fishAppearTimer <= 0)
                {
                    fishPos = RandP05ToP95();
                    fishPosNext = RandP05ToP95();
                    fishPosTimer = FISH_POS_TIMER_MAX;
                }
            }
            else if (fishScale < 1f && healthChangeTimer <= 0)
            {
                fishScale = Math.Min(1f, fishScale + 0.1f);
            }
            else if (fishScale > 0f && healthChangeTimer > 0)
            {
                fishScale = Math.Min(0f, fishScale - 0.1f);
            }
            if (treasure)
            {
                if (treasureAppearTimer > 0)
                {
                    treasureAppearTimer -= (float)time.ElapsedGameTime.TotalMilliseconds;
                }
                else if (treasureScale < 1f && !treasureCaught)
                {
                    treasureScale = Math.Min(1f, treasureScale + 0.1f);
                }
                else if (treasureScale > 0f && treasureCaught)
                {
                    treasureScale = Math.Min(0f, treasureScale - 0.1f);
                }
            }
            Reposition();
            if (healthChangeTimer > 0)
            {
                healthChangeTimer -= time.ElapsedGameTime.TotalMilliseconds;
                unReelSound?.Stop(AudioStopOptions.Immediate);
                if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
                {
                    Game1.playSound("fastReel", out reelSound);
                }
                if (healthChangeTimer <= 0)
                {
                    reelSound?.Stop(AudioStopOptions.Immediate);
                    ChangeFishHealth(healthChange);
                    healthChange = 0;
                    fishAppearTimer = FISH_APPEAR_TIMER_MAX;
                }
            }
            else
            {
                reelSound?.Stop(AudioStopOptions.Immediate);
                if (unReelSound == null || unReelSound.IsStopped)
                {
                    Game1.playSound("slowReel", out unReelSound);
                }
                if (health < healthMax && fishScale >= 1f)
                {
                    ChangeFishHealth(time.ElapsedGameTime.TotalMilliseconds / regen);
                }
                // fish motion
                UpdateFishPos(time);
                // fish size
                fishSizeReductionTimer -= time.ElapsedGameTime.Milliseconds;
                if (fishSizeReductionTimer <= 0)
                {
                    fishSize = Math.Max(minFishSize, fishSize - 1);
                    fishSizeReductionTimer = 800;
                }
                // fish escaping
                if (fishEscapeTimer > 0)
                {
                    fishEscapeTimer -= time.ElapsedGameTime.TotalMilliseconds;
                    if (fishEscapeTimer <= 0)
                    {
                        // escaped
                        SetFishResult(false);
                    }
                }
            }
            Game1.player.CurrentTool?.tickUpdate(time, Game1.player);
            // hook
            SetHookPos(time);
        }
    }

    private void UpdateFishPos(GameTime time)
    {
        fishPosTimer -= time.ElapsedGameTime.TotalMilliseconds;
        if (
            fishPosTimer <= 0
            || fishPosNext == fishPos
            || (fishPosTimer <= FISH_POS_TIMER_MAX_QUART && Game1.random.NextDouble() < (double)(difficulty / 1000f))
        )
        {
            fishPosPrev = fishPos;
            switch (motionType)
            {
                case 0:
                    UpdateFishMotion_Mixed();
                    break;
                case 1:
                    UpdateFishMotion_Dart();
                    break;
                case 2:
                    UpdateFishMotion_Smooth();
                    break;
                case 3:
                    UpdateFishMotion_Floater();
                    break;
                case 4:
                    UpdateFishMotion_Sinker();
                    break;
            }
            fishPosTimer = FISH_POS_TIMER_MAX;
        }
        else
        {
            fishPos = Lerp(fishPosNext, fishPosPrev, fishPosTimer / FISH_POS_TIMER_MAX);
        }
    }

    public static double Lerp(double a, double b, double t)
    {
        return a + t * (b - a);
    }

    private void UpdateFishMotion_Mixed()
    {
        switch (Random.Shared.Next(1, 5))
        {
            case 1:
                UpdateFishMotion_Dart();
                break;
            case 2:
                UpdateFishMotion_Smooth();
                break;
            case 3:
                UpdateFishMotion_Floater();
                break;
            case 4:
                UpdateFishMotion_Sinker();
                break;
        }
    }

    private void UpdateFishMotion_Dart()
    {
        fishPosNext = RandP05ToP95();
    }

    private void UpdateFishMotion_Smooth()
    {
        fishPosNext = fishPos <= 0.5 ? 0.95 : 0.05;
    }

    private void UpdateFishMotion_Floater()
    {
        fishPosNext = Math.Clamp(fishPos + 0.1 + (Random.Shared.NextDouble() / 50), 0.05, 0.95);
    }

    private void UpdateFishMotion_Sinker()
    {
        fishPosNext = Math.Clamp(fishPos - (0.1 + (Random.Shared.NextDouble() / 50)), 0.05, 0.95);
    }

    private void SetFishResult(bool caught)
    {
        distanceFromCatching = caught ? 1f : 0f;
        fadeOut = true;
        handledFishResult = true;
        fishEscapeTimer = 0.001;
    }

    private void SetHookPos(GameTime time)
    {
        hookTimer += time.ElapsedGameTime.TotalMilliseconds;
        double theta = hookTimer / 1200.0;
        hookPos = 0.5 + Math.Cos(Math.PI * (theta + 1)) / 2.0;
    }

    private bool WouldHook()
    {
        return Math.Abs(hookPos - fishPos) <= hookLeeway;
    }

    private bool WouldHookTreasure()
    {
        if (!treasure)
            return false;
        return Math.Abs(hookPos - treasurePos) <= hookLeeway;
    }

    private void NotPerfect()
    {
        perfect = false;
        if (challengeBaitFishes > 0)
        {
            challengeBaitFishes--;
            if (challengeBaitFishes <= 0)
            {
                distanceFromCatching = 0f;
            }
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (sparkleText != null)
            return;
        if (healthChange == 0)
        {
            if (WouldHook())
            {
                Game1.playSound("dwop");
                StartChangeFishHealth(-(attack + Random.Shared.Next(8)));
            }
            else
            {
                Game1.playSound("dwoop");
                NotPerfect();
            }
            if (WouldHookTreasure())
            {
                treasureCaught = true;
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
        }
        ModEntry.Log($"Fish will change by {change}: {health} + {healthChange} = {healthAfterChange}");
        if (healthChange != 0)
        {
            healthChangeTimer = HEATLTH_CHANGE_TIMER_MAX;
        }
    }

    public void ChangeFishHealth(double change)
    {
        health += change;
        if (health <= 0)
        {
            // caught
            Game1.playSound("jingle1");
            SetFishResult(true);
            if (perfect)
            {
                sparkleText = new SparklingText(
                    Game1.dialogueFont,
                    Game1.content.LoadString("Strings\\UI:BobberBar_Perfect"),
                    Color.Yellow,
                    Color.White,
                    rainbow: false,
                    0.1,
                    1500
                );
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
            // escaping
            fishEscapeTimer = FISH_ESCAPE_TIMER_MAX;
            NotPerfect();
        }
        else
        {
            fishEscapeTimer = -1;
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
            (
                fishEscapeTimer >= 0
                    ? Utility.getRedToGreenLerpColor((float)(fishEscapeTimer / FISH_ESCAPE_TIMER_MAX))
                    : Greeeen
            ) * fade,
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
            fishObject.drawInMenu(b, new(sonarPos.X + 36, sonarPos.Y + 16), 1f);
        }

        // fish & treasure bars
        Vector2 treasurePosVec = Vector2.Zero;
        bool drawTreasure = treasure && treasureScale > 0;
        if (drawTreasure)
        {
            treasurePosVec = BarPosToVec(pos, treasurePos);
            DrawMinigamePartCentered(
                b,
                treasurePosVec,
                goldenTreasure ? rectRodTreasureGolden : rectRodTreasure,
                drawScale: SCALE * treasureScale,
                layerDepth: 0.91f
            );
        }
        Vector2 fishPosVec = BarPosToVec(pos, fishPos);
        DrawMinigamePartCentered(b, fishPosVec, rectRodFish, layerDepth: 0.915f, drawScale: SCALE * fishScale);

        // hook
        DrawMinigamePartCentered(b, BarPosToVec(pos, hookPos), rectRodHook, layerDepth: 0.92f);

        // fish icon
        if (drawTreasure)
        {
            DrawMinigamePartCentered(
                b,
                treasurePosVec,
                goldenTreasure ? rectRodIconTreasureGolden : rectRodIconTreasure,
                drawScale: (WouldHookTreasure() ? 3f : 2f) * treasureScale,
                layerDepth: 0.93f
            );
        }
        DrawMinigamePartCentered(
            b,
            fishPosVec,
            bossFish ? rectRodIconFishKing : rectRodIconFish,
            drawScale: (WouldHook() ? 3f : 2f) * fishScale,
            layerDepth: 0.935f
        );

        // sparkle text
        sparkleText?.draw(b, new Vector2(xPositionOnScreen - 16, yPositionOnScreen - 64));

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
        if (drawScale <= 0 || fade <= 0)
            return;
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
        if (drawScale <= 0 || fade <= 0)
            return;
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
