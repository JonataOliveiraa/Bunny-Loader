using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

public class ExampleBestiaryGlobalNPC : GlobalNPC
{
    public override void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ImportantFlavorText"));
    }
}
