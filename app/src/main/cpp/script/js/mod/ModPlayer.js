// Os métodos recebem o jogador (o mesmo this.Player).
class ModPlayer {
    // Somados ao máximo de vida/mana no ModifyMaxStats.
    CumulativeHealth = 0;
    CumulativeMana = 0;
    WeaponDamage = 0;

    get Player() { return Entities.Of(this); }

    Initialize() {}
    OnEnterWorld(player) {}
    OnRespawn(player) {}

    ResetEffects(player) {}
    ModifyMaxStats(player) {
        this.CumulativeHealth = 0;
        this.CumulativeMana = 0;
    }
    PreUpdate(player) {}
    PostUpdate(player) {}
    PreUpdateBuffs(player) {}
    PostUpdateBuffs(player) {}
    UpdateEquips(player) {}
    PostUpdateEquips(player) {}
    UpdateBadLifeRegen(player) {}
    UpdateLifeRegen(player) {}
    UpdateManaRegen(player) {}
    UpdateDead(player) {}
    UpdateMovement(player) {}
    // Cada quadro, depois de o jogo montar o que se veste: trocar player.head/body/legs muda o desenho.
    FrameEffects(player) {}

    CanUseItem(player, item) { return true; }
    // damage vale como número (devolva o novo) e como StatModifier (damage.Additive += 0.1).
    ModifyWeaponDamage(player, item, damage) {}

    ImmuneTo(player, damageSource, cooldownCounter, dodgeable) { return false; }
    FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) { return false; }
    ModifyHurt(player, modifiers) {}
    OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PreKill(player, damageSource, damage, hitDirection, pvp) { return true; }
    Kill(player, damageSource, damage, hitDirection, pvp) {}
    // Pesca, depois do sorteio do jogo: itemDrop e npcSpawn são Ref (o item e o
    // inimigo que saem; 0 = nenhum). sonar e sonarPosition existem pela
    // assinatura do tModLoader e ainda não fazem nada.
    CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition) {}

    SaveData(data) {}
    LoadData(data) {}

    static get(player) {
        return PlayerLoader.Of(player).get(this);
    }

    static getByName(name) {
        return PlayerLoader.Find(Terraria.Main.player[Terraria.Main.myPlayer], name);
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPlayer)) {
            throw new TypeError('ModPlayer.register(Classe): passe a classe, que estende ModPlayer');
        }

        Hooks.Once('player.fields', () => {
            Entities.Define(Terraria.Player, 'ModPlayers');
            bl.defineMethod(Terraria.Player, 'GetModPlayer', function (which) {
                return PlayerLoader.Find(this, which);
            });
        });

        PlayerLoader.Add(cls);
        PlayerLoader.Hook(cls);

        if (Hooks.Overrides(cls, ModPlayer, 'SaveData') || Hooks.Overrides(cls, ModPlayer, 'LoadData')) {
            Hooks.Once('player.save', PlayerLoader.InstallSave);
        }
        return cls;
    }
}
