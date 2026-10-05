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
    static #jumping = null;

    static Install(cls) {
        PlayerLoader.Wants(cls, ['CanStartExtraJump', 'ModifyExtraJumpDurationMultiplier', 'OnExtraJumpStarted', 'OnExtraJumpEnded', 'OnExtraJumpCleared'],
            'player.ExtraJumps', PlayerJumpHooks.#Movement);
        PlayerLoader.Wants(cls, ['CanShowExtraJumpVisuals', 'ExtraJumpVisuals'], 'player.JumpVisuals', () => {
            Terraria.Player['void DoubleJumpVisuals()'].hook((original, player) => {
                const hidden = [];
                for (const jump of PlayerJumpHooks.Jumps) {
                    const field = 'isPerformingJump_' + jump.Field;
                    if (!player[field]) continue;
                    if (PlayerLoader.Veto(player, 'CanShowExtraJumpVisuals', jump)) { hidden.push(field); player[field] = false; }
                    else PlayerLoader.Call(player, 'ExtraJumpVisuals', jump);
                }
                try { return original(player); }
                finally { for (const field of hidden) player[field] = true; }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpRefreshed'], 'player.RefreshJumps', () => {
            Terraria.Player['void RefreshDoubleJumps()'].hook((original, player) => {
                original(player);
                for (const jump of PlayerJumpHooks.Jumps) {
                    if (player['hasJumpOption_' + jump.Field]) PlayerLoader.Call(player, 'OnExtraJumpRefreshed', jump);
                }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpEnded'], 'player.EndJumps', () => {
            Terraria.Player['void CancelAllJumpVisualEffects(bool includeDownDash)'].hook((original, player, includeDownDash) => {
                const before = PlayerJumpHooks.Jumps.filter((jump) => player['isPerformingJump_' + jump.Field]);
                original(player, includeDownDash);
                for (const jump of before) {
                    if (!player['isPerformingJump_' + jump.Field]) PlayerLoader.Call(player, 'OnExtraJumpEnded', jump);
                }
            });
        });
        PlayerLoader.Wants(cls, ['OnExtraJumpCleared'], 'player.ClearJumps', () => {
            Terraria.Player['void UpdateJumpHeight()'].hook((original, player) => {
                for (const jump of PlayerJumpHooks.Jumps) {
                    if (player['canJumpAgain_' + jump.Field] && !player['hasJumpOption_' + jump.Field]) PlayerLoader.Call(player, 'OnExtraJumpCleared', jump);
                }
                return original(player);
            });
        });
    }

    static #Movement() {
        Terraria.Player['void JumpMovement()'].hook((original, player) => {
            const outer = PlayerJumpHooks.#jumping;
            const scope = { player, before: new Map(), started: new Set() }, blocked = [];
            for (const jump of PlayerJumpHooks.Jumps) {
                scope.before.set(jump, player['isPerformingJump_' + jump.Field]);
                const field = 'canJumpAgain_' + jump.Field;
                if (player.controlJump && player.releaseJump && player[field] && PlayerLoader.Veto(player, 'CanStartExtraJump', jump)) {
                    blocked.push(field);
                    player[field] = false;
                }
            }
            PlayerJumpHooks.#jumping = scope;
            try {
                original(player);
                PlayerJumpHooks.#Started(scope);
                for (const jump of PlayerJumpHooks.Jumps) {
                    const active = player['isPerformingJump_' + jump.Field];
                    if (!scope.before.get(jump) && active) {
                        const duration = new Ref(1);
                        PlayerLoader.Call(player, 'ModifyExtraJumpDurationMultiplier', jump, duration);
                        if (Number.isFinite(duration.value) && duration.value >= 0) player.jump = Math.max(0, Math.trunc(player.jump * duration.value));
                    } else if (scope.before.get(jump) && !active) PlayerLoader.Call(player, 'OnExtraJumpEnded', jump);
                }
            } finally {
                PlayerJumpHooks.#jumping = outer;
                for (const field of blocked) player[field] = true;
            }
        });
        for (const signature of ['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)',
            'SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)']) {
            Terraria.Audio.SoundEngine[signature].hook((original, ...args) => {
                const scope = PlayerJumpHooks.#jumping;
                if (scope && !PlayerJumpHooks.#Started(scope)) return null;
                return original(...args);
            });
        }
    }

    static #Started(scope) {
        let sound = true;
        for (const jump of PlayerJumpHooks.Jumps) {
            if (scope.before.get(jump) || scope.started.has(jump) || !scope.player['isPerformingJump_' + jump.Field]) continue;
            scope.started.add(jump);
            const playSound = new Ref(true);
            PlayerLoader.Call(scope.player, 'OnExtraJumpStarted', jump, playSound);
            if (playSound.value === false) sound = false;
        }
        return sound;
    }
}
