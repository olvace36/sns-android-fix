using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);

        helper.Events.GameLoop.GameLaunched += (s, e) =>
        {
            LevelUpMenuTranspilerFix.Apply(harmony);
        };
    }
}
