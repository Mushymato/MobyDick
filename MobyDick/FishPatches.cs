using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;
using static StardewValley.Objects.TankFish;

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

    private Vector2 origin = new(Data.SpriteSize.X / 2, Data.SpriteSize.Y / 2);

    internal void Draw(Texture2D texture, SpriteBatch b, float alpha, float draw_layer)
    {
        float scale = Fish.GetScale();
        Rectangle sourceRect = Data.GetAquariumSourceRect(Fish.currentFrame, texture);
        b.Draw(
            texture,
            Game1.GlobalToLocal(Fish.GetWorldPosition()),
            sourceRect,
            Color.White * alpha,
            Data.RotateByVelocity ? Utility.Clamp(Fish.velocity.X, -0.5f, 0.5f) : 0f,
            origin,
            4f * scale,
            Fish.facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            draw_layer
        );
    }

    internal Rectangle GetBounds(Rectangle tankBounds)
    {
        Vector2 size = new(Data.SpriteSize.X, Data.SpriteSize.Y * 9f / 16);
        float scaleFactor = 4f * Fish.GetScale();
        size *= scaleFactor;
        FishType fishType = Fish.fishType;
        Vector2 position = Fish.position;
        float div =
            fishType == FishType.Crawl
            || fishType == FishType.Ground
            || fishType == FishType.Static
            || fishType == FishType.Hop
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

internal static class FishPatches
{
    internal static void Patch(IModHelper helper)
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.Draw)),
                transpiler: new HarmonyMethod(typeof(FishPatches), nameof(TankFish_Draw_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.GetBounds)),
                postfix: new HarmonyMethod(typeof(FishPatches), nameof(TankFish_GetBounds_Postfix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(FishingRod), "doPullFishFromWater"),
                postfix: new HarmonyMethod(typeof(FishPatches), nameof(FishingRod_doPullFishFromWater_Postfix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.draw)),
                transpiler: new HarmonyMethod(typeof(FishPatches), nameof(FishingRod_draw_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawWhenHeld)),
                prefix: new HarmonyMethod(typeof(FishPatches), nameof(SObject_drawWhenHeld_Prefix))
                {
                    priority = Priority.Last,
                }
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in TankFishDrawPatches:\n{err}", LogLevel.Error);
            return;
        }

        if (
            helper.ModRegistry.Get("shekurika.WaterFish") is IModInfo modInfo
            && modInfo.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod
        )
        {
            Assembly assembly = mod.GetType().Assembly;
            if (
                (visibleFishCustomFish = assembly.GetType("showFishInWater.CustomFish")) is not null
                && AccessTools.DeclaredMethod(visibleFishCustomFish, "Draw") is MethodInfo visibleFishCustomFishDraw
            )
            {
                ModEntry.Log($"Patching VisibleFish: {visibleFishCustomFishDraw}");
                showFishInWater_CustomFish_alpha = AccessTools.DeclaredField(visibleFishCustomFish, "alpha");
                harmony.Patch(
                    original: visibleFishCustomFishDraw,
                    transpiler: new HarmonyMethod(typeof(FishPatches), nameof(TankFish_Draw_Transpiler))
                );
            }
        }

        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    }

    private static bool SObject_drawWhenHeld_Prefix(
        Item __instance,
        SpriteBatch spriteBatch,
        Vector2 objectPosition,
        Farmer f
    )
    {
        ItemMetadata itemMetadata = ItemRegistry.GetMetadata(__instance.QualifiedItemId);
        if (itemMetadata.TypeIdentifier != "(O)")
            return true;
        if (!TryGetMBData(itemMetadata.LocalItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return true;
        Texture2D tankFishTx = aqf.Texture;
        Rectangle sourceRect = data.GetAquariumSourceRect();
        float originW = MathF.Min(sourceRect.Width, sourceRect.Height) / 2;
        Vector2 origin = new(originW, originW);
        spriteBatch.Draw(
            tankFishTx,
            new(objectPosition.X - Math.Sign(f.rotation) * 8, objectPosition.Y + (sourceRect.Height - 16) * 2f),
            sourceRect,
            Color.White,
            0f,
            origin,
            4f,
            SpriteEffects.None,
            Math.Max(0f, (f.StandingPixel.Y + 3) / 10000f)
        );
        return false;
    }

    private static bool TryGetMBData(
        string itemId,
        [NotNullWhen(true)] out MobyDickData? data,
        [NotNullWhen(true)] out AquariumFishData? aqf
    )
    {
        aqf = null;
        return AssetManager.MBData.TryGetValue(itemId, out data)
            && (aqf = data.AquariumFish) is not null
            && !aqf.IsErrorFish;
    }

    private static Texture2D FishingRod_Draw_ReplaceTexture(Texture2D existing, FishingRod rod)
    {
        if (rod.whichFish.TypeIdentifier != "(O)")
            return existing;
        if (!TryGetMBData(rod.whichFish.LocalItemId, out _, out AquariumFishData? aqf))
            return existing;
        return Game1.content.Load<Texture2D>(aqf.TextureName);
    }

    private static Rectangle FishingRod_Draw_ReplaceSourceRect(Rectangle existing, FishingRod rod)
    {
        if (rod.whichFish.TypeIdentifier != "(O)")
            return existing;
        if (!TryGetMBData(rod.whichFish.LocalItemId, out MobyDickData? data, out _))
            return existing;
        return data.GetAquariumSourceRect();
    }

    private static Vector2 FishingRod_Draw_ReplaceOrigin(Vector2 existing, Rectangle sourceRect)
    {
        if (sourceRect.Width == 16 && sourceRect.Height == 16)
            return existing;
        float originW = MathF.Min(sourceRect.Width, sourceRect.Height) / 2;
        return new(originW, originW);
    }

    private static IEnumerable<CodeInstruction> FishingRod_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_1420: ldarg.0
            // IL_1421: ldfld class StardewValley.ItemTypeDefinitions.ItemMetadata StardewValley.Tools.FishingRod::whichFish
            // IL_1426: callvirt instance class StardewValley.ItemTypeDefinitions.ParsedItemData StardewValley.ItemTypeDefinitions.ItemMetadata::GetParsedOrErrorData()
            // IL_142b: dup
            // IL_142c: callvirt instance class [MonoGame.Framework]Microsoft.Xna.Framework.Graphics.Texture2D StardewValley.ItemTypeDefinitions.ParsedItemData::GetTexture()
            // IL_1431: stloc.s 27
            // IL_1433: ldc.i4.0
            // IL_1434: ldloca.s 5
            // IL_1436: initobj valuetype [System.Runtime]System.Nullable`1<int32>
            // IL_143c: ldloc.s 5
            // IL_143e: callvirt instance valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Rectangle StardewValley.ItemTypeDefinitions.ParsedItemData::GetSourceRect(int32, valuetype [System.Runtime]System.Nullable`1<int32>)
            // IL_1443: stloc.s 28
            // IL_1445: ldarg.1
            // IL_1446: ldloc.s 27

            matcher
                .MatchEndForward([
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(FishingRod), nameof(FishingRod.whichFish))),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(typeof(ItemMetadata), nameof(ItemMetadata.GetParsedOrErrorData))
                    ),
                    new(OpCodes.Dup),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(typeof(ParsedItemData), nameof(ParsedItemData.GetTexture))
                    ),
                    new(inst => inst.IsStloc()),
                    new(OpCodes.Ldc_I4_0),
                    new(inst => inst.IsLdloc()),
                    new(OpCodes.Initobj),
                    new(inst => inst.IsLdloc()),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(typeof(ParsedItemData), nameof(ParsedItemData.GetSourceRect))
                    ),
                    new(inst => inst.IsStloc()),
                    new(OpCodes.Ldarg_1),
                    new(inst => inst.IsLdloc()),
                ])
                .ThrowIfNotMatch("ParsedItemData parsedOrErrorData ... parsedOrErrorData.GetSourceRect();");
            CodeInstruction stlocTexture = matcher.InstructionAt(-8);
            CodeInstruction ldlocTexture = matcher.InstructionAt(0);
            CodeInstruction stlocRectangle = matcher.InstructionAt(-2);
            // other ldloc very unlikely
            CodeInstruction ldlocRectangle = new(OpCodes.Ldloc_S, stlocRectangle.operand);

            // IL_14b4: callvirt instance void [MonoGame.Framework]Microsoft.Xna.Framework.Graphics.SpriteBatch::Draw(class [MonoGame.Framework]Microsoft.Xna.Framework.Graphics.Texture2D, valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Vector2, valuetype [System.Runtime]System.Nullable`1<valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Rectangle>, valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Color, float32, valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Vector2, float32, valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Graphics.SpriteEffects, float32)
            matcher
                .MatchStartForward([
                    new(
                        OpCodes.Callvirt,
                        AccessTools.DeclaredMethod(
                            typeof(SpriteBatch),
                            nameof(SpriteBatch.Draw),
                            [
                                typeof(Texture2D),
                                typeof(Vector2),
                                typeof(Rectangle?),
                                typeof(Color),
                                typeof(float),
                                typeof(Vector2),
                                typeof(float),
                                typeof(SpriteEffects),
                                typeof(float),
                            ]
                        )
                    ),
                ])
                .ThrowIfNotMatch("spritebatch.Draw")
                .Advance(1)
                .InsertAndAdvance([
                    ldlocTexture,
                    new(OpCodes.Ldarg_0),
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(FishPatches), nameof(FishingRod_Draw_ReplaceTexture))
                    ),
                    stlocTexture,
                    ldlocRectangle,
                    new(OpCodes.Ldarg_0),
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(FishPatches), nameof(FishingRod_Draw_ReplaceSourceRect))
                    ),
                    stlocRectangle,
                ]);
            // IL_15d6: ldc.r4 8
            // IL_15db: ldc.r4 8
            // IL_15e0: newobj instance void [MonoGame.Framework]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
            CodeInstruction replaceOrigin = new(
                OpCodes.Call,
                AccessTools.DeclaredMethod(typeof(FishPatches), nameof(FishingRod_Draw_ReplaceOrigin))
            );
            for (int i = 0; i < 2; i++)
            {
                matcher
                    .MatchEndForward([new(OpCodes.Ldc_R4, 8f), new(OpCodes.Ldc_R4, 8f), new(OpCodes.Newobj)])
                    .Advance(1)
                    .InsertAndAdvance([ldlocRectangle, replaceOrigin]);
            }

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in TankFish_Draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void FishingRod_doPullFishFromWater_Postfix(FishingRod __instance, ItemMetadata ___whichFish)
    {
        if (___whichFish.TypeIdentifier != "(O)")
            return;
        if (!TryGetMBData(___whichFish.LocalItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return;

        Texture2D tankFishTx = aqf.Texture;
        ParsedItemData parsedOrErrorData = ___whichFish.GetParsedOrErrorData();
        foreach (TemporaryAnimatedSprite anim in __instance.animations)
        {
            if (anim.textureName == parsedOrErrorData.TextureName)
            {
                anim.textureName = aqf.TextureName;
                anim.texture = tankFishTx;
                anim.sourceRect = new(0, 0, data.SpriteSize.X, data.SpriteSize.Y);
            }
        }
    }

    private static IEnumerable<CodeInstruction> TankFish_Draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            matcher
                .MatchEndForward([
                    new(OpCodes.Ldc_I4_S, (sbyte)24),
                    new(OpCodes.Ldloc_2),
                    new(OpCodes.Div),
                    new(OpCodes.Blt),
                ])
                .ThrowIfNotMatch("for (int i = 0; i < 24 / num2; i++) END")
                .Advance(1);
            Label label = matcher.Labels.Last();

            matcher
                .MatchEndBackwards([new(OpCodes.Br, label), new(OpCodes.Ldc_I4_0)])
                .ThrowIfNotMatch("for (int i = 0; i < 24 / num2; i++) START");
            matcher.Opcode = OpCodes.Ldarg_0;
            matcher.Operand = null;
            matcher
                .Advance(1)
                .Insert([
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(TankFish), "_texture")),
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Ldarg_2),
                    new(OpCodes.Ldarg_3),
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(FishPatches), nameof(TankFish_DrawReplace_Default))
                    ),
                    new(OpCodes.Brtrue, label),
                    new(OpCodes.Ldc_I4_0),
                ]);

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in TankFish_Draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static readonly ConditionalWeakTable<TankFish, TankFishDrawOverride?> TankFishDrawOverrides = [];

    private static TankFishDrawOverride? GetTankFishDrawOverride(TankFish fish) =>
        TankFishDrawOverrides.GetValue(fish, TankFishDrawOverride.Create);

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        TankFishDrawOverrides.Clear();
    }

    private static void TankFish_GetBounds_Postfix(
        TankFish __instance,
        ref Rectangle __result,
        ref FishTankFurniture ____tank
    )
    {
        if (GetTankFishDrawOverride(__instance) is not TankFishDrawOverride drawOverride)
        {
            return;
        }
        __result = drawOverride.GetBounds(____tank.GetTankBounds());
    }

    internal static Type? visibleFishCustomFish;
    internal static FieldInfo? showFishInWater_CustomFish_alpha = null;

    private static bool TankFish_DrawReplace_Default(
        TankFish fish,
        Texture2D texture,
        SpriteBatch b,
        float alpha,
        float draw_layer
    )
    {
        if (GetTankFishDrawOverride(fish) is not TankFishDrawOverride drawOverride)
        {
            return false;
        }
        if (showFishInWater_CustomFish_alpha != null && fish.GetType() == visibleFishCustomFish)
        {
            alpha = (float)(showFishInWater_CustomFish_alpha.GetValue(fish) ?? alpha);
        }
        drawOverride.Draw(texture, b, alpha, draw_layer);
        return true;
    }
}
