using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class SkillsPagePatch
{
    internal static IMonitor? Monitor;
    private static Type? _newSkillsPageType;

    public static void Apply(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        Monitor = monitor;
        _newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");

        if (_newSkillsPageType != null)
        {
            var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });
            if (drawMethod != null)
            {
                harmony.Patch(drawMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch)
                        .GetMethod(nameof(DrawPostfix))));
            }

            var maxProp = _newSkillsPageType.GetProperty("MaxSkillCountOnScreen",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (maxProp != null)
            {
                harmony.Patch(maxProp.GetGetMethod(true),
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch)
                        .GetMethod(nameof(MaxSkillCountPostfix))));
            }
        }

        helper.Events.Display.MenuChanged += (s, e) =>
        {
            if (e.NewMenu is not GameMenu gameMenu) return;
            if (_newSkillsPageType == null) return;

            var pages = typeof(GameMenu).GetField("pages",
                BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(gameMenu) as List<IClickableMenu>;
            if (pages == null) return;

            int skillsTab = 1;
            if (skillsTab >= pages.Count) return;

            var constructor = _newSkillsPageType.GetConstructor(new[]
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

            var skillAreasList = _newSkillsPageType.GetField("skillAreas", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(newPage) as List<ClickableTextureComponent>;
            var skillBarsList = _newSkillsPageType.GetField("skillBars", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(newPage) as List<ClickableTextureComponent>;

            // ขยับ custom skills ไปฝั่งขวา
            if (skillAreasList != null && skillBarsList != null)
            {
                int vanillaAreaY0 = skillAreasList.Count > 0 ? skillAreasList[0].bounds.Y : 216;

                for (int i = 5; i < skillAreasList.Count; i++)
                {
                    int row = i - 5;
                    var area = skillAreasList[i];
                    var bounds = area.bounds;
                    bounds.X = 900;
                    bounds.Y = vanillaAreaY0 + row * 56;
                    area.bounds = bounds;
                    Monitor?.Log($"Moved skillArea[{i}] to x={bounds.X} y={bounds.Y}", LogLevel.Info);
                }

                for (int i = 5; i < skillBarsList.Count; i++)
                {
                    int row = i - 5;
                    var bar = skillBarsList[i];
                    var bounds = bar.bounds;
                    bounds.Y = vanillaAreaY0 + row * 56;
                    bar.bounds = bounds;
                    Monitor?.Log($"Moved skillBar[{i}] to x={bounds.X} y={bounds.Y}", LogLevel.Info);
                }
            }

            var skillScrollOffset = _newSkillsPageType.GetField("skillScrollOffset", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var maxSkillCountOnScreen = _newSkillsPageType.GetProperty("MaxSkillCountOnScreen", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var allSkillCount = _newSkillsPageType.GetProperty("AllSkillCount", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage) as string[];

            Monitor?.Log($"pos: x={x}, y={y}, w={w}, h={h}", LogLevel.Info);
            Monitor?.Log($"skillScrollOffset={skillScrollOffset}, maxOnScreen={maxSkillCountOnScreen}, allSkillCount={allSkillCount}", LogLevel.Info);
            Monitor?.Log($"VisibleSkills={visibleSkills?.Length}", LogLevel.Info);
            if (visibleSkills != null)
                foreach (var skill in visibleSkills)
                    Monitor?.Log($"  skill: {skill}", LogLevel.Info);

            pages[skillsTab] = newPage;
            Monitor?.Log("SkillsPage replaced!", LogLevel.Info);
        };

        helper.Events.Display.RenderedActiveMenu += (s, e) =>
        {
            if (Game1.activeClickableMenu is not GameMenu gameMenu) return;
            if (_newSkillsPageType == null) return;

            var pages = typeof(GameMenu).GetField("pages", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(gameMenu) as List<IClickableMenu>;
            if (pages == null || gameMenu.currentTab != 1) return;

            var page = pages[1];
            if (page?.GetType() != _newSkillsPageType) return;

            Monitor?.Log("RenderedActiveMenu: active", LogLevel.Info);
        };
    }

    public static void MaxSkillCountPostfix(ref int __result)
    {
        __result = 10;
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        if (_newSkillsPageType == null) return;

        var skillBarsList = _newSkillsPageType.GetField("skillBars",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;

        if (skillBarsList == null) return;

        // log bounds ของ skillBar[0-4] เพื่อหา x สูงสุดของ vanilla
        int maxVanillaX = 0;
        for (int i = 0; i < Math.Min(5, skillBarsList.Count); i++)
        {
            var bar = skillBarsList[i];
            Monitor?.Log($"DrawPostfix skillBar[{i}] bounds={bar.bounds}", LogLevel.Info);
            if (bar.bounds.X + bar.bounds.Width > maxVanillaX)
                maxVanillaX = bar.bounds.X + bar.bounds.Width;
        }
        Monitor?.Log($"DrawPostfix maxVanillaX={maxVanillaX}", LogLevel.Info);

        // log skillBar[5] จาก __instance จริงๆ
        if (skillBarsList.Count > 5)
            Monitor?.Log($"DrawPostfix skillBar[5] bounds={skillBarsList[5].bounds}", LogLevel.Info);
    }
}
