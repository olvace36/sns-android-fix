using HarmonyLib;
using StardewModdingAPI;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        RevalidateHealthPatch.Monitor = Monitor;
        SkillsPagePatch.Monitor = Monitor;
        FancyAlchemyMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);
    }
}
