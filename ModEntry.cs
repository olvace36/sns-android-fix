using HarmonyLib;
using StardewModdingAPI;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
    }
}
