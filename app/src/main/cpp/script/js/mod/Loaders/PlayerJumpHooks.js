class ExtraJump {
    constructor(name, field) { this.Name = name; this.Field = field; Object.freeze(this); }
    static CloudInABottle = new ExtraJump('CloudInABottle', 'Cloud');
    static SandstormInABottle = new ExtraJump('SandstormInABottle', 'Sandstorm');
    static BlizzardInABottle = new ExtraJump('BlizzardInABottle', 'Blizzard');
    static FartInAJar = new ExtraJump('FartInAJar', 'Fart');
    static TsunamiInABottle = new ExtraJump('TsunamiInABottle', 'Sail');
    static UnicornMount = new ExtraJump('UnicornMount', 'Unicorn');
    static SantankMount = new ExtraJump('SantankMount', 'Santank');
    static GoatMount = new ExtraJump('GoatMount', 'WallOfFleshGoat');
    static BasiliskMount = new ExtraJump('BasiliskMount', 'Basilisk');
}

class PlayerJumpHooks {
    static Jumps = Object.freeze(Object.values(ExtraJump));
    static #states = PlayerJumpHooks.Jumps.map((jump, index) => ({ jump, bit: 1 << index,
        active: 'isPerformingJump_' + jump.Field, ready: 'canJumpAgain_' + jump.Field, option: 'hasJumpOption_' + jump.Field }));
    static #jumping = null;

    static Install(cls) {
        PlayerLoader.Wants(cls, ['CanStartExtraJump', 'ModifyExtraJumpDurationMultiplier', 'OnExtraJumpStarted', 'OnExtraJumpEnded'],
            'player.ExtraJumps', PlayerJumpHooks.#Movement);
        PlayerLoader.Wants(cls, ['OnExtraJumpStarted'], 'player.ExtraJumpSounds', PlayerJumpHooks.#Sounds);
        PlayerLoader.Wants(cls, ['CanShowExtraJumpVisuals', 'ExtraJumpVisuals'], 'player.JumpVisuals', () => {
            Terraria.Player['void DoubleJumpVisuals()'].hook((original, player) => {
                let hidden = 0;
                for (const state of PlayerJumpHooks.#states) {
                    if (!player[state.active]) continue;
                    if (PlayerLoader.Veto(player, 'CanShowExtraJumpVisuals', state.jump)) { hidden |= state.bit; player[state.active] = false; }
                    else PlayerLoader.Call(player, 'ExtraJumpVisuals', state.jump);
                }
                try { return original(player); }
                finally { for (const state of PlayerJumpHooks.#states) if (hidden & state.bit) player[state.active] = true; }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpRefreshed'], 'player.RefreshJumps', () => {
            Terraria.Player['void RefreshDoubleJumps()'].hook((original, player) => {
                original(player);
                for (const state of PlayerJumpHooks.#states) {
                    if (player[state.option]) PlayerLoader.Call(player, 'OnExtraJumpRefreshed', state.jump);
                }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpEnded'], 'player.EndJumps', () => {
            Terraria.Player['void CancelAllJumpVisualEffects(bool includeDownDash)'].hook((original, player, includeDownDash) => {
                const before = PlayerJumpHooks.#Active(player);
                original(player, includeDownDash);
                for (const state of PlayerJumpHooks.#states) {
                    if ((before & state.bit) && !player[state.active]) PlayerLoader.Call(player, 'OnExtraJumpEnded', state.jump);
                }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpCleared'], 'player.ClearJumps', () => {
            Terraria.Player['void UpdateJumpHeight()'].hook((original, player) => {
                for (const state of PlayerJumpHooks.#states) {
                    if (player[state.ready] && !player[state.option]) PlayerLoader.Call(player, 'OnExtraJumpCleared', state.jump);
                }
                return original(player);
            });
        });
    }

    static #Movement() {
        const gate = Terraria.Player['void JumpMovement()'];
        gate.hook((original, player) => {
            const outer = PlayerJumpHooks.#jumping;
            const start = PlayerLoader.Has('OnExtraJumpStarted'), duration = PlayerLoader.Has('ModifyExtraJumpDurationMultiplier');
            const end = PlayerLoader.Has('OnExtraJumpEnded'), observe = start || duration || end;
            const veto = PlayerLoader.Has('CanStartExtraJump') && player.controlJump && player.releaseJump;
            if (!observe && !veto) return original(player);
            const scope = observe ? { player, before: PlayerJumpHooks.#Active(player), started: 0 } : null;
            let blocked = 0;
            if (veto) for (const state of PlayerJumpHooks.#states) {
                if (player[state.ready] && PlayerLoader.Veto(player, 'CanStartExtraJump', state.jump)) {
                    blocked |= state.bit;
                    player[state.ready] = false;
                }
            }
            PlayerJumpHooks.#jumping = scope;
            try {
                original(player);
                if (start) PlayerJumpHooks.#Started(scope);
                if (duration || end) for (const state of PlayerJumpHooks.#states) {
                    const active = player[state.active];
                    if (duration && !(scope.before & state.bit) && active) {
                        const duration = new Ref(1);
                        PlayerLoader.Call(player, 'ModifyExtraJumpDurationMultiplier', state.jump, duration);
                        if (Number.isFinite(duration.value) && duration.value >= 0) player.jump = Math.max(0, Math.trunc(player.jump * duration.value));
                    } else if (end && (scope.before & state.bit) && !active) PlayerLoader.Call(player, 'OnExtraJumpEnded', state.jump);
                }
            } finally {
                PlayerJumpHooks.#jumping = outer;
                for (const state of PlayerJumpHooks.#states) if (blocked & state.bit) player[state.ready] = true;
            }
        });
    }

    static #Sounds() {
        const gate = Terraria.Player['void JumpMovement()'];
        for (const signature of ['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)',
            'SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)']) {
            Terraria.Audio.SoundEngine[signature].hook((original, ...args) => {
                const scope = PlayerJumpHooks.#jumping;
                if (scope && !PlayerJumpHooks.#Started(scope)) return null;
                return original(...args);
            }, { whileIn: gate });
        }
    }

    static #Started(scope) {
        let sound = true;
        for (const state of PlayerJumpHooks.#states) {
            if ((scope.before & state.bit) || (scope.started & state.bit) || !scope.player[state.active]) continue;
            scope.started |= state.bit;
            const playSound = new Ref(true);
            PlayerLoader.Call(scope.player, 'OnExtraJumpStarted', state.jump, playSound);
            if (playSound.value === false) sound = false;
        }
        return sound;
    }

    static #Active(player) {
        let active = 0;
        for (const state of PlayerJumpHooks.#states) if (player[state.active]) active |= state.bit;
        return active;
    }
}
