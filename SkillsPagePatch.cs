using System;
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
                ?.GetValue(gameMenu) as System.Collections.Generic.List<IClickableMenu>;
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

            int rightEdge = x + 800;

            var upBtn = _newSkillsPageType.GetField("upButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var downBtn = _newSkillsPageType.GetField("downButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var scrollBtn = _newSkillsPageType.GetField("scrollBar", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);

            MoveButton(upBtn, rightEdge);
            MoveButton(downBtn, rightEdge);
            MoveButton(scrollBtn, rightEdge + 12);

            var scrollBarRunnerField = _newSkillsPageType.GetField("scrollBarRunner", BindingFlags.NonPublic | BindingFlags.Instance);
            if (scrollBarRunnerField != null)
            {
                var runner = (Rectangle)scrollBarRunnerField.GetValue(newPage);
                runner.X = rightEdge + 12;
                scrollBarRunnerField.SetValue(newPage, runner);
            }

            var skillScrollOffset = _newSkillsPageType.GetField("skillScrollOffset", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var maxSkillCountOnScreen = _newSkillsPageType.GetProperty("MaxSkillCountOnScreen", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var allSkillCount = _newSkillsPageType.GetProperty("AllSkillCount", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage);
            var skillBars = _newSkillsPageType.GetField("skillBars", BindingFlags.Public | BindingFlags.Instance)?.GetValue(newPage) as System.Collections.IList;
            var skillAreas = _newSkillsPageType.GetField("skillAreas", BindingFlags.Public | BindingFlags.Instance)?.GetValue(newPage) as System.Collections.IList;
            var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(newPage) as string[];
            var upBounds = upBtn?.GetType().GetField("bounds")?.GetValue(upBtn);
            var downBounds = downBtn?.GetType().GetField("bounds")?.GetValue(downBtn);

            Monitor?.Log($"pos: x={x}, y={y}, w={w}, h={h}, rightEdge={rightEdge}", LogLevel.Info);
            Monitor?.Log($"skillScrollOffset={skillScrollOffset}, maxOnScreen={maxSkillCountOnScreen}, allSkillCount={allSkillCount}", LogLevel.Info);
            Monitor?.Log($"skillBars={skillBars?.Count}, skillAreas={skillAreas?.Count}", LogLevel.Info);
            Monitor?.Log($"upButton: {upBounds}, downButton: {downBounds}", LogLevel.Info);
            Monitor?.Log($"scrollBarRunner: {scrollBarRunnerField?.GetValue(newPage)}", LogLevel.Info);
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
                ?.GetValue(gameMenu) as System.Collections.Generic.List<IClickableMenu>;
            if (pages == null || gameMenu.currentTab != 1) return;

            var page = pages[1];
            if (page?.GetType() != _newSkillsPageType) return;

            var showsAll = _newSkillsPageType.GetProperty("ShowsAllSkillsAtOnce",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(page);
            if (showsAll is true) return;

            var upBtn = _newSkillsPageType.GetField("upButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(page);
            var downBtn = _newSkillsPageType.GetField("downButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(page);

            // log texture info
            var texField = upBtn?.GetType().GetField("texture", BindingFlags.Public | BindingFlags.Instance);
            var srcField = upBtn?.GetType().GetField("sourceRect", BindingFlags.Public | BindingFlags.Instance);
            var tex = texField?.GetValue(upBtn);
            var src = srcField?.GetValue(upBtn);
            Monitor?.Log($"upBtn texture={tex?.GetType().Name}, sourceRect={src}", LogLevel.Info);

            var upBounds = (Rectangle?)upBtn?.GetType().GetField("bounds")?.GetValue(upBtn);
            var downBounds = (Rectangle?)downBtn?.GetType().GetField("bounds")?.GetValue(downBtn);

            if (tex is Texture2D texture2D && src is Rectangle srcRect && upBounds.HasValue)
            {
                e.SpriteBatch.Draw(texture2D, upBounds.Value, srcRect, Color.White);
            }
            if (tex is Texture2D texture2D2 && src is Rectangle srcRect2 && downBounds.HasValue)
            {
                var downTexField = downBtn?.GetType().GetField("texture", BindingFlags.Public | BindingFlags.Instance);
                var downSrcField = downBtn?.GetType().GetField("sourceRect", BindingFlags.Public | BindingFlags.Instance);
                if (downTexField?.GetValue(downBtn) is Texture2D downTex &&
                    downSrcField?.GetValue(downBtn) is Rectangle downSrc)
                {
                    e.SpriteBatch.Draw(downTex, downBounds.Value, downSrc, Color.White);
                }
            }

            Monitor?.Log("RenderedActiveMenu: drew buttons", LogLevel.Info);
        };
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        Monitor?.Log("DrawPostfix called!", LogLevel.Info);
    }
}
