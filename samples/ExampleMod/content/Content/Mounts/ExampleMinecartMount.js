import { ExampleMinecartBuff } from '../Buffs/ExampleMinecartBuff.js';

const { DustID, MountID } = Terraria.ID;
const Mount = Terraria.Mount;
const NewDustPerfect = Terraria.Dust['Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)'];
const SetAsMinecart = Mount['void SetAsMinecart(Mount.MountData newMount, int buff, Asset`1 texture, int verticalOffset, int playerVerticalOffset)'];

// O carrinho de mina do ExampleMod. As faíscas e os sons vêm de carrinhos do
// jogo (o do Meowmere e o de pum): no celular esses métodos são privados, e o
// delegate pronto é o do MountData deles. Os MinecartUpgrade* do tModLoader
// não existem no celular (o kit de melhoria usa os valores do jogo).
export class ExampleMinecartMount extends ModMount {
    SetStaticDefaults() {
        MountID.Sets.Cart[this.Type] = true;

        // O que todo carrinho tem (velocidade, pulo, quadros...).
        SetAsMinecart(this.MountData, ModContent.BuffType(ExampleMinecartBuff), this.MountData.frontTexture, 0, 0);

        const d = this.MountData;
        d.spawnDust = 21;
        const meow = Mount.mounts[MountID.MeowmereMinecart].delegations;
        const fart = Mount.mounts[MountID.FartMinecart].delegations;
        d.delegations.MinecartDust = meow.MinecartDust;
        d.delegations.MinecartLandingSound = fart.MinecartLandingSound;
        d.delegations.MinecartBumperSound = fart.MinecartBumperSound;
    }

    // O brilho do carrinho de diamante.
    UpdateEffects(player) {
        if (!Rand.NextBool(10)) return;
        const directions = player.Directions;
        const at = Vector2.new(player.Center.X + Rand.NextFloat(-1, 1) * 22,
                               player.Center.Y + 10 * directions.Y + Rand.NextFloat(-1, 1) * 10);
        const dust = NewDustPerfect(player.RotatedRelativePoint(at, false, true, 0), DustID.GemDiamond, null, 0, Color.White, 1);
        dust.noGravity = true;
        dust.fadeIn = 0.6;
        dust.scale = 0.4;
        dust.velocity = Vector2.new(dust.velocity.X * 0.25, dust.velocity.Y * 0.25);
        dust.shader = Terraria.Graphics.Shaders.GameShaders.Armor['ArmorShaderData GetSecondaryShader(int id, Player player)'](player.cMinecart, player);
    }
}
