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

    static void MoveButton(object? btn, int newX)
    {
        if (btn == null) return;
        var boundsField = btn.GetType().GetField("bounds");
        if (boundsField == null) return;
        var b = (Rectangle)boundsField.GetValue(btn);
        b.X = newX;
        boundsField.SetValue(btn, b);
    }

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

            if (skillAreasList != null)
                for (int i = 0; i < skillAreasList.Count; i++)
                    Monitor?.Log($"skillArea[{i}] bounds={skillAreasList[i].bounds}", LogLevel.Info);
            if (skillBarsList != null)
                for (int i = 0; i < skillBarsList.Count; i++)
                    Monitor?.Log($"skillBar[{i}] bounds={skillBarsList[i].bounds}", LogLevel.Info);

            var skillScrollOffset = _newSkillsPageType.GetField("skillScrollOffset", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var maxSkillCountOnScreen = _newSkillsPageType.GetProperty("MaxSkillCountOnScreen", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var allSkillCount = _newSkillsPageType.GetProperty("AllSkillCount", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage) as string[];

            Monitor?.Log($"pos: x={x}, y={y}, w={w}, h={h}", LogLevel.Info);
            Monitor?.Log($"skillScrollOffset={skillScrollOffset}, maxOnScreen={maxSkillCountOnScreen}, allSkillCount={allSkillCount}", LogLevel.Info);
            Monitor?.Log($"skillBars={skillBarsList?.Count}, skillAreas={skillAreasList?.Count}", LogLevel.Info);
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

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        if (_newSkillsPageType == null) return;

        var maxOnScreen = (int?)_newSkillsPageType.GetProperty("MaxSkillCountOnScreen",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? 5;
        var skillScrollOffset = (int?)_newSkillsPageType.GetField("skillScrollOffset",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? 0;

        var skillBarsList = _newSkillsPageType.GetField("skillBars",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillAreasList = _newSkillsPageType.GetField("skillAreas",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillBarIndexes = _newSkillsPageType.GetField("skillBarSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;
        var skillAreaIndexes = _newSkillsPageType.GetField("skillAreaSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;

        if (skillBarsList == null || skillBarIndexes == null) return;

        // draw skillBars ที่ index >= maxOnScreen (custom skills)
        for (int i = 0; i < skillBarsList.Count; i++)
        {
            if (!skillBarIndexes.TryGetValue(i, out int skillIndex)) continue;
            if (skillIndex < maxOnScreen) continue; // vanilla skills ถูก draw แล้ว
            skillBarsList[i].draw(b);
        }

        // draw skillAreas ที่ index >= maxOnScreen (custom skills)
        if (skillAreasList != null && skillAreaIndexes != null)
        {
            for (int i = 0; i < skillAreasList.Count; i++)
            {
                if (!skillAreaIndexes.TryGetValue(i, out int skillIndex)) continue;
                if (skillIndex < maxOnScreen) continue;
                skillAreasList[i].draw(b);
            }
        }

        Monitor?.Log($"DrawPostfix: drew custom skills (maxOnScreen={maxOnScreen})", LogLevel.Info);
    }
}
