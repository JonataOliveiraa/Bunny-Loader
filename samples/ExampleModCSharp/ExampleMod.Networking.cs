using System.IO;
using ExampleMod.Common.Players;
using ExampleMod.Common.Systems;
using ExampleMod.Content.Items.Consumables;
using ExampleMod.Content.NPCs;
using ExampleMod.Content.CustomModType;
using ExampleMod.Content.Items.Weapons;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod;

partial class ExampleMod
{
    internal enum MessageType : byte
    {
        ExampleStatIncreasePlayerSync,
        ExampleTeleportToStatue,
        ExampleDodge,
        ExampleTownPetUnlockOrExchange,
        ExampleResourceEffect,
        StartVictoryPose,
        CancelVictoryPose,
        SendCustomUseStylePlayerDirection,
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        MessageType msgType = (MessageType)reader.ReadByte();

        switch (msgType)
        {
            case MessageType.ExampleStatIncreasePlayerSync:
                byte playerNumber = reader.ReadByte();
                ExampleStatIncreasePlayer examplePlayer = Main.player[playerNumber].GetModPlayer<ExampleStatIncreasePlayer>();
                examplePlayer.ReceivePlayerSync(reader);

                if ((Main.netMode & NetmodeID.Server) != 0)
                {
                    examplePlayer.SyncPlayer(-1, whoAmI, false);
                }
                break;
            case MessageType.ExampleTeleportToStatue:
                if (Main.npc[reader.ReadByte()].ModNPC is ExamplePerson person && person.NPC.active)
                {
                    person.StatueTeleport();
                }
                break;
            case MessageType.ExampleDodge:
                ExampleDamageModificationPlayer.HandleExampleDodgeMessage(reader, whoAmI);
                break;
            case MessageType.ExampleTownPetUnlockOrExchange:
                ExampleTownPetLicense.ExampleTownPetUnlockOrExchangePet(ref ExampleTownPetSystem.boughtExampleTownPet, ModContent.NPCType<Content.NPCs.TownPets.ExampleTownPet>(), ModContent.GetInstance<ExampleTownPetLicense>().GetLocalizationKey("LicenseExampleTownPetUse"));
                break;
            case MessageType.ExampleResourceEffect:
                ExampleResourcePlayer.HandleExampleResourceEffectMessage(reader, whoAmI);
                break;
            case MessageType.StartVictoryPose:
                VictoryPosePlayer.HandleStartVictoryPoseMessage(reader, whoAmI);
                break;
            case MessageType.CancelVictoryPose:
                VictoryPosePlayer.HandleCancelVictoryPoseMessage(reader, whoAmI);
                break;
            case MessageType.SendCustomUseStylePlayerDirection:
                ExampleCustomUseStylePlayer.ReceiveDirection(reader, whoAmI);
                break;
            default:
                Logger.WarnFormat("ExampleMod: Unknown Message type: {0}", msgType);
                break;
        }
    }
}
