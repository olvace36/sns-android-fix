using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Menus;

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

        helper.Events.Display.MenuChanged += (s, e) => {
            if (e.NewMenu is GameMenu gameMenu)
            {
                var pages = typeof(GameMenu).GetField("pages",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(gameMenu) as System.Collections.IList;
                if (pages != null)
                {
                    for (int i = 0; i < pages.Count; i++)
                        Monitor.Log($"Page {i}: {pages[i]?.GetType().FullName}", LogLevel.Info);
                }
            }
        };
    }
}
