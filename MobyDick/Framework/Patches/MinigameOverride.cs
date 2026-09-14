using HarmonyLib;
using MobyDick.Framework.AltFishing;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace MobyDick.Framework;

internal static partial class Patches
{
    public static void Patch_Minigame(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.startMinigameEndFunction)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FishingRod_startMinigameEndFunction_Postfix))
        );
    }

    private static void FishingRod_startMinigameEndFunction_Postfix()
    {
        if (Game1.activeClickableMenu is not BobberBar bobberBar)
            return;
        Game1.activeClickableMenu = DredgeBar.FromBobberBar(bobberBar);
    }
}
