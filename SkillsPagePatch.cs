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
    private static bool _isDrawingCustom = false;

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
                    prefix: new HarmonyMethod(typeof(SkillsPagePatch)
                        .GetMethod(nameof(DrawPrefix))),
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

            // patch drawDialogueBox ให้ skip ตอน _isDrawingCustom
            var dialogMethod = typeof(IClickableMenu).GetMethod("drawDialogueBox",
                BindingFlags.Public | BindingFlags.Static);
            if (dialogMethod != null)
            {
                harmony.Patch(dialogMethod,
                    prefix: new HarmonyMethod(typeof(SkillsPagePatch)
                        .GetMethod(nameof(DrawDialogueBoxPrefix))));
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

    public static bool DrawDialogueBoxPrefix()
    {
        if (_isDrawingCustom) return false;
        return true;
    }

    public static bool DrawPrefix(object __instance, SpriteBatch b)
    {
        if (_newSkillsPageType == null) return true;
        if (_isDrawingCustom) return true;

        var allSkillCount = (int?)_newSkillsPageType.GetProperty("AllSkillCount",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? 0;

        if (allSkillCount <= 5) return true;

        var scrollOffsetField = _newSkillsPageType.GetField("skillScrollOffset",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var xField = typeof(IClickableMenu).GetField("xPositionOnScreen",
            BindingFlags.Public | BindingFlags.Instance);

        int origX = (int?)xField?.GetValue(__instance) ?? 0;
        Monitor?.Log($"DrawPrefix origX={origX}", LogLevel.Info);

        _isDrawingCustom = true;
        try
        {
            var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });

            // ขยับ x ไปขวา แล้ว draw custom skills
            scrollOffsetField?.SetValue(__instance, 5);
            xField?.SetValue(__instance, origX + 676);
            Monitor?.Log($"Drawing custom skills at x={origX + 676}", LogLevel.Info);
            drawMethod?.Invoke(__instance, new object[] { b });

            // restore
            xField?.SetValue(__instance, origX);
            scrollOffsetField?.SetValue(__instance, 0);
        }
        finally
        {
            _isDrawingCustom = false;
        }

        return true;
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        if (_isDrawingCustom) return;
        Monitor?.Log("DrawPostfix called", LogLevel.Info);
    }
}
