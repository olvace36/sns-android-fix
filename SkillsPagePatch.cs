using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
            if (e.NewMenu is not GameMenu gameMenu) return;

            var newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");
            if (newSkillsPageType == null) return;

            var pages = typeof(GameMenu).GetField("pages",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(gameMenu) as System.Collections.Generic.List<IClickableMenu>;
            if (pages == null) return;

            int skillsTab = GameMenu.skillsTab;
            if (skillsTab >= pages.Count) return;

            var constructor = newSkillsPageType.GetConstructor(new[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int)
            });
            if (constructor == null) return;

            int x = (Game1.uiViewport.Width - gameMenu.width) / 2;
            int y = (Game1.uiViewport.Height - gameMenu.height) / 2;

            var newPage = (IClickableMenu)constructor.Invoke(new object[]
            {
                x, y, gameMenu.width, gameMenu.height
            });

            var visibleSkills = newSkillsPageType.GetProperty("VisibleSkills",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(newPage) as string[];
            Monitor?.Log($"VisibleSkills: {visibleSkills?.Length ?? -1}, pos: x={x}, y={y}", LogLevel.Info);
            if (visibleSkills != null)
                foreach (var skill in visibleSkills)
                    Monitor?.Log($"  skill: {skill}", LogLevel.Info);

            pages[skillsTab] = newPage;
            Monitor?.Log("SkillsPage replaced!", LogLevel.Info);
        };
    }
}
