using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MobyDick.Model;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;

namespace MobyDick.Framework;

internal static partial class Patches
{
    internal static void Patch_Drawing(IModHelper helper, Harmony harmony)
    {
        try
        {
            // introduce special drawing logic for tankfish
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.Draw)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(TankFish_Draw_Transpiler))
            );
            // change bounds for purpose of turnaround
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.GetBounds)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(TankFish_GetBounds_Postfix))
            );
            // change turnaround logic
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.Update)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(TankFish_Update_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Patch_Drawing(primary):\n{err}", LogLevel.Error);
            return;
        }

        try
        {
            // change how fish looks in other scenarios

            // fished up from water
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(FishingRod), "doPullFishFromWater"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(FishingRod_doPullFishFromWater_Postfix))
            );
            // held after fished
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.draw)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(FishingRod_draw_Transpiler))
            );

            // eat the fish
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.showEatingItem)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Farmer_showEatingItem_Prefix)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Farmer_showEatingItem_Postfix))
            );
            // fish frenzy
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(GameLocation),
                    nameof(GameLocation.UpdateWhenCurrentLocation)
                ),
                postfix: new HarmonyMethod(typeof(Patches), nameof(GameLocation_UpdateWhenCurrentLocation_Postfix))
            );
            // held above head
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawWhenHeld)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(SObject_drawWhenHeld_Prefix))
                {
                    priority = Priority.Last,
                }
            );
            // put on furniture
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(Furniture_draw_Transpiler))
            );
            // smoked
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ColoredObject), "drawSmokedFish"),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ColoredObject_drawSmokedFish_Transpiler))
            );
            // fish pond jumping fish
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(JumpingFish), nameof(JumpingFish.Draw)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(JumpingFish_Draw_Postfix))
                {
                    priority = Priority.Last,
                }
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Patch_Drawing(secondary):\n{err}", LogLevel.Error);
            return;
        }

        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.RenderingHud += OnRenderingHud;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Display.RenderingActiveMenu += OnRenderingActiveMenu;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;

        // visible fish patches
        try
        {
            if (
                helper.ModRegistry.Get("shekurika.WaterFish") is IModInfo modInfo
                && modInfo.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod
            )
            {
                Assembly assembly = mod.GetType().Assembly;
                if ((visibleFishCustomFish = assembly.GetType("showFishInWater.CustomFish")) is not null)
                {
                    if (
                        AccessTools.DeclaredMethod(visibleFishCustomFish, "Draw")
                        is MethodInfo visibleFishCustomFishDraw
                    )
                    {
                        ModEntry.Log($"Patching VisibleFish: {visibleFishCustomFishDraw}");
                        showFishInWater_CustomFish_alpha = AccessTools.DeclaredField(visibleFishCustomFish, "alpha");
                        harmony.Patch(
                            original: AccessTools.DeclaredMethod(visibleFishCustomFish, "Draw"),
                            transpiler: new HarmonyMethod(typeof(Patches), nameof(TankFish_Draw_Transpiler))
                        );
                    }
                    if (
                        AccessTools.DeclaredMethod(visibleFishCustomFish, "Update")
                        is MethodInfo visibleFishCustomFishUpdate
                    )
                    {
                        ModEntry.Log($"Patching VisibleFish: {visibleFishCustomFishUpdate}");
                        harmony.Patch(
                            original: visibleFishCustomFishUpdate,
                            postfix: new HarmonyMethod(typeof(Patches), nameof(VisibleFish_Update_Postfix))
                        );
                    }
                    if (
                        AccessTools.DeclaredMethod(visibleFishCustomFish, "GetBounds")
                        is MethodInfo visibleFishCustomFishGetBounds
                    )
                    {
                        ModEntry.Log($"Patching VisibleFish: {visibleFishCustomFishGetBounds}");
                        harmony.Patch(
                            original: visibleFishCustomFishGetBounds,
                            postfix: new HarmonyMethod(typeof(Patches), nameof(TankFish_GetBounds_Postfix))
                        );
                    }
                }
            }
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Patch_Drawing(VisibleFish):\n{err}", LogLevel.Error);
            return;
        }
    }

    private static CodeInstruction StlocToLdloca(CodeInstruction stloc)
    {
        if (stloc.opcode == OpCodes.Stloc_S)
        {
            return new(OpCodes.Ldloca_S, stloc.operand);
        }
        else if (stloc.opcode == OpCodes.Stloc)
        {
            return new(OpCodes.Ldloca, stloc.operand);
        }
        sbyte index;
        if (stloc.opcode == OpCodes.Stloc_0)
        {
            index = 0;
        }
        else if (stloc.opcode == OpCodes.Stloc_1)
        {
            index = 1;
        }
        else if (stloc.opcode == OpCodes.Stloc_2)
        {
            index = 2;
        }
        else if (stloc.opcode == OpCodes.Stloc_3)
        {
            index = 3;
        }
        else
        {
            return new(OpCodes.Nop);
        }
        return new(OpCodes.Ldloca_S, index);
    }

    private static bool IsInMenuDraw = false;

    private static void OnRenderingHud(object? sender, RenderingHudEventArgs e) => IsInMenuDraw = true;

    private static void OnRenderedHud(object? sender, RenderedHudEventArgs e) => IsInMenuDraw = false;

    private static void OnRenderingActiveMenu(object? sender, RenderingActiveMenuEventArgs e) => IsInMenuDraw = true;

    private static void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e) => IsInMenuDraw = false;

    private static void ColoredObject_drawSmokedFish_ReplaceVars(
        ColoredObject smokedFish,
        ref Texture2D texture,
        ref Rectangle sourceRect,
        ref Vector2 origin
    )
    {
        if (IsInMenuDraw)
            return;
        if (
            smokedFish.GetPreservedItemId() is not string preserveId
            || !TryGetMBData(preserveId, out MobyDickData? data, out AquariumFishData? aqf)
        )
            return;
        texture = Game1.content.Load<Texture2D>(aqf.TextureName);
        sourceRect = data.GetAquariumSourceRect();
        origin = new(sourceRect.Width / 2f, sourceRect.Height / 2f);
        return;
    }

    private static IEnumerable<CodeInstruction> ColoredObject_drawSmokedFish_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_0000: ldloca.s 0
            // IL_0002: ldc.r4 8
            // IL_0007: ldc.r4 8
            // IL_000c: call instance void [MonoGame.Framework]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
            matcher
                .MatchStartForward([
                    new(OpCodes.Ldloca_S),
                    new(OpCodes.Ldc_R4, 8f),
                    new(OpCodes.Ldc_R4, 8f),
                    new(OpCodes.Call, AccessTools.Constructor(typeof(Vector2), [typeof(float), typeof(float)])),
                ])
                .ThrowIfNotMatch("new Vector2(8f, 8f)");
            CodeInstruction ldlocaOrigin = matcher.Instruction;
            // IL_0024: call class StardewValley.ItemTypeDefinitions.ParsedItemData StardewValley.ItemRegistry::GetDataOrErrorItem(string)
            // IL_0029: dup
            // IL_002a: callvirt instance class [MonoGame.Framework]Microsoft.Xna.Framework.Graphics.Texture2D StardewValley.ItemTypeDefinitions.ParsedItemData::GetTexture()
            // IL_002f: stloc.2
            // IL_0030: ldc.i4.0
            // IL_0031: ldloca.s 5
            // IL_0033: initobj valuetype [System.Runtime]System.Nullable`1<int32>
            // IL_0039: ldloc.s 5
            // IL_003b: callvirt instance valuetype [MonoGame.Framework]Microsoft.Xna.Framework.Rectangle StardewValley.ItemTypeDefinitions.ParsedItemData::GetSourceRect(int32, valuetype [System.Runtime]System.Nullable`1<int32>)
            // IL_0040: stloc.3
            matcher
                .MatchEndForward([
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(ItemRegistry), nameof(ItemRegistry.GetDataOrErrorItem))
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
                ])
                .ThrowIfNotMatch("ParsedItemData parsedOrErrorData ... parsedOrErrorData.GetSourceRect();");
            CodeInstruction ldlocaRect = StlocToLdloca(matcher.InstructionAt(-1));
            CodeInstruction ldlocaTexture = StlocToLdloca(matcher.InstructionAt(-7));
            matcher.Insert([
                new(OpCodes.Ldarg_0),
                ldlocaTexture,
                ldlocaRect,
                ldlocaOrigin,
                new(
                    OpCodes.Call,
                    AccessTools.DeclaredMethod(typeof(Patches), nameof(ColoredObject_drawSmokedFish_ReplaceVars))
                ),
            ]);

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in ColoredObject_drawSmokedFish_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void ModifyFishTAS(
        MobyDickData data,
        AquariumFishData aqf,
        Texture2D tankFishTx,
        ParsedItemData parsedOrErrorData,
        TemporaryAnimatedSprite anim
    )
    {
        if (
            anim.textureName == parsedOrErrorData.TextureName
            && anim.sourceRect.Width == 16
            && anim.sourceRect.Height == 16
        )
        {
            anim.textureName = aqf.TextureName;
            anim.texture = tankFishTx;
            anim.sourceRect = data.GetAquariumSourceRect();
            anim.position.X -= (anim.sourceRect.Width - 16) * anim.scale / 2;
            anim.position.X -= (anim.sourceRect.Height - 16) * anim.scale / 2;
        }
    }

    private static IEnumerable<CodeInstruction> Furniture_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
#pragma warning disable AvoidNetField // Avoid Netcode types when possible
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            CodeMatch inst = new(OpCodes.Ldfld, AccessTools.Field(typeof(Furniture), nameof(Furniture.isOn)));

            inst.ToString();
            // IL_075a: ldarg.0
            // IL_075b: ldfld class Netcode.NetBool StardewValley.Object::isOn
            // IL_0760: callvirt instance !0 class Netcode.NetFieldBase`2<bool, class Netcode.NetBool>::get_Value
            // IL_0765: brfalse.s IL_0778

            matcher
                .MatchStartForward([
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(SObject), nameof(SObject.isOn))),
                    new(OpCodes.Callvirt, AccessTools.Method(typeof(Netcode.NetBool), nameof(Netcode.NetBool.Value))),
                    new(OpCodes.Brfalse),
                ])
                .ThrowIfNotMatch("if (this.IsOn.Value...");

            matcher.CreateLabel(out Label AfterHeldObject);
            // IL_0476: ldarg.0
            // IL_0477: ldfld class Netcode.NetRef`1<class StardewValley.Object> StardewValley.Object::heldObject
            // IL_047c: callvirt instance !0 class Netcode.NetFieldBase`2<class StardewValley.Object, class Netcode.NetRef`1<class StardewValley.Object>>::get_Value()
            // IL_0488: brfalse IL_075a

            matcher
                .MatchEndBackwards([
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(SObject), nameof(SObject.heldObject))),
                    new(OpCodes.Callvirt),
                    new(OpCodes.Brfalse),
                ])
                .ThrowIfNotMatch("if (this.heldObject.Value == null)");

            matcher
                .Advance(1)
                .InsertAndAdvance([
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_1),
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(Patches), nameof(Furniture_checkHeldItemAndDraw))
                    ),
                    new(OpCodes.Brtrue, AfterHeldObject),
                ]);
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Furniture_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
    }

    private static bool Furniture_checkHeldItemAndDraw(Furniture furniture, SpriteBatch spriteBatch)
    {
        if (
            furniture.heldObject.Value is not ColoredObject
            && furniture.heldObject.Value is not Furniture
            && TryGetMBData(furniture.heldObject.Value.ItemId, out MobyDickData? data, out AquariumFishData? aqf)
        )
        {
            //spriteBatch.Draw(heldItemData.GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(base.boundingBox.Center.X - 32, base.boundingBox.Center.Y - (this.drawHeldObjectLow.Value ? 32 : 85))), heldItemData.GetSourceRect(), Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, (float)(base.boundingBox.Bottom + 1) / 10000f);
            Rectangle sourceRect = data.GetAquariumSourceRect();
            Vector2 origin = new(sourceRect.Width / 2, sourceRect.Height / 2);
            spriteBatch.Draw(
                aqf.GetTexture(),
                Game1.GlobalToLocal(
                    Game1.viewport,
                    new Vector2(
                        furniture.boundingBox.Center.X,
                        furniture.boundingBox.Center.Y - (furniture.drawHeldObjectLow.Value ? 0 : 53)
                    )
                ),
                sourceRect,
                Color.White,
                0f,
                origin,
                4f,
                SpriteEffects.None,
                (furniture.boundingBox.Bottom + 1) / 10000f
            );
            return true;
        }
        return false;
    }

    private static bool JumpingFish_Draw_Postfix(
        SpriteBatch b,
        SObject ____fishObject,
        float ___angle,
        bool ____flipped,
        Vector2 ___position,
        float ____age,
        float ___jumpTime,
        float ___jumpHeight
    )
    {
        ItemMetadata itemMetadata = ItemRegistry.GetMetadata(____fishObject.QualifiedItemId);
        if (itemMetadata.TypeIdentifier != "(O)")
            return true;
        if (!TryGetMBData(itemMetadata.LocalItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return true;

        float angle = ___angle;
        SpriteEffects effects = SpriteEffects.None;
        if (____flipped)
        {
            effects = SpriteEffects.FlipHorizontally;
            angle *= -1f;
        }
        float scaleMod = data.DrawScaleInTank;
        Rectangle sourceRect = data.GetAquariumSourceRect();
        float originW = MathF.Min(sourceRect.Width, sourceRect.Height) / 2;
        Vector2 origin = new(sourceRect.Width - originW, originW);
        Vector2 globalPosition =
            ___position + new Vector2(0f, (float)Math.Sin((double)(____age / ___jumpTime) * Math.PI) * -___jumpHeight);
        b.Draw(
            aqf.GetTexture(),
            Game1.GlobalToLocal(Game1.viewport, globalPosition),
            sourceRect,
            Color.White,
            angle,
            origin,
            scaleMod,
            effects,
            ___position.Y / 10000f + 1E-06f
        );
        b.Draw(
            Game1.shadowTexture,
            Game1.GlobalToLocal(Game1.viewport, ___position),
            Game1.shadowTexture.Bounds,
            Color.White * 0.5f,
            0f,
            new Vector2(Game1.shadowTexture.Bounds.Width / 2, Game1.shadowTexture.Bounds.Height / 2),
            2f,
            effects,
            ___position.Y / 10000f + 1E-06f
        );
        return false;
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
        Rectangle sourceRect = data.GetAquariumSourceRect();
        Vector2 drawPos = new(objectPosition.X + 32, objectPosition.Y + 32);
        Vector2 origin = new(sourceRect.Width / 2, sourceRect.Height / 2);
        origin.Y += data.HeldItemOriginOffset.Y;
        SpriteEffects flip;
        if (f.facingDirection.Value == 3)
        {
            flip = SpriteEffects.FlipHorizontally;
            origin.X -= data.HeldItemOriginOffset.X;
        }
        else
        {
            flip = SpriteEffects.None;
            origin.X += data.HeldItemOriginOffset.X;
        }
        spriteBatch.Draw(
            aqf.GetTexture(),
            drawPos,
            sourceRect,
            Color.White,
            0f,
            origin,
            4f,
            flip,
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

    private static void FishingRod_Draw_ReplaceVars(FishingRod rod, ref Texture2D texture, ref Rectangle sourceRect)
    {
        if (rod.whichFish.TypeIdentifier != "(O)")
            return;
        if (!TryGetMBData(rod.whichFish.LocalItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return;
        texture = Game1.content.Load<Texture2D>(aqf.TextureName);
        sourceRect = data.GetAquariumSourceRect();
        return;
    }

    private static Vector2 FishingRod_Draw_ReplaceOrigin(Vector2 existing, ref Rectangle sourceRect)
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
                ])
                .ThrowIfNotMatch("ParsedItemData parsedOrErrorData ... parsedOrErrorData.GetSourceRect();");
            CodeInstruction ldlocaTexture = StlocToLdloca(matcher.InstructionAt(-7));
            CodeInstruction ldlocaRect = StlocToLdloca(matcher.InstructionAt(-1));

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
                    new(OpCodes.Ldarg_0),
                    ldlocaTexture,
                    ldlocaRect,
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(FishingRod_Draw_ReplaceVars))),
                ]);
            // IL_15d6: ldc.r4 8
            // IL_15db: ldc.r4 8
            // IL_15e0: newobj instance void [MonoGame.Framework]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
            CodeInstruction replaceOrigin = new(
                OpCodes.Call,
                AccessTools.DeclaredMethod(typeof(Patches), nameof(FishingRod_Draw_ReplaceOrigin))
            );
            for (int i = 0; i < 2; i++)
            {
                matcher
                    .MatchEndForward([new(OpCodes.Ldc_R4, 8f), new(OpCodes.Ldc_R4, 8f), new(OpCodes.Newobj)])
                    .Advance(1)
                    .InsertAndAdvance([ldlocaRect, replaceOrigin]);
            }

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in TankFish_Draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void GameLocation_UpdateWhenCurrentLocation_Postfix(GameLocation __instance)
    {
        if (string.IsNullOrEmpty(__instance.fishFrenzyFish.Value))
        {
            return;
        }

        if (
            ItemRegistry.GetData(__instance.fishFrenzyFish.Value) is not ParsedItemData parsedOrErrorData
            || !TryGetMBData(parsedOrErrorData.ItemId, out MobyDickData? data, out AquariumFishData? aqf)
        )
            return;

        Texture2D tankFishTx = aqf.GetTexture();

        foreach (TemporaryAnimatedSprite anim in __instance.TemporarySprites.Reverse())
        {
            if (anim.id >= 982648)
            {
                ModifyFishTAS(data, aqf, tankFishTx, parsedOrErrorData, anim);
            }
            else
            {
                break;
            }
        }
    }

    private static void Farmer_showEatingItem_Prefix(Farmer who, ref int __state)
    {
        __state = -1;
        if (who.tempFoodItemTextureName.Value != null || who.itemToEat == null)
            return;
        __state = who.currentLocation.temporarySprites.Count;
    }

    private static void Farmer_showEatingItem_Postfix(Farmer who, ref int __state)
    {
        if (__state == -1)
            return;
        if (!TryGetMBData(who.itemToEat.ItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return;

        Texture2D tankFishTx = aqf.GetTexture();
        ParsedItemData parsedOrErrorData = ItemRegistry.GetData(who.itemToEat.QualifiedItemId);
        for (int i = __state; i < who.currentLocation.temporarySprites.Count; i++)
        {
            TemporaryAnimatedSprite anim = who.currentLocation.temporarySprites[i];
            ModifyFishTAS(data, aqf, tankFishTx, parsedOrErrorData, anim);
        }
    }

    private static void FishingRod_doPullFishFromWater_Postfix(FishingRod __instance, ItemMetadata ___whichFish)
    {
        if (___whichFish.TypeIdentifier != "(O)")
            return;
        if (!TryGetMBData(___whichFish.LocalItemId, out MobyDickData? data, out AquariumFishData? aqf))
            return;

        Texture2D tankFishTx = aqf.GetTexture();
        ParsedItemData parsedOrErrorData = ___whichFish.GetParsedOrErrorData();
        foreach (TemporaryAnimatedSprite anim in __instance.animations)
        {
            ModifyFishTAS(data, aqf, tankFishTx, parsedOrErrorData, anim);
        }
    }

    private static void TankFish_Update_Postfix(TankFish __instance, FishTankFurniture ____tank, GameTime time)
    {
        if (GetTankFishDrawOverride(__instance) is not TankFishDrawOverride drawOverride)
        {
            return;
        }
        if (____tank.GetTankBounds().Width <= (drawOverride.Data.SpriteSize.X * __instance.GetScale() * 4f))
        {
            if (__instance.fishType == TankFish.FishType.Float)
                __instance.velocity.X = 0;
            __instance.facingLeft = false;
        }
        drawOverride.Update(time);
    }

    private static void VisibleFish_Update_Postfix(TankFish __instance, FishTankFurniture ____tank, GameTime time)
    {
        if (GetTankFishDrawOverride(__instance) is not TankFishDrawOverride drawOverride)
        {
            return;
        }
        drawOverride.Update(time);
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
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(TankFish_DrawOverride))),
                    new(OpCodes.Brtrue, label),
                    new(OpCodes.Ldc_I4_0),
                ]);

            CodeMatch[] drawMatch =
            [
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
            ];
            CodeMatch[] ldarg1Match = [new(OpCodes.Ldarg_1)];
            for (int i = 0; i < 3; i++)
            {
                matcher.MatchStartBackwards(drawMatch);
                if (matcher.IsInvalid)
                {
                    break;
                }
                matcher.Advance(1).CreateLabel(out Label drawLbl);
                matcher
                    .MatchStartBackwards(ldarg1Match)
                    .Insert([
                        new(OpCodes.Ldarg_0),
                        new(OpCodes.Ldarg_0),
                        new(OpCodes.Ldfld, AccessTools.Field(typeof(TankFish), "_texture")),
                        new(OpCodes.Ldarg_1),
                        new(OpCodes.Ldarg_2),
                        new(OpCodes.Ldarg_3),
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(TankFish_DrawOverride))),
                        new(OpCodes.Brtrue, drawLbl),
                    ]);
            }

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
        ClearTankFishDrawOverrides();
    }

    internal static void ClearTankFishDrawOverrides()
    {
        TankFishDrawOverrides.Clear();
    }

    internal static Type? visibleFishCustomFish;
    internal static FieldInfo? showFishInWater_CustomFish_alpha = null;

    private static bool TankFish_DrawOverride(
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
