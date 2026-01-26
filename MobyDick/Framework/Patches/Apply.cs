using HarmonyLib;
using StardewModdingAPI;

namespace MobyDick.Framework;

internal static partial class Patches
{
    internal static void Patch(IModHelper helper, Harmony harmony)
    {
        Patch_Drawing(helper, harmony);
        Patch_BaitAndTackle(helper, harmony);
    }
}
