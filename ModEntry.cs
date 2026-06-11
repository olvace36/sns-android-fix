using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        RevalidateHealthPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
    }
}
