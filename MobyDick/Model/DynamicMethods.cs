using System.Reflection.Emit;
using HarmonyLib;
using StardewValley.Objects;

namespace MobyDick.Model;

internal static class DynamicMethods
{
    internal const string _tank = "_tank";
    internal static Func<TankFish, FishTankFurniture> Get_TankFish__tank = null!;

    internal static void Make()
    {
        Get_TankFish__tank = MakeFieldGetter<TankFish, FishTankFurniture>(nameof(Get_TankFish__tank), _tank);
    }

    private static Func<TArg0, TRet> MakeFieldGetter<TArg0, TRet>(string name, string field)
    {
        DynamicMethod dm = new(name, typeof(TRet), [typeof(TArg0)]);
        ILGenerator gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(TArg0), field));
        gen.Emit(OpCodes.Ret);
        return dm.CreateDelegate<Func<TArg0, TRet>>();
    }
}
