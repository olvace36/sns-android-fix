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

    private static System.Reflection.MethodInfo? _getSkillMethod;
    private static System.Reflection.MethodInfo? _getBuffedLevel;
    private static System.Reflection.MethodInfo? _getBaseLevel;
    private static System.Reflection.MethodInfo? _getBuffAmount;
    private static Type? _skillsType;

    // cache field refs
    private static FieldInfo? _hoverTextField;
    private static FieldInfo? _hoverTitleField;
    private static FieldInfo? _scrollOffsetField;
    private static FieldInfo? _skillBarsField;
    private static FieldInfo? _skillAreasField;
    private static FieldInfo? _skillAreaIndexesField;

    public static void Apply(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        Monitor = monitor;
        _newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");

        var skillsType = AccessTools.TypeByName("SpaceCore.Skills");
        _skillsType = skillsType;
        _getSkillMethod = skillsType?.GetMethod("GetSkill", BindingFlags.Public | BindingFlags.Static);
        _getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), typeof(string) });
        _getBaseLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), typeof(string) });
        _getBuffAmount = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillBuffAmount",
            new[] { typeof(Farmer), typeof(string), typeof(string) });

        if (_newSkillsPageType != null)
        {
            _xField = typeof(IClickableMenu).GetField("xPositionOnScreen", BindingFlags.Public | BindingFlags.Instance);
            _yField = typeof(IClickableMenu).GetField("yPositionOnScreen", BindingFlags.Public | BindingFlags.Instance);

            // cache fields ครั้งเดียว
            _hoverTextField   = _newSkillsPageType.GetField("hoverText",           BindingFlags.NonPublic | BindingFlags.Instance);
            _hoverTitleField  = _newSkillsPageType.GetField("hoverTitle",          BindingFlags.NonPublic | BindingFlags.Instance);
            _scrollOffsetField= _newSkillsPageType.GetField("skillScrollOffset",   BindingFlags.NonPublic | BindingFlags.Instance);
            _skillBarsField   = _newSkillsPageType.GetField("skillBars",           BindingFlags.Public   | BindingFlags.Instance);
            _skillAreasField  = _newSkillsPageType.GetField("skillAreas",          BindingFlags.Public   | BindingFlags.Instance);
            _skillAreaIndexesField = _newSkillsPageType.GetField("skillAreaSkillIndexes", BindingFlags.NonPublic | BindingFlags.Instance);

            var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });
            if (drawMethod != null)
                harmony.Patch(drawMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch).GetMethod(nameof(DrawPostfix))));

            var hoverMethod = _newSkillsPageType.GetMethod("performHoverAction",
                BindingFlags.Public | BindingFlags.Instance);
            if (hoverMethod != null)
                harmony.Patch(hoverMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch).GetMethod(nameof(HoverPostfix))));

            Monitor.Log("SkillsPagePatch applied (draw + performHoverAction)", LogLevel.Info);
        }
        else
        {
            Monitor.Log("NewSkillsPage type NOT FOUND!", LogLevel.Error);
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
            pages[skillsTab] = newPage;
        };
    }

    public static void HoverPostfix(object __instance, int x, int y)
    {
        if (_newSkillsPageType == null) return;

        int scrollOffset = (int?)_scrollOffsetField?.GetValue(__instance) ?? 0;

        // ── 1. SNS profession bars (skillBars ที่ชื่อขึ้นต้น C) ──
        var skillBars = _skillBarsField?.GetValue(__instance) as System.Collections.IEnumerable;
        if (skillBars != null)
        {
            var skillsByName = _skillsType?.GetProperty("SkillsByName",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                as System.Collections.IDictionary;

            foreach (ClickableTextureComponent bar in skillBars)
            {
                if (bar == null || !bar.name.StartsWith("C") || bar.hoverText.Length <= 0) continue;

                bool hit = bar.containsPoint(x, y + scrollOffset * 56);
                Monitor?.Log($"HoverPostfix: SNS bar '{bar.name}' bounds={bar.bounds} containsPoint({x},{y}+scroll{scrollOffset*56})={hit}", LogLevel.Debug);
                if (!hit) continue;

                Monitor?.Log($"HoverPostfix: hit SNS bar '{bar.name}'", LogLevel.Debug);

                // หา profession จาก SkillsByName
                if (skillsByName != null)
                {
                    foreach (var key in skillsByName.Keys)
                    {
                        var skill = skillsByName[key];
                        var professions = skill?.GetType()
                            .GetProperty("Professions", BindingFlags.Public | BindingFlags.Instance)
                            ?.GetValue(skill) as System.Collections.IEnumerable;
                        if (professions == null) continue;

                        foreach (var prof in professions)
                        {
                            if (prof == null) continue;
                            var profId = prof.GetType().GetProperty("Id",
                                BindingFlags.Public | BindingFlags.Instance)?.GetValue(prof)?.ToString();
                            if ("C" + profId != bar.name) continue;

                            var getName = prof.GetType().GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                            var getDesc = prof.GetType().GetMethod("GetDescription", BindingFlags.Public | BindingFlags.Instance);
                            string title = (string?)getName?.Invoke(prof, null) ?? "";
                            string desc  = (string?)getDesc?.Invoke(prof, null) ?? bar.hoverText;

                            Monitor?.Log($"HoverPostfix: profession found title='{title}' desc='{desc}'", LogLevel.Debug);
                            // set field ของ SpaceCore โดยตรง → SpaceCore จะวาดให้เอง
                            _hoverTextField?.SetValue(__instance, desc);
                            _hoverTitleField?.SetValue(__instance, title);
                            return;
                        }
                    }
                }

                // fallback
                Monitor?.Log($"HoverPostfix: fallback for '{bar.name}'", LogLevel.Warn);
                _hoverTextField?.SetValue(__instance, bar.hoverText);
                _hoverTitleField?.SetValue(__instance, bar.name.Substring(1));
                return;
            }
        }

        // ── 2. SNS skillAreas (skill index >= 5) ──
        var skillAreasList   = _skillAreasField?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillAreaIndexes = _skillAreaIndexesField?.GetValue(__instance) as Dictionary<int, int>;
        if (skillAreasList == null || skillAreaIndexes == null) return;

        foreach (var area in skillAreasList)
        {
            if (!skillAreaIndexes.TryGetValue(area.myID, out int skillIndex)) continue;
            if (skillIndex < 5) continue;
            if (!area.containsPoint(x, y)) continue;
            if (area.hoverText.Length <= 0) continue;

            Monitor?.Log($"HoverPostfix: SNS skillArea hit skillIndex={skillIndex} title='{area.name}'", LogLevel.Debug);
            // set field ของ SpaceCore โดยตรง → SpaceCore จะวาดให้เอง
            _hoverTextField?.SetValue(__instance, area.hoverText);
            _hoverTitleField?.SetValue(__instance, area.name.StartsWith("C")
                ? area.name.Substring(1)
                : area.name);
            return;
        }
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
    {
        if (_newSkillsPageType == null) return;

        int pageX = (int?)_xField?.GetValue(__instance) ?? 0;
        int pageY = (int?)_yField?.GetValue(__instance) ?? 0;

        if (pageX == 0 && Game1.activeClickableMenu is GameMenu gm)
            pageX = gm.xPositionOnScreen;
        if (pageY == 0 && Game1.activeClickableMenu is GameMenu gm2)
            pageY = gm2.yPositionOnScreen;

        int num = pageX + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 256 - 8 + 800;
        int num2 = pageY + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth - 8;

        var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as string[];
        if (visibleSkills == null || visibleSkills.Length == 0) return;

        // ย้าย skillArea bounds ฝั่งขวา
        var skillAreasList = _skillAreasField?.GetValue(__instance) as List<ClickableTextureComponent>;
        if (skillAreasList != null)
        {
            for (int i = 5; i < skillAreasList.Count; i++)
            {
                int r = i - 5;
                var area = skillAreasList[i];
                var bounds = area.bounds;
                bounds.X = num - 128 - 48;
                bounds.Y = num2 + r * 56;
                area.bounds = bounds;
            }
        }

        // ย้าย SNS skillBars (profession bars ชื่อขึ้นต้น C) ให้อยู่ฝั่งขวาด้วย
        // เพื่อให้ containsPoint ใน HoverPostfix match กับที่กดจริง
        var skillBarsList = _skillBarsField?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillBarIndexes = _newSkillsPageType?.GetField("skillBarSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance)
            as Dictionary<int, int>;

        if (skillBarsList != null && skillBarIndexes != null)
        {
            // นับ row ของ SNS skills (gameSkillCount = 6 สำหรับ vanilla)
            const int gameSkillCount = 6;
            foreach (var bar in skillBarsList)
            {
                if (bar == null) continue;
                if (!bar.name.StartsWith("C")) continue; // เฉพาะ SNS profession bars
                if (!skillBarIndexes.TryGetValue(bar.myID, out int skillIdx)) continue;

                // row ของ SNS skill นี้ (นับจาก 0 ของ SNS skills)
                int snsRow = skillIdx - gameSkillCount;
                if (snsRow < 0) continue;

                // หา column (level 5 หรือ level 10) จาก X เดิม
                // X เดิม = num2 - 4 + num11 * 36 (num11 = 4 หรือ 9)
                // เราใช้ bounds.Width เดิมและหา offset จาก X ปัจจุบัน
                var bounds = bar.bounds;
                int oldX = bounds.X;

                // คำนวณ X ใหม่ให้อยู่ฝั่งขวาเหมือน skillArea
                // skillArea อยู่ที่ num - 128 - 48
                // profession bar อยู่ที่ level 5 (num11=4) และ 10 (num11=9)
                // X = baseX + num11 * 36 - 4
                // เราต้องรู้ว่า bar นี้อยู่ level ไหน — ดูจาก myID
                // myID = skillIdx + column*100 (column 1=level5, 2=level10)
                int column = bar.myID / 100; // 1 = level5, 2 = level10
                int num11  = column == 1 ? 4 : 9;
                int newX   = num - 4 + num11 * 36;
                int newY   = num2 + snsRow * 56;

                bounds.X = newX;
                bounds.Y = newY;
                bar.bounds = bounds;

                Monitor?.Log($"DrawPostfix: moved SNS bar '{bar.name}' to ({newX},{newY})", LogLevel.Debug);
            }
        }

        int row = 0;
        foreach (var name in visibleSkills)
        {
            var skill = _getSkillMethod?.Invoke(null, new object[] { name });
            if (skill == null) { row++; continue; }

            var skillType = skill.GetType();
            int num4 = 0;
            var expCurve = skillType.GetProperty("ExperienceCurve")?.GetValue(skill) as int[];
            int levels = expCurve?.Length ?? 10;

            int buffedLevel = (int?)_getBuffedLevel?.Invoke(null, new object[] { Game1.player, name }) ?? 0;
            int buffAmount  = (int?)_getBuffAmount?.Invoke(null,  new object[] { Game1.player, name, null }) ?? 0;
            bool hasBuff    = buffAmount != 0;

            string skillName = (string?)skillType.GetMethod("GetName")?.Invoke(skill, null) ?? name;
            var skillIcon    = skillType.GetProperty("SkillsPageIcon")?.GetValue(skill) as Texture2D;

            if (skillName.Length > 0)
                b.DrawString(Game1.smallFont, skillName,
                    new Vector2((float)((double)((float)num - Game1.smallFont.MeasureString(skillName).X) + 4.0 - 64.0),
                    (float)(num2 + 4 + row * 56)), Game1.textColor);

            if (skillIcon != null)
            {
                b.Draw(skillIcon, new Vector2((float)(num - 56), (float)(num2 + row * 56)),
                    null, Color.Black * 0.3f, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.85f);
                b.Draw(skillIcon, new Vector2((float)(num - 52), (float)(num2 - 4 + row * 56)),
                    null, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
            }

            for (int l = 0; l < levels; l++)
            {
                bool filled = buffedLevel > l;

                if ((l + 1) % 5 == 0)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num - 4 + l * 36), (float)(num2 + row * 56)),
                        new Rectangle(145, 338, 14, 9), Color.Black * 0.35f,
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num + l * 36), (float)(num2 - 4 + row * 56)),
                        new Rectangle(filled ? 159 : 145, 338, 14, 9), Color.White * (filled ? 1f : 0.65f),
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                }
                else
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num - 4 + l * 36), (float)(num2 + row * 56)),
                        new Rectangle(129, 338, 8, 9), Color.Black * 0.35f,
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.85f);
                    b.Draw(Game1.mouseCursors,
                        new Vector2((float)(num4 + num + l * 36), (float)(num2 - 4 + row * 56)),
                        new Rectangle(129 + (filled ? 8 : 0), 338, 8, 9), Color.White * (filled ? 1f : 0.65f),
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
                }

                if (l == levels - 1)
                {
                    Color levelColor = hasBuff ? Color.LightGreen : Color.SandyBrown;
                    NumberSprite.draw(buffedLevel, b,
                        new Vector2((float)(num4 + num + (l + 2) * 36 + 12 + ((buffedLevel >= 10) ? 12 : 0)),
                        (float)(num2 + 16 + row * 56)), Color.Black * 0.35f, 1f, 0.85f, 1f, 0, 0);
                    NumberSprite.draw(buffedLevel, b,
                        new Vector2((float)(num4 + num + (l + 2) * 36 + 16 + ((buffedLevel >= 10) ? 12 : 0)),
                        (float)(num2 + 12 + row * 56)), levelColor * (buffedLevel == 0 ? 0.75f : 1f),
                        1f, 0.87f, 1f, 0, 0);
                }

                if ((l + 1) % 5 == 0) num4 += 24;
            }
            row++;
        }

        // ── วาด hoverText ──
        // SpaceCore วาดที่บรรทัด 1275/1286 แต่อาจถูก reset โดย draw loop
        // เราวาดซ้ำที่ท้าย postfix เพื่อให้แน่ใจว่าขึ้น
        string hoverText  = (string?)_hoverTextField?.GetValue(__instance)  ?? "";
        string hoverTitle = (string?)_hoverTitleField?.GetValue(__instance) ?? "";

        if (hoverText.Length > 0)
        {
            Monitor?.Log($"DrawPostfix: drawing hoverText='{hoverText}' title='{hoverTitle}'", LogLevel.Debug);
            IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont, 0, 0, -1,
                hoverTitle.Length > 0 ? hoverTitle : null);
        }
    }
}

