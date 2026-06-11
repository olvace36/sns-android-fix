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

            var oldPage = pages[skillsTab];
            int x = oldPage.xPositionOnScreen;
            int y = oldPage.yPositionOnScreen;
            int w = oldPage.width;
            int h = oldPage.height;

            var newPage = (IClickableMenu)constructor.Invoke(new object[] { x, y, w, h });

            // log ทุกอย่าง
            var squareSide = newSkillsPageType.GetField("squareSide",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var scaleFactor = newSkillsPageType.GetField("scaleFactor",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var skillScrollOffset = newSkillsPageType.GetField("skillScrollOffset",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var maxSkillCountOnScreen = newSkillsPageType.GetProperty("MaxSkillCountOnScreen",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var allSkillCount = newSkillsPageType.GetProperty("AllSkillCount",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var skillBars = newSkillsPageType.GetField("skillBars",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(newPage) as System.Collections.IList;
            var skillAreas = newSkillsPageType.GetField("skillAreas",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(newPage) as System.Collections.IList;
            var visibleSkills = newSkillsPageType.GetProperty("VisibleSkills",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage) as string[];
            var upButton = newSkillsPageType.GetField("upButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var downButton = newSkillsPageType.GetField("downButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var upBounds = upButton?.GetType().GetField("bounds")?.GetValue(upButton);
            var downBounds = downButton?.GetType().GetField("bounds")?.GetValue(downButton);
            Monitor?.Log($"upButton bounds: {upBounds}, downButton bounds: {downBounds}", LogLevel.Info);
            Monitor?.Log($"scrollBarRunner: {newSkillsPageType.GetField("scrollBarRunner", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage)}", LogLevel.Info);
            Monitor?.Log($"pos: x={x}, y={y}, w={w}, h={h}", LogLevel.Info);
            Monitor?.Log($"squareSide={squareSide}, scaleFactor={scaleFactor}", LogLevel.Info);
            Monitor?.Log($"skillScrollOffset={skillScrollOffset}, maxOnScreen={maxSkillCountOnScreen}", LogLevel.Info);
            Monitor?.Log($"allSkillCount={allSkillCount}, skillBars={skillBars?.Count}, skillAreas={skillAreas?.Count}", LogLevel.Info);
            Monitor?.Log($"VisibleSkills={visibleSkills?.Length}", LogLevel.Info);
            if (visibleSkills != null)
                foreach (var skill in visibleSkills)
                    Monitor?.Log($"  skill: {skill}", LogLevel.Info);
                foreach (var f in newSkillsPageType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    Monitor?.Log($"field: {f.Name} = {f.GetValue(newPage)}", LogLevel.Info);
            
            pages[skillsTab] = newPage;
            Monitor?.Log("SkillsPage replaced!", LogLevel.Info);
        };
    }
}
