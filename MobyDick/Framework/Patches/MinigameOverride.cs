using HarmonyLib;
using MobyDick.Framework.AltFishing;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.Menus;
using StardewValley.Tools;

namespace MobyDick.Framework;

internal static partial class Patches
{
    private const string CustomField_AltFishing = "mushymato.MobyDick/AltFishing";

    public static void Patch_Minigame(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.startMinigameEndFunction)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FishingRod_startMinigameEndFunction_Postfix))
        );
    }

    private static void FishingRod_startMinigameEndFunction_Postfix(FishingRod __instance)
    {
        if (Game1.activeClickableMenu is not BobberBar bobberBar)
            return;
        if (
            __instance.GetToolData() is ToolData toolData
            && (toolData.CustomFields?.TryGetValue(CustomField_AltFishing, out string? altFishing) ?? false)
        )
        {
            if (altFishing == "Dredge")
                Game1.activeClickableMenu = DredgeBar.FromBobberBar(bobberBar);
        }
    }
}
