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
            if (e.NewMenu is not GameMenu gameMenu) return;

            var newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");
            if (newSkillsPageType == null)
            {
                Monitor.Log("NewSkillsPage type not found!", LogLevel.Error);
                return;
            }

            var pages = typeof(GameMenu).GetField("pages",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(gameMenu) as System.Collections.Generic.List<IClickableMenu>;
            if (pages == null) return;

            int skillsTab = GameMenu.skillsTab;
            if (skillsTab >= pages.Count) return;

            if (pages[skillsTab]?.GetType() == newSkillsPageType) return;

            var constructor = newSkillsPageType.GetConstructor(new[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int)
            });
            if (constructor == null)
            {
                Monitor.Log("NewSkillsPage constructor not found!", LogLevel.Error);
                return;
            }

            var newPage = (IClickableMenu)constructor.Invoke(new object[]
            {
                gameMenu.xPositionOnScreen,
                gameMenu.yPositionOnScreen,
                gameMenu.width,
                gameMenu.height
            });

            // log VisibleSkills
            var visibleSkills = newSkillsPageType.GetProperty("VisibleSkills",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(newPage) as string[];
            Monitor.Log($"VisibleSkills count: {visibleSkills?.Length ?? -1}", LogLevel.Info);
            if (visibleSkills != null)
                foreach (var skill in visibleSkills)
                    Monitor.Log($"  skill: {skill}", LogLevel.Info);

            // log skillBars and skillAreas
            var skillBars = newSkillsPageType.GetField("skillBars",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(newPage) as System.Collections.IList;
            Monitor.Log($"skillBars count: {skillBars?.Count ?? -1}", LogLevel.Info);

            var skillAreas = newSkillsPageType.GetField("skillAreas",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(newPage) as System.Collections.IList;
            Monitor.Log($"skillAreas count: {skillAreas?.Count ?? -1}", LogLevel.Info);

            // log AllSkillCount
            var allSkillCount = newSkillsPageType.GetProperty("AllSkillCount",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(newPage);
            Monitor.Log($"AllSkillCount: {allSkillCount}", LogLevel.Info);

            pages[skillsTab] = newPage;
            Monitor.Log("SkillsPage replaced with NewSkillsPage!", LogLevel.Info);
        };
    }
}
