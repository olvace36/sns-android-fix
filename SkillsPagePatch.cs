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
    private static FieldInfo? _xField;
    private static FieldInfo? _yField;

    public static void Apply(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        Monitor = monitor;
        _newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");

        if (_newSkillsPageType != null)
        {
            _xField = typeof(IClickableMenu).GetField("xPositionOnScreen", BindingFlags.Public | BindingFlags.Instance);
            _yField = typeof(IClickableMenu).GetField("yPositionOnScreen", BindingFlags.Public | BindingFlags.Instance);

            var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });
            if (drawMethod != null)
            {
                harmony.Patch(drawMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch)
                        .GetMethod(nameof(DrawPostfix))));
            }

            // MaxSkillCountOnScreen คืนค่า 5 ปกติ ไม่ต้อง patch แล้ว
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
        };
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        if (_newSkillsPageType == null) return;

        int pageX = (int?)_xField?.GetValue(__instance) ?? 90;
        int pageY = (int?)_yField?.GetValue(__instance) ?? 72;

        // คำนวณ num เหมือน draw() จริงๆ แต่บวก offset ไปขวา
        int num = pageX + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 256 - 8 + 676;
        int num2 = pageY + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth - 8;

        Monitor?.Log($"DrawPostfix: pageX={pageX}, num={num}, num2={num2}", LogLevel.Info);

        var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as string[];
        if (visibleSkills == null || visibleSkills.Length == 0) return;

        var skillsType = AccessTools.TypeByName("SpaceCore.Skills");
        var getSkillMethod = skillsType?.GetMethod("GetSkill", BindingFlags.Public | BindingFlags.Static);
        var getCustomSkillLevel = AccessTools.Method(typeof(Farmer), "GetCustomSkillLevel");

        int row = 0;
        foreach (var name in visibleSkills)
        {
            var skill = getSkillMethod?.Invoke(null, new object[] { name });
            if (skill == null) { row++; continue; }

            var skillType = skill.GetType();
            int num4 = 0;
            var expCurve = skillType.GetProperty("ExperienceCurve")?.GetValue(skill) as int[];
            int levels = expCurve?.Length ?? 10;
            int playerLevel = (int?)getCustomSkillLevel?.Invoke(Game1.player, new object[] { skill }) ?? 0;
            string skillName = (string?)skillType.GetMethod("GetName")?.Invoke(skill, null) ?? name;
            var skillIcon = skillType.GetProperty("SkillsPageIcon")?.GetValue(skill) as Texture2D;

            Monitor?.Log($"Drawing skill: {skillName}, level={playerLevel}, row={row}", LogLevel.Info);

            // draw skill name
            if (skillName.Length > 0)
            {
                b.DrawString(Game1.smallFont, skillName,
                    new Vector2((float)((double)((float)num - Game1.smallFont.MeasureString(skillName).X) + 4.0 - 64.0),
                    (float)(num2 + 4 + row * 56)), Game1.textColor);
            }

            // draw skill icon
            if (skillIcon != null)
            {
                b.Draw(skillIcon, new Vector2((float)(num - 56), (float)(num2 + row * 56)),
                    null, Color.Black * 0.3f, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.85f);
                b.Draw(skillIcon, new Vector2((float)(num - 52), (float)(num2 - 4 + row * 56)),
                    null, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
            }

            // draw XP bars
            for (int l = 0; l < levels; l++)
            {
                bool filled = playerLevel > l;
                if (!filled && (l + 1) % 5 == 0)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num - 4 + l * 36), (float)(num2 + row * 56)),
                        new Rectangle(145, 338, 14, 9), Color.Black * 0.35f, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num + l * 36), (float)(num2 - 4 + row * 56)),
                        new Rectangle(145, 338, 14, 9), Color.White * 0.65f, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                }
                else if ((l + 1) % 5 != 0)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num - 4 + l * 36), (float)(num2 + row * 56)),
                        new Rectangle(129, 338, 8, 9), Color.Black * 0.35f, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.85f);
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num + l * 36), (float)(num2 - 4 + row * 56)),
                        new Rectangle(129 + (filled ? 8 : 0), 338, 8, 9), Color.White * (filled ? 1f : 0.65f),
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                }
                if (l == levels - 1)
                {
                    NumberSprite.draw(playerLevel, b,
                        new Vector2((float)(num4 + num + (l + 2) * 36 + 12 + ((playerLevel >= 10) ? 12 : 0)),
                        (float)(num2 + 16 + row * 56)), Color.Black * 0.35f, 1f, 0.85f, 1f, 0, 0);
                    NumberSprite.draw(playerLevel, b,
                        new Vector2((float)(num4 + num + (l + 2) * 36 + 16 + ((playerLevel >= 10) ? 12 : 0)),
                        (float)(num2 + 12 + row * 56)), Color.SandyBrown * (playerLevel == 0 ? 0.75f : 1f),
                        1f, 0.87f, 1f, 0, 0);
                }
                if ((l + 1) % 5 == 0) num4 += 24;
            }
            row++;
        }
        Monitor?.Log($"DrawPostfix: drew {visibleSkills.Length} custom skills", LogLevel.Info);
    }
}
