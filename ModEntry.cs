using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Reflection;
using StardewValley;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        RevalidateHealthPatch.Monitor = Monitor;
        SkillsPagePatch.Monitor = Monitor;
        FancyAlchemyMenuPatch.Monitor = Monitor;
        ShieldSigilMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        ShieldSigilMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);

        helper.Events.GameLoop.SaveLoaded += (s, e) =>
        {
            var getLevel = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.SkillExtensions"),
                "GetCustomSkillLevel",
                new[] { typeof(Farmer), typeof(string) });
            var getExp = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.Skills"),
                "GetExperienceFor",
                new[] { typeof(Farmer), typeof(string) });

            int rogueLevel = (int)(getLevel?.Invoke(null, new object[] { Game1.player, "DestyNova.SwordAndSorcery.Rogue" }) ?? 0);
            int rogueXP = (int)(getExp?.Invoke(null, new object[] { Game1.player, "DestyNova.SwordAndSorcery.Rogue" }) ?? 0);
            int paladinLevel = (int)(getLevel?.Invoke(null, new object[] { Game1.player, "DestyNova.SwordAndSorcery.Paladin" }) ?? 0);
            int paladinXP = (int)(getExp?.Invoke(null, new object[] { Game1.player, "DestyNova.SwordAndSorcery.Paladin" }) ?? 0);

            int expectedBonus = rogueLevel * 3 + paladinLevel * 5;
            int expectedMaxHealth = 100 + expectedBonus;

            Monitor.Log($"SaveLoaded: Rogue level={rogueLevel}, XP={rogueXP}", LogLevel.Info);
            Monitor.Log($"SaveLoaded: Paladin level={paladinLevel}, XP={paladinXP}", LogLevel.Info);
            Monitor.Log($"SaveLoaded: expectedBonus={expectedBonus}, currentMaxHealth={Game1.player.maxHealth}, expectedMaxHealth={expectedMaxHealth}", LogLevel.Info);
            Monitor.Log($"SaveLoaded: bonus applied correctly={Game1.player.maxHealth >= expectedMaxHealth}", LogLevel.Info);
        };
    }
}
