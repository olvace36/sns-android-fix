using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class SkillsPagePatch
{
    internal static IMonitor? Monitor;

    public static void Apply(IModHelper helper, IMonitor monitor)
    {
        Monitor = monitor;
        helper.Events.Display.MenuChanged += (s, e) =>
        {
            if (e.NewMenu is not GameMenu gameMenu)
            {
                Monitor.Log($"Menu changed but not GameMenu: {e.NewMenu?.GetType().Name}", LogLevel.Info);
                return;
            }

            Monitor.Log("GameMenu opened, trying to replace SkillsPage", LogLevel.Info);

            var newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");
            Monitor.Log($"NewSkillsPage type: {newSkillsPageType?.FullName ?? "null"}", LogLevel.Info);

            var pages = typeof(GameMenu).GetField("pages",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(gameMenu) as System.Collections.Generic.List<IClickableMenu>;
            Monitor.Log($"pages count: {pages?.Count ?? -1}", LogLevel.Info);

            if (pages == null) return;

            int skillsTab = GameMenu.skillsTab;
            if (skillsTab >= pages.Count) return;

            Monitor.Log($"skillsTab={skillsTab}, current page={pages[skillsTab]?.GetType().Name}", LogLevel.Info);

            if (pages[skillsTab]?.GetType() == newSkillsPageType) return;

            var constructor = newSkillsPageType?.GetConstructor(new[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int)
            });
            Monitor.Log($"constructor: {constructor != null}", LogLevel.Info);

            if (constructor == null) return;

            var newPage = (IClickableMenu)constructor.Invoke(new object[]
            {
                gameMenu.xPositionOnScreen,
                gameMenu.yPositionOnScreen,
                gameMenu.width,
                gameMenu.height
            });

            pages[skillsTab] = newPage;
            Monitor.Log("SkillsPage replaced with NewSkillsPage!", LogLevel.Info);
        };
    }
}
