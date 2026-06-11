using HarmonyLib;
using StardewModdingAPI;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony, Monitor);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
    }
}
