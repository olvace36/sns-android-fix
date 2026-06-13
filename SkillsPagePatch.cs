using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class SkillsPagePatch
{
    public static void Apply(IModHelper helper)
    {
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

            if (pages[skillsTab]?.GetType() == newSkillsPageType) return;

            var constructor = newSkillsPageType.GetConstructor(new[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int)
            });
            if (constructor == null) return;

            var newPage = (IClickableMenu)constructor.Invoke(new object[]
            {
                gameMenu.xPositionOnScreen,
                gameMenu.yPositionOnScreen,
                gameMenu.width,
                gameMenu.height
            });

            pages[skillsTab] = newPage;
        };
    }
}
