// Uma montaria de mod, como o ModMount do tModLoader: uma instância por tipo
// (o jogo guarda o estado de cada jogador no Mount dele), com o MountData do
// jogo em MountData. Texturas: Texture + '_Back', '_Front'... (as camadas de
// MountTextureType), só as que existem. Os parâmetros `ref` do tModLoader
// chegam como Ref (.value).
class ModMount {
    Type = undefined;
    MountData = null;   // Terraria.Mount.MountData, antes do SetStaticDefaults
    Texture = this.constructor.name;

    SetStaticDefaults() {}

    // Pulo: jumpHeight e jumpSpeed são Ref, com o valor que o jogo calculou.
    JumpHeight(mountedPlayer, jumpHeight, xVelocity) {}
    JumpSpeed(mountedPlayer, jumpSpeed, xVelocity) {}
    // A cada quadro montado, antes dos efeitos do jogo.
    UpdateEffects(player) {}
    // false pula a animação do jogo. state: 0 parado, 1 andando, 2 no ar,
    // 3 voando, 4 nadando, 5 correndo (dash).
    UpdateFrame(mountedPlayer, state, velocity) { return true; }
    UseAbility(player, mousePosition, toggleOn) {}
    AimAbility(player, mousePosition) {}
    // Ao montar e desmontar; skipDust (Ref) true pula a poeira do jogo.
    SetMount(player, skipDust) {}
    Dismount(player, skipDust) {}
    // Uma vez por camada com textura (drawType 0 Back, 1 BackExtra, 2 Front,
    // 3 FrontExtra). texture, glowTexture, drawPosition, frame, drawColor,
    // glowColor, rotation, spriteEffects, drawOrigin e drawScale são Ref com
    // o que o jogo desenharia; mudados, desenha-se com os novos. false: o jogo
    // não desenha a camada. Um desenho a mais (o playerDrawData.Add do
    // tModLoader): ModMount.AddDrawData(playerDrawData, ModMount.NewDrawData(...)).
    Draw(playerDrawData, drawType, drawPlayer, texture, glowTexture, drawPosition, frame, drawColor,
         glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow) { return true; }

    // O dado de cada jogador nesta montaria (o player.mount._mountSpecificData
    // do tModLoader, que no C# é object): vale até ele desmontar.
    static GetSpecificData(player) { return MountLoader.SpecificData(player); }
    static SetSpecificData(player, value) { MountLoader.SetSpecificData(player, value); }

    // new DrawData(texture, position, sourceRect, color, rotation, origin, scale, effect).
    static NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect) {
        return MountLoader.NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect);
    }
    static AddDrawData(playerDrawData, data) { return MountLoader.AddDrawData(playerDrawData, data); }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModMount)) {
            throw new TypeError('ModMount.register(Classe): passe a classe, que estende ModMount');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        MountLoader.Add(inst);
        return inst.Type;
    }
}

// As camadas de textura, na ordem do MountTextureType do tModLoader: o nome é
// o sufixo do arquivo e o campo do MountData.
const MountTextureType = Object.freeze({
    Back: 'Back', BackGlow: 'BackGlow', BackExtra: 'BackExtra', BackExtraGlow: 'BackExtraGlow',
    Front: 'Front', FrontGlow: 'FrontGlow', FrontExtra: 'FrontExtra', FrontExtraGlow: 'FrontExtraGlow',
});
