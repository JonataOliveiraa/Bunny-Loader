using System.IO;
using ExampleMod.Content.Items.Consumables;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Players;

public class ExampleStatIncreasePlayer : ModPlayer
{
    public int exampleLifeFruits;
    public int exampleManaCrystals;

    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        health = StatModifier.Default;
        health.Base = exampleLifeFruits * ExampleLifeFruit.LifePerFruit;
        mana = StatModifier.Default;
        mana.Base = exampleManaCrystals * ExampleManaCrystal.ManaPerCrystal;
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        ModPacket packet = Mod.GetPacket();
        packet.Write((byte)ExampleMod.MessageType.ExampleStatIncreasePlayerSync);
        packet.Write((byte)Player.whoAmI);
        packet.Write((byte)exampleLifeFruits);
        packet.Write((byte)exampleManaCrystals);
        packet.Send(toWho, fromWho);
    }

    public void ReceivePlayerSync(BinaryReader reader)
    {
        exampleLifeFruits = reader.ReadByte();
        exampleManaCrystals = reader.ReadByte();
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        ExampleStatIncreasePlayer clone = (ExampleStatIncreasePlayer)targetCopy;
        clone.exampleLifeFruits = exampleLifeFruits;
        clone.exampleManaCrystals = exampleManaCrystals;
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        ExampleStatIncreasePlayer clone = (ExampleStatIncreasePlayer)clientPlayer;

        if (exampleLifeFruits != clone.exampleLifeFruits || exampleManaCrystals != clone.exampleManaCrystals)
        {
            SyncPlayer(toWho: -1, fromWho: Main.myPlayer, newPlayer: false);
        }
    }

    public override void SaveData(TagCompound tag)
    {
        tag["exampleLifeFruits"] = exampleLifeFruits;
        tag["exampleManaCrystals"] = exampleManaCrystals;
    }

    public override void LoadData(TagCompound tag)
    {
        exampleLifeFruits = tag.GetInt("exampleLifeFruits");
        exampleManaCrystals = tag.GetInt("exampleManaCrystals");
    }
}
