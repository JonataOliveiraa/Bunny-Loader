using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleCustomUseStyleWeapon : ModItem
    {
        public override void SetDefaults() {

            Item.useStyle = checked((byte)ExampleCustomUseStyleGlobalItem.ExampleCustomUseStyle);

            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.DamageType = DamageClass.Melee;
            Item.damage = 20;
            Item.width = 58;
            Item.height = 58;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;
        }

        public override void SetStaticDefaults() {

        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }

    public class ExampleCustomUseStylePlayer : ModPlayer
    {
        public float swingAngle;

        public void SyncDirection(int whoAmI)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)ExampleMod.MessageType.SendCustomUseStylePlayerDirection);
            packet.Write((byte)whoAmI);
            packet.Write(swingAngle);
            packet.Send(ignoreClient: whoAmI);
        }

        public static void ReceiveDirection(BinaryReader reader, int whoAmI)
        {
            int player = reader.ReadByte();
            float angle = reader.ReadSingle();
            if ((Main.netMode & NetmodeID.Server) != 0) player = whoAmI;
            if ((uint)player >= (uint)Main.maxPlayers || !float.IsFinite(angle)) return;
            ExampleCustomUseStylePlayer useStylePlayer = Main.player[player].GetModPlayer<ExampleCustomUseStylePlayer>();
            useStylePlayer.swingAngle = angle;
            if ((Main.netMode & NetmodeID.Server) != 0) useStylePlayer.SyncDirection(player);
        }
    }

    public static class ExampleCustomUseStyleItemSets
    {

        public static int[] HandOffsets = System.Array.Empty<int>();
    }

    public class ExampleCustomUseStyleGlobalItem : GlobalItem
    {
        public static int ExampleCustomUseStyle;

        public override void Load() {

            ExampleCustomUseStyle = ItemLoader.RegisterUseStyle(Mod, "ExampleCustomUseStyle");

            TEDisplayDoll.RegisterUsePose(ExampleCustomUseStyle, DisplayDollPoseID.Use1, 0.1f, null);
            TEDisplayDoll.RegisterUsePose(ExampleCustomUseStyle, DisplayDollPoseID.Use2, 0.3f, null);
            TEDisplayDoll.RegisterUsePose(ExampleCustomUseStyle, DisplayDollPoseID.Use3, 0.5f, null);
            TEDisplayDoll.RegisterUsePose(ExampleCustomUseStyle, DisplayDollPoseID.Use4, 0.7f, null);
            TEDisplayDoll.RegisterUsePose(ExampleCustomUseStyle, DisplayDollPoseID.Use5, 0.9f, null);
        }

        public override bool AppliesToEntity(Item item, bool lateInstantiation) {
            return lateInstantiation && (item.type == ItemID.Cutlass || item.useStyle == ExampleCustomUseStyle);
        }

        public override void SetDefaults(Item item) {

            if (item.type == ItemID.Cutlass) {
                item.useStyle = checked((byte)ExampleCustomUseStyle);
            }
        }

        public override void SetStaticDefaults() {
            ExampleCustomUseStyleItemSets.HandOffsets = ItemID.Sets.Factory.CreateIntSet(0, ItemID.Cutlass, -6, ModContent.ItemType<ExampleCustomUseStyleWeapon>(), 0);
        }

        public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) {

            ExampleCustomUseStylePlayer useStylePlayer = player.GetModPlayer<ExampleCustomUseStylePlayer>();

            float percentDone = 1 - (float)player.itemAnimation / player.itemAnimationMax;

            float swingArcRange = MathHelper.ToRadians(115);

            if (player.ItemAnimationJustStarted && player.whoAmI == Main.myPlayer) {

                useStylePlayer.swingAngle = ((Main.MouseWorld - player.MountedCenter) * new Vector2(1, player.gravDir)).ToRotation();

                if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {
                    useStylePlayer.SyncDirection(Main.myPlayer);
                }
            }

            if (player.isDisplayDollOrInanimate) {
                useStylePlayer.swingAngle = player.direction > 0 ? 0 : MathHelper.Pi;
            }
            else {

                player.direction = Utils.ToDirectionInt(useStylePlayer.swingAngle.ToRotationVector2().X >= 0);
            }

            float start = useStylePlayer.swingAngle + (swingArcRange * .5f * player.direction);
            float end = useStylePlayer.swingAngle - (swingArcRange * .5f * player.direction);

            float currentAngle = MathHelper.Lerp(start, end, percentDone);

            if (player.direction > 0) {
                player.itemRotation = currentAngle + MathHelper.PiOver4;
            }
            else {
                player.itemRotation = currentAngle + (MathHelper.PiOver4 * 3);
            }

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);

            player.itemLocation = player.MountedCenter + currentAngle.ToRotationVector2() * new Vector2(ExampleCustomUseStyleItemSets.HandOffsets[item.type]);

            player.FlipItemLocationAndRotationForGravity();

        }

        public override void UseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox) {

            Vector2 handDirection = (player.compositeFrontArm.rotation + MathHelper.PiOver2).ToRotationVector2() * player.gravDir;

            Rectangle drawHitbox = Item.GetDrawHitbox(item.type, player);
            Vector2 itemSize = Main.dedServ ? new Vector2(32) : new Vector2(drawHitbox.Width, drawHitbox.Height);

            float itemLength = (itemSize * player.GetAdjustedItemScale(item)).Length();

            Vector2 handlePosition = handDirection * new Vector2(ExampleCustomUseStyleItemSets.HandOffsets[item.type]) + player.MountedCenter;
            Vector2 tipPosition = handlePosition + handDirection * itemLength;

            hitbox = Utils.CornerRectangle(handlePosition.ToPoint(), tipPosition.ToPoint());
            hitbox.Inflate(1, 1);
        }

        public override void UseItemFrame(Item item, Player player) {

            player.bodyFrame.Y = player.bodyFrame.Height;
        }
    }
}
