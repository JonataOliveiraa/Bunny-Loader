using ExampleMod.Content.Biomes;
using ExampleMod.Content.Buffs;
using ExampleMod.Content.Items.Armor;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleCostumePlayer : ModPlayer
{
    public bool BlockyAccessoryPrevious;
    public bool BlockyAccessory;
    public bool BlockyHideVanity;
    public bool BlockyForceVanity;
    public bool BlockyPower;

    public bool BlockyVanityEffects => BlockyForceVanity || (BlockyPower && !BlockyHideVanity);

    public override void ResetEffects()
    {
        BlockyAccessoryPrevious = BlockyAccessory;
        BlockyAccessory = BlockyHideVanity = BlockyForceVanity = BlockyPower = false;
    }

    public override void UpdateEquips()
    {
        if (Player.townNPCs >= 1 && BlockyAccessory)
        {
            Player.AddBuff(ModContent.BuffType<Blocky>(), 60);
        }
    }

    public override void FrameEffects()
    {
        if (BlockyVanityEffects)
        {
            var exampleCostume = ModContent.GetInstance<ExampleCostume>();
            Player.head = EquipLoader.GetEquipSlot(Mod, exampleCostume.Name, EquipType.Head);
            Player.body = EquipLoader.GetEquipSlot(Mod, exampleCostume.Name, EquipType.Body);
            Player.legs = EquipLoader.GetEquipSlot(Mod, exampleCostume.Name, EquipType.Legs);

            if (Player.wet)
            {
                Player.head = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Head);
                Player.body = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Body);
                Player.legs = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Legs);
            }
        }
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (BlockyVanityEffects)
        {
            Player.headRotation = Player.velocity.Y * Player.direction * 0.1f;
            Player.headRotation = Utils.Clamp(Player.headRotation, -0.3f, 0.3f);

            if (Player.InModBiome<ExampleSurfaceBiome>())
            {
                Player.headRotation = (float)Main.time * 0.1f * Player.direction;
            }
        }
    }

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (BlockyVanityEffects)
        {
            modifiers.DisableSound();
        }
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (BlockyVanityEffects)
        {
            SoundEngine.PlaySound(SoundID.Frog, Player.position);
        }
    }
}
