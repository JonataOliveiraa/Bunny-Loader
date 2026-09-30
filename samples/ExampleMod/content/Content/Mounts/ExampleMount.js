import { ExampleMountBuff } from '../Buffs/ExampleMountBuff.js';

const { DustID } = Terraria.ID;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const NewDustPerfect = Terraria.Dust['Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)'];

// A poeira Sparkle do tModLoader é uma ModDust, que ainda não existe aqui:
// fica a de diamante do jogo.
const SPARKLE = DustID.GemDiamond;
const OFFSETS = [0, 14, -14];   // os três balões, em x

// O carro com três balões do ExampleMod: anda como o unicórnio. Os balões
// balançam com o vento (UpdateEffects) e são desenhados antes da camada de
// trás (Draw). Uma instância serve a todos os jogadores: o estado dos balões
// de cada um fica no dado da montaria (ModMount.SetSpecificData).
export class ExampleMount extends ModMount {
    balloonTexture = null;

    SetStaticDefaults() {
        const d = this.MountData;
        // Movimento
        d.jumpHeight = 5;           // altura do pulo
        d.acceleration = 0.19;      // aceleração
        d.jumpSpeed = 4;            // velocidade de subida no pulo
        d.blockExtraJumps = false;  // deixa usar pulo duplo (nuvem na garrafa)
        d.constantJump = true;      // pular segurando o botão
        d.heightBoost = 20;         // altura entre a montaria e o chão
        d.fallDamage = 0.5;         // multiplicador do dano de queda
        d.runSpeed = 11;
        d.dashSpeed = 8;
        d.flightTimeMax = 0;

        d.fatigueMax = 0;
        d.buff = ModContent.BuffType(ExampleMountBuff);
        d.spawnDust = SPARKLE;

        // Quadros e deslocamentos do jogador
        d.totalFrames = 4;
        d.playerYOffsets = Array(d.totalFrames).fill(20);
        d.xOffset = 13;
        d.yOffset = -12;
        d.playerHeadOffset = 22;
        d.bodyFrame = 3;
        // Parado
        d.standingFrameCount = 4;
        d.standingFrameDelay = 12;
        d.standingFrameStart = 0;
        // Andando
        d.runningFrameCount = 4;
        d.runningFrameDelay = 12;
        d.runningFrameStart = 0;
        // Voando
        d.flyingFrameCount = 0;
        d.flyingFrameDelay = 0;
        d.flyingFrameStart = 0;
        // No ar
        d.inAirFrameCount = 1;
        d.inAirFrameDelay = 12;
        d.inAirFrameStart = 0;
        // Ocioso
        d.idleFrameCount = 4;
        d.idleFrameDelay = 12;
        d.idleFrameStart = 0;
        d.idleFrameLoop = true;
        // Nadando
        d.swimFrameCount = d.inAirFrameCount;
        d.swimFrameDelay = d.inAirFrameDelay;
        d.swimFrameStart = d.inAirFrameStart;

        const back = d.backTexture.Value;
        d.textureWidth = back.Width + 20;
        d.textureHeight = back.Height;

        this.balloonTexture = ModContent.Request('Items/Armor/SimpleAccessory_Balloon');
    }

    // O vento nos balões e poeira quando corre.
    UpdateEffects(player) {
        const balloons = ModMount.GetSpecificData(player);
        if (!balloons) return;
        let scale = 0.05;
        for (let i = 0; i < balloons.count; i++) {
            if (Math.abs(balloons.rotations[i]) > Math.PI / 2) scale *= -1;
            let rotation = balloons.rotations[i] - player.velocity.X * scale * Rand.NextFloat();
            balloons.rotations[i] = rotation + (0 - rotation) * 0.05;   // AngleLerp(0, 0.05)
        }

        if (Math.abs(player.velocity.X) > 4) {
            const rect = player.getRect();
            NewDust(Vector2.new(rect.X, rect.Y), rect.Width, rect.Height, SPARKLE, 0, 0, 0, Color.White, 1);
        }
    }

    // Os balões de cada jogador, e um anel de poeira no lugar da do jogo.
    SetMount(player, skipDust) {
        ModMount.SetSpecificData(player, { count: 3, rotations: [0, 0, 0] });
        for (let i = 0; i < 16; i++) {
            const angle = i * Math.PI * 2 / 16;
            const at = Vector2.new(player.Center.X + 80 * Math.cos(angle), player.Center.Y + 80 * Math.sin(angle));
            NewDustPerfect(at, this.MountData.spawnDust, null, 0, Color.White, 1);
        }
        skipDust.value = true;
    }

    // Os balões antes da textura de trás; true: o jogo desenha o carro.
    Draw(playerDrawData, drawType, drawPlayer, texture, glowTexture, drawPosition, frame, drawColor,
         glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow) {
        if (drawType !== 0) return true;
        const balloons = ModMount.GetSpecificData(drawPlayer);
        if (!balloons || !this.balloonTexture) return true;

        const balloon = this.balloonTexture.Value;
        const timer = Math.floor((Date.now() % 800) / 200);
        const position = drawPosition.value;
        for (let i = 0; i < balloons.count; i++) {
            const at = Vector2.new(position.X + (-36 + OFFSETS[i]) * drawPlayer.direction, position.Y + 14);
            const src = Rectangle.new(28, Math.floor(balloon.Height / 4) * ((timer + i) % 4), 28, 42);
            const origin = Vector2.new(14 + drawPlayer.direction * 7, 42);
            ModMount.AddDrawData(playerDrawData, ModMount.NewDrawData(balloon, at, src, drawColor.value,
                rotation.value + balloons.rotations[i], origin, drawScale.value, spriteEffects.value ^ 1));
        }
        return true;
    }
}
