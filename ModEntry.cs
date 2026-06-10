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

        helper.Events.Display.MenuChanged += (s, e) => {
            if (e.NewMenu != null)
                Monitor.Log($"Menu opened: {e.NewMenu.GetType().FullName}", LogLevel.Info);
        };
    }
}
