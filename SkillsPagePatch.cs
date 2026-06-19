using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private static Dictionary<int, int> _barOriginalX = new();
    public static int LastHoverX = 0;
    public static int LastHoverY = 0;

    // cache profession icons
    private static Dictionary<string, Texture2D?> _professionIconCache = new();

    public static void Apply(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        Monitor = monitor;
        _newSkillsPageType = AccessTools.TypeByName("SpaceCore.Interface.NewSkillsPage");

        var skillsType = AccessTools.TypeByName("SpaceCore.Skills");
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

            var drawMethod = _newSkillsPageType.GetMethod("draw", new[] { typeof(SpriteBatch) });
            if (drawMethod != null)
                harmony.Patch(drawMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch).GetMethod(nameof(DrawPostfix))));

            var hoverMethod = _newSkillsPageType.GetMethod("performHoverAction",
                BindingFlags.Public | BindingFlags.Instance);
            if (hoverMethod != null)
                harmony.Patch(hoverMethod,
                    postfix: new HarmonyMethod(typeof(SkillsPagePatch).GetMethod(nameof(HoverPostfix))));
        }

        helper.Events.Display.MenuChanged += (s, e) =>
        {
            if (e.NewMenu is not GameMenu gameMenu) return;
            if (_newSkillsPageType == null) return;

            _barOriginalX.Clear();
            _professionIconCache.Clear(); // เพิ่มบรรทัดนี้

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
            var newPage = (IClickableMenu)constructor.Invoke(new object[]
            {
                oldPage.xPositionOnScreen, oldPage.yPositionOnScreen,
                oldPage.width, oldPage.height
            });
            pages[skillsTab] = newPage;
        };
    }

    static (int num, int num2) CalcPositions(object __instance)
    {
        int pageX = (int?)_xField?.GetValue(__instance) ?? 0;
        int pageY = (int?)_yField?.GetValue(__instance) ?? 0;

        if (pageX == 0 && Game1.activeClickableMenu is GameMenu gm)
            pageX = gm.xPositionOnScreen;
        if (pageY == 0 && Game1.activeClickableMenu is GameMenu gm2)
            pageY = gm2.yPositionOnScreen;

        int num = pageX + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 256 - 8 + 800;
        int num2 = pageY + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth - 8;
        return (num, num2);
    }

    static void UpdateSkillAreaBounds(object __instance, int num, int num2)
    {
        if (_newSkillsPageType == null) return;

        var skillAreasList = _newSkillsPageType.GetField("skillAreas",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillAreaIndexes = _newSkillsPageType.GetField("skillAreaSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;

        if (skillAreasList == null || skillAreaIndexes == null) return;

        foreach (var area in skillAreasList)
        {
            if (!skillAreaIndexes.TryGetValue(area.myID, out int skillIndex)) continue;
            if (skillIndex < 5) continue;

            int r = skillIndex - 5;
            var bounds = area.bounds;
            bounds.X = num - 128 - 48;
            bounds.Y = num2 + r * 56;
            bounds.Width = 148;
            bounds.Height = 36;
            area.bounds = bounds;
        }
    }

    static void UpdateSkillBarBounds(object __instance, int num, int num2)
    {
        if (_newSkillsPageType == null) return;

        var skillBars = _newSkillsPageType.GetField("skillBars",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillBarIndexes = _newSkillsPageType.GetField("skillBarSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;

        if (skillBars == null || skillBarIndexes == null) return;

        foreach (var bar in skillBars)
        {
            if (!skillBarIndexes.TryGetValue(bar.myID, out int skillIndex)) continue;
            if (skillIndex < 5) continue;

            if (!_barOriginalX.ContainsKey(bar.myID))
                _barOriginalX[bar.myID] = bar.bounds.X;

            int col = bar.myID % 100 - 1;
            int row = skillIndex - 5;
            int num4 = col >= 5 ? 24 : 0;

            var bounds = bar.bounds;
            bounds.X = num + num4 + (col * 36) - 4;
            bounds.Y = num2 + row * 56;
            bar.bounds = bounds;
        }
    }

static Texture2D? GetProfessionIcon(string barName)
{
    if (_professionIconCache.TryGetValue(barName, out var cached))
        return cached;

    Monitor?.Log($"GetProfessionIcon: looking for barName={barName}", LogLevel.Info);

    try
    {
        var skillsByName = AccessTools.TypeByName("SpaceCore.Skills")
            ?.GetField("SkillsByName", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as IDictionary;

        if (skillsByName == null)
        {
            Monitor?.Log("GetProfessionIcon: SkillsByName is null", LogLevel.Warn);
            return null;
        }

        Monitor?.Log($"GetProfessionIcon: SkillsByName has {skillsByName.Count} entries", LogLevel.Info);

        foreach (DictionaryEntry kvp in skillsByName)
        {
            var skill = kvp.Value;
            if (skill == null) continue;

            var professions = skill.GetType()
                .GetProperty("Professions", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(skill) as IEnumerable;
            if (professions == null) continue;

            foreach (var p in professions)
            {
                string? id = p.GetType()
                    .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(p) as string;

                Monitor?.Log($"GetProfessionIcon: found id={id}", LogLevel.Info);

                if ("C" + id == barName)
                {
                    var icon = p.GetType()
                        .GetProperty("Icon", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(p) as Texture2D;
                    Monitor?.Log($"GetProfessionIcon: matched! icon={icon != null}", LogLevel.Info);
                    _professionIconCache[barName] = icon;
                    return icon;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Monitor?.Log($"GetProfessionIcon error: {ex.Message}", LogLevel.Warn);
    }

    _professionIconCache[barName] = null;
    return null;
}

    public static void HoverPostfix(object __instance, int x, int y)
    {
        if (_newSkillsPageType == null) return;

        LastHoverX = x;
        LastHoverY = y;

        var (num, num2) = CalcPositions(__instance);
        UpdateSkillAreaBounds(__instance, num, num2);
        UpdateSkillBarBounds(__instance, num, num2);

        int skillScrollOffset = (int?)(_newSkillsPageType
            .GetField("skillScrollOffset", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance)) ?? 0;

        var skillAreasList = _newSkillsPageType.GetField("skillAreas",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillAreaIndexes = _newSkillsPageType.GetField("skillAreaSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;
        var skillBars = _newSkillsPageType.GetField("skillBars",
            BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as List<ClickableTextureComponent>;
        var skillBarIndexes = _newSkillsPageType.GetField("skillBarSkillIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(__instance) as Dictionary<int, int>;
        var hoverTextField = _newSkillsPageType.GetField("hoverText",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var hoverTitleField = _newSkillsPageType.GetField("hoverTitle",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var professionImageField = _newSkillsPageType.GetField("professionImage",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (skillAreasList == null || skillAreaIndexes == null) return;

        foreach (var area in skillAreasList)
        {
            if (!skillAreaIndexes.TryGetValue(area.myID, out int skillIndex)) continue;
            if (skillIndex < 5) continue;
            if (!area.containsPoint(x, y + skillScrollOffset * 56)) continue;
            if (area.hoverText.Length <= 0) continue;

            hoverTextField?.SetValue(__instance, area.hoverText);
            hoverTitleField?.SetValue(__instance, area.name.StartsWith("C")
                ? area.name.Substring(1)
                : area.name);
            return;
        }

        if (skillBars != null && skillBarIndexes != null)
        {
            foreach (var bar in skillBars)
            {
                if (!skillBarIndexes.TryGetValue(bar.myID, out int skillIndex)) continue;
                if (skillIndex < 5) continue;
                if (!bar.containsPoint(x, y + skillScrollOffset * 56)) continue;
                if (bar.hoverText.Length <= 0) continue;

                hoverTextField?.SetValue(__instance, bar.hoverText);
                hoverTitleField?.SetValue(__instance, bar.name.StartsWith("C")
                    ? bar.name.Substring(1)
                    : bar.name);
                professionImageField?.SetValue(__instance, bar.name.StartsWith("C") ? 0 : Convert.ToInt32(bar.name));
                bar.scale = 0f;
                return;
            }
        }
    }

    public static void DrawPostfix(object __instance, SpriteBatch b)
{
    if (_newSkillsPageType == null) return;

    var (num, num2) = CalcPositions(__instance);

    var visibleSkills = _newSkillsPageType.GetProperty("VisibleSkills",
        BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as string[];
    if (visibleSkills == null || visibleSkills.Length == 0) return;

    UpdateSkillAreaBounds(__instance, num, num2);
    UpdateSkillBarBounds(__instance, num, num2);

    var skillBars = _newSkillsPageType.GetField("skillBars",
        BindingFlags.Public | BindingFlags.Instance)
        ?.GetValue(__instance) as List<ClickableTextureComponent>;
    var skillBarIndexes = _newSkillsPageType.GetField("skillBarSkillIndexes",
        BindingFlags.NonPublic | BindingFlags.Instance)
        ?.GetValue(__instance) as Dictionary<int, int>;

    int skillScrollOffset = (int?)(_newSkillsPageType
        .GetField("skillScrollOffset", BindingFlags.NonPublic | BindingFlags.Instance)
        ?.GetValue(__instance)) ?? 0;

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
        int buffAmount = (int?)_getBuffAmount?.Invoke(null, new object[] { Game1.player, name, null }) ?? 0;
        bool hasBuff = buffAmount != 0;

        string skillName = (string?)skillType.GetMethod("GetName")?.Invoke(skill, null) ?? name;
        var skillIcon = skillType.GetProperty("SkillsPageIcon")?.GetValue(skill) as Texture2D;

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

    var hoverText = (string?)_newSkillsPageType.GetField("hoverText",
        BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? "";
    var hoverTitle = (string?)_newSkillsPageType.GetField("hoverTitle",
        BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? "";

    if (hoverText.Length > 0)
        IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont, 0, 0, -1, hoverTitle.Length > 0 ? hoverTitle : null);

    // วาด icon หลัง drawHoverText เพื่อให้อยู่บนสุด
    if (skillBars != null && skillBarIndexes != null)
    {
        foreach (var bar in skillBars)
        {
            if (!skillBarIndexes.TryGetValue(bar.myID, out int skillIndex)) continue;
            if (skillIndex < 5) continue;
            if (!bar.name.StartsWith("C")) continue;
            if (!bar.containsPoint(LastHoverX, LastHoverY + skillScrollOffset * 56)) continue;
            if (bar.hoverText.Length <= 0) continue;

            var icon = GetProfessionIcon(bar.name) ?? Game1.staminaRect;
            b.Draw(icon,
                new Vector2(bar.bounds.X - 8, bar.bounds.Y - 32 + 16),
                new Rectangle(0, 0, 16, 16),
                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            break;
        }
    }
    }
