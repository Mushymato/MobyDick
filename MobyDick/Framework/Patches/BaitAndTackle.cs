using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Tools;

namespace MobyDick.Framework;

internal static partial class Patches
{
    private const string Default_BaitContextTag = "mobydick_bait_item";
    private const string Default_TackleContextTag = "mobydick_tackle_item";
    private const string CustomField_BaitContextTag = $"{ModEntry.ModId}/BaitContextTag";
    private const string CustomField_TackleContextTag = $"{ModEntry.ModId}/TackleContextTag";
    private const string GSQ_HAS_BAIT = $"{ModEntry.ModId}_HAS_BAIT";
    private const string GSQ_HAS_TACKLE = $"{ModEntry.ModId}_HAS_TACKLE";

    internal static void Patch_BaitAndTackle(IModHelper helper, Harmony harmony)
    {
        try
        {
            // accept context tag for bait + tackle instead of enforcing a particular category
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.canThisBeAttached)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(FishingRod_canThisBeAttached_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Patch_BaitAndTackle:\n{err}", LogLevel.Error);
            return;
        }

        // add a new query to check for particular bait
        GameStateQuery.Register(GSQ_HAS_BAIT, HAS_BAIT);
        GameStateQuery.Register(GSQ_HAS_TACKLE, HAS_TACKLE);
    }

    private static string? ApplyTargetOrInputItem(GameStateQueryContext context, string? baitId)
    {
        if (baitId == null || baitId.EqualsIgnoreCase("Any"))
        {
            return null;
        }
        if (baitId.EqualsIgnoreCase("Target"))
        {
            return context.TargetItem?.ItemId ?? baitId;
        }
        if (baitId.EqualsIgnoreCase("Input"))
        {
            return context.TargetItem?.ItemId ?? baitId;
        }
        return baitId;
    }

    private static bool HAS_BAIT(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string? baitId, out string error, name: "string baitId")
            || !ArgUtility.TryGetOptional(
                query,
                2,
                out string? baitPreserveId,
                out error,
                name: "string baitPreserveId"
            )
        )
        {
            ModEntry.Log(error);
            return false;
        }
        if (context.Player?.CurrentTool is not FishingRod fishingRod || !fishingRod.isFishing)
        {
            return false;
        }

        baitId = ApplyTargetOrInputItem(context, baitId);
        baitPreserveId = ApplyTargetOrInputItem(context, baitPreserveId);

        SObject baitItem = fishingRod.GetBait();

        if (baitId != null && baitItem.QualifiedItemId != baitId)
            return false;
        if (baitPreserveId != null && baitItem.preservedParentSheetIndex.Value != baitPreserveId)
            return false;

        return true;
    }

    private static bool HAS_TACKLE(string[] query, GameStateQueryContext context)
    {
        if (context.Player?.CurrentTool is not FishingRod fishingRod || !fishingRod.isFishing)
        {
            return false;
        }

        IEnumerable<SObject> tackleItems = fishingRod.GetTackle();
        HashSet<string> tackleItemIds = tackleItems.Select(item => item.QualifiedItemId).ToHashSet();

        for (int i = 1; i < query.Length; i++)
        {
            string? tackleId = ApplyTargetOrInputItem(context, query[i]);
            if (tackleId == null)
            {
                return true;
            }
            if (tackleItemIds.Contains(tackleId))
            {
                return true;
            }
        }

        return true;
    }

    private static void FishingRod_canThisBeAttached_Postfix(
        FishingRod __instance,
        SObject o,
        int slot,
        ref bool __result
    )
    {
        if (__result || o.bigCraftable.Value)
            return;
        if (slot == 0)
        {
            if (!__instance.CanUseBait())
            {
                return;
            }
            if (
                __instance.GetToolData()?.CustomFields?.TryGetValue(CustomField_BaitContextTag, out string? baitTag)
                ?? false
            )
            {
                foreach (string tag in baitTag.Split(','))
                    __result = o.HasContextTag(tag);
            }
            else
            {
                __result = o.HasContextTag(Default_BaitContextTag);
            }
        }
        else
        {
            if (!__instance.CanUseTackle())
            {
                return;
            }
            if (
                __instance.GetToolData()?.CustomFields?.TryGetValue(CustomField_TackleContextTag, out string? tackleTag)
                ?? false
            )
            {
                foreach (string tag in tackleTag.Split(','))
                    __result = o.HasContextTag(tag);
            }
            else
            {
                __result = o.HasContextTag(Default_TackleContextTag);
            }
        }
    }
}
