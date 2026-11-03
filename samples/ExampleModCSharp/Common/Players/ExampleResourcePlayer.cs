using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleResourcePlayer : ModPlayer
{
    public int exampleResourceCurrent;
    public const int DefaultExampleResourceMax = 100;
    public int exampleResourceMax;
    public int exampleResourceMax2;
    public float exampleResourceRegenRate;
    internal int exampleResourceRegenTimer = 0;
    public bool exampleResourceMagnet = false;
    public static readonly int exampleResourceMagnetGrabRange = 300;
    public static readonly Color HealExampleResourceColor = new(187, 91, 201);

    public override void Initialize()
    {
        exampleResourceMax = DefaultExampleResourceMax;
    }

    public override void ResetEffects()
    {
        ResetVariables();
    }

    public override void UpdateDead()
    {
        ResetVariables();
    }

    private void ResetVariables()
    {
        exampleResourceRegenRate = 1f;
        exampleResourceMax2 = exampleResourceMax;
        exampleResourceMagnet = false;
    }

    public override void PostUpdateMiscEffects()
    {
        UpdateResource();
    }

    public override void PostUpdate()
    {
        CapResourceGodMode();
    }

    private void UpdateResource()
    {
        exampleResourceRegenTimer++;

        if (exampleResourceRegenTimer > 60 / exampleResourceRegenRate)
        {
            exampleResourceCurrent += 1;
            exampleResourceRegenTimer = 0;
        }

        exampleResourceCurrent = Utils.Clamp(exampleResourceCurrent, 0, exampleResourceMax2);
    }

    private void CapResourceGodMode()
    {
        if (Main.myPlayer == Player.whoAmI && Player.creativeGodMode)
        {
            exampleResourceCurrent = exampleResourceMax2;
        }
    }

    public void HealExampleResource(int healAmount)
    {
        exampleResourceCurrent = Math.Clamp(exampleResourceCurrent + healAmount, 0, exampleResourceMax2);

        if (Main.myPlayer == Player.whoAmI)
        {
            HealExampleResourceEffect(healAmount);
        }
    }

    public void HealExampleResourceEffect(int healAmount)
    {
        CombatText.NewText(Player.getRect(), HealExampleResourceColor, healAmount);

        if ((Main.netMode & NetmodeID.MultiplayerClient) != 0 && Player.whoAmI == Main.myPlayer)
        {
            SendExampleResourceEffectMessage(Player.whoAmI, healAmount);
        }
    }

    public static void HandleExampleResourceEffectMessage(BinaryReader reader, int whoAmI)
    {
        int player = reader.ReadByte();
        if ((Main.netMode & NetmodeID.Server) != 0)
        {
            player = whoAmI;
        }

        int healAmount = reader.ReadInt32();
        if (player != Main.myPlayer)
        {
            Main.player[player].GetModPlayer<ExampleResourcePlayer>().HealExampleResourceEffect(healAmount);
        }

        if ((Main.netMode & NetmodeID.Server) != 0)
        {
            SendExampleResourceEffectMessage(player, healAmount);
        }
    }

    public static void SendExampleResourceEffectMessage(int whoAmI, int healAmount)
    {
        ModPacket packet = ModContent.GetInstance<ExampleMod>().GetPacket();
        packet.Write((byte)ExampleMod.MessageType.ExampleResourceEffect);
        packet.Write((byte)whoAmI);
        packet.Write(healAmount);
        packet.Send(ignoreClient: whoAmI);
    }
}
