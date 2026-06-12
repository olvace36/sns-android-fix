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
    private static FieldInfo? _xField;

    public static void Apply(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        Monitor = monitor;
        _newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");

        if (_newSkillsPageType != null)
        {
            _xField = _newSkillsPageType.GetField("xPositionOnScreen",
                BindingFlags.Public | BindingFlags.Instance)
                ?? typeof(IClickableMenu).GetField("xPositionOnScreen",
                BindingFlags.Public | BindingFlags.Instance);

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
        var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });

        // วิธีที่ 1: origX จาก _xField
        int origX1 = (int?)_xField?.GetValue(__instance) ?? 0;
        // วิธีที่ 2: origX จาก GameMenu
        int origX2 = Game1.activeClickableMenu is GameMenu gm ? gm.xPositionOnScreen : 0;
        // วิธีที่ 3: hardcode 90
        int origX3 = 90;

        Monitor?.Log($"__instance type={__instance?.GetType().FullName}", LogLevel.Info);
        Monitor?.Log($"origX1(xField)={origX1}, origX2(GameMenu)={origX2}, origX3(hardcode)={origX3}", LogLevel.Info);

        _isDrawingCustom = true;
        try
        {
            // draw วิธีที่ 1
            scrollOffsetField?.SetValue(__instance, 5);
            _xField?.SetValue(__instance, origX1 + 676);
            Monitor?.Log($"Method1: Drawing at x={origX1 + 676}", LogLevel.Info);
            drawMethod?.Invoke(__instance, new object[] { b });
            _xField?.SetValue(__instance, origX1);
            scrollOffsetField?.SetValue(__instance, 0);

            // draw วิธีที่ 2
            scrollOffsetField?.SetValue(__instance, 5);
            _xField?.SetValue(__instance, origX2 + 756);
            Monitor?.Log($"Method2: Drawing at x={origX2 + 756}", LogLevel.Info);
            drawMethod?.Invoke(__instance, new object[] { b });
            _xField?.SetValue(__instance, origX2);
            scrollOffsetField?.SetValue(__instance, 0);

            // draw วิธีที่ 3
            scrollOffsetField?.SetValue(__instance, 5);
            _xField?.SetValue(__instance, origX3 + 836);
            Monitor?.Log($"Method3: Drawing at x={origX3 + 836}", LogLevel.Info);
            drawMethod?.Invoke(__instance, new object[] { b });
            _xField?.SetValue(__instance, origX3);
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
