global using SObject = StardewValley.Object;
using System.Diagnostics;
using HarmonyLib;
using MobyDick.Framework;
using MobyDick.Model;
using StardewModdingAPI;
using StardewValley;

namespace MobyDick;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif

    public const string ModId = "mushymato.MobyDick";
    private static IMonitor? mon;

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;

        DynamicMethods.Make();

        Harmony harmony = new(ModId);
        Framework.Patches.Patch(helper, harmony);

        AssetManager.Register(helper);
        FishWatcher.Register(helper);
        GameDelegates.Register();
        LocationalFishTankManager.Register(helper);

#if DEBUG
        helper.ConsoleCommands.Add("md-testsummit", "Test summit", ConsoleTestSummit);
#endif
    }

#if DEBUG
    private void ConsoleTestSummit(string arg1, string[] arg2)
    {
        Game1.player.mailReceived.Remove("Summit_event");
        Game1.MasterPlayer.mailReceived.Add("Farm_Eternal");
        Game1.player.team.farmPerfect.Value = true;
    }
#endif

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
