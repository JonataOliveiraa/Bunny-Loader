using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

/// <summary>
/// Showcases adding a custom bestiary UI element (IBestiaryInfoElement) to a pre-existing Bestiary entry.
/// </summary>
public class ExampleBestiaryGlobalNPC : GlobalNPC
{
    public override void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        // Add our "important" flavor text to every bestiary entry!
        bestiaryEntry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ImportantFlavorText"));
    }
}
