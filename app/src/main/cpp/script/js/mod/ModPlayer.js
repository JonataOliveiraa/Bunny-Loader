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

    CanUseItem(player, item) { return true; }
    ModifyWeaponDamage(player, item, damage) { this.WeaponDamage = damage; }

    ImmuneTo(player, damageSource, cooldownCounter, dodgeable) { return false; }
    FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) { return false; }
    ModifyHurt(player, modifiers) {}
    OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PreKill(player, damageSource, damage, hitDirection, pvp) { return true; }
    Kill(player, damageSource, damage, hitDirection, pvp) {}

    SaveData(data) {}
    LoadData(data) {}

    // ExampleDashPlayer.get(player)
    static get(player) {
        return PlayerLoader.Of(player)[this.name];
    }

    // A instância do jogador local.
    static getByName(name) {
        return PlayerLoader.Of(Terraria.Main.player[Terraria.Main.myPlayer])[name];
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPlayer)) {
            throw new TypeError('ModPlayer.register(Classe): passe a classe, que estende ModPlayer');
        }
        if (PlayerLoader.Classes.some((c) => c.name === cls.name)) {
            throw new TypeError('ModPlayer.register: ja existe um ModPlayer chamado ' + cls.name);
        }

        Hooks.Once('player.fields', () => {
            Entities.Define(Terraria.Player, 'ModPlayers');
            bl.defineMethod(Terraria.Player, 'GetModPlayer', function (which) {
                const name = typeof which === 'string' ? which : which && which.name;
                return PlayerLoader.Of(this)[name];
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
