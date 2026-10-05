class PlayerUpdateHooks {
    static #movement = new Set();

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['PostUpdateMiscEffects'], 'player.MiscEffects', () => {
            Terraria.Player['void CapAttackSpeeds()'].hook((original, player) => {
                original(player);
                PlayerLoader.Call(player, 'PostUpdateMiscEffects');
            });
        });
        want(cls, ['PostUpdateRunSpeeds'], 'player.RunSpeeds', () => {
            Terraria.Player['void HorizontalMovement()'].hook((original, player) => {
                PlayerLoader.Call(player, 'PostUpdateRunSpeeds');
                return original(player);
            });
        });
        want(cls, ['PreUpdateMovement'], 'player.PreMovement', () => {
            Terraria.Player['void Update(int i)'].hook((original, player, index) => {
                const key = bl.addressOf(player), outer = PlayerUpdateHooks.#movement.has(key);
                PlayerUpdateHooks.#movement.delete(key);
                try { return original(player, index); }
                finally {
                    PlayerUpdateHooks.#movement.delete(key);
                    if (outer) PlayerUpdateHooks.#movement.add(key);
                }
            });
            for (const signature of ['void WetCollision(bool fallThrough, bool ignorePlats, float movementSpeed)',
                'void DryCollision(bool fallThrough, bool ignorePlats)', 'void SlopingCollision(bool fallThrough, bool ignorePlats)', 'void BordersMovement()']) {
                Terraria.Player[signature].hook((original, player, ...args) => {
                    const key = bl.addressOf(player);
                    if (!PlayerUpdateHooks.#movement.has(key) && !player.dead) {
                        PlayerUpdateHooks.#movement.add(key);
                        PlayerLoader.Call(player, 'PreUpdateMovement');
                    }
                    return original(player, ...args);
                });
            }
        });
        want(cls, ['NaturalLifeRegen'], 'player.NaturalRegen', () => {
            bl.installPlayerStage('NaturalLifeRegen', (player, value) => {
                const regen = new Ref(value);
                PlayerLoader.Call(player, 'NaturalLifeRegen', regen);
                return Number.isFinite(regen.value) ? regen.value : value;
            });
        });
        want(cls, ['UpdateAutopause'], 'player.Autopause', () => {
            Terraria.Main['void DoUpdate_WhilePaused()'].hook((original) => {
                original();
                const player = Terraria.Main.player[Terraria.Main.myPlayer];
                if (player && !Terraria.Main.gameMenu) PlayerLoader.Call(player, 'UpdateAutopause');
            });
        });
        want(cls, ['ProcessTriggers'], 'player.Triggers', () => {
            Terraria.GameInput.TriggersSet['void CopyInto(Player p)'].hook((original, triggers, player) => {
                original(triggers, player);
                if (player.whoAmI === Terraria.Main.myPlayer) PlayerLoader.Call(player, 'ProcessTriggers', triggers);
            });
        });
        want(cls, ['ResetInfoAccessories'], 'player.InfoAccessories', () => {
            Terraria.Player['void ResetEffects()'].hook((original, player) => {
                original(player);
                PlayerLoader.Call(player, 'ResetInfoAccessories');
            });
            Terraria.Player['void RefreshInfoAccs()'].hook((original, player) => {
                original(player);
                PlayerLoader.Call(player, 'ResetInfoAccessories');
            });
        });
        want(cls, ['ArmorSetBonusActivated'], 'player.SetBonusActivated', () => {
            Terraria.Player['void KeyDoubleTap(int keyDir)'].hook((original, player, direction) => {
                original(player, direction);
                if (direction === (Terraria.Main.ReversedUpDownArmorSetBonuses ? 1 : 0)) PlayerLoader.Call(player, 'ArmorSetBonusActivated');
            });
        });
        want(cls, ['ArmorSetBonusHeld'], 'player.SetBonusHeld', () => {
            Terraria.Player['void KeyHoldDown(int keyDir, int holdTime)'].hook((original, player, direction, duration) => {
                original(player, direction, duration);
                if (direction === (Terraria.Main.ReversedUpDownArmorSetBonuses ? 1 : 0)) PlayerLoader.Call(player, 'ArmorSetBonusHeld', duration);
            });
        });
        want(cls, ['OnEquipmentLoadoutSwitched'], 'player.Loadout', () => {
            Terraria.Player['void TrySwitchingLoadout(int loadoutIndex)'].hook((original, player, index) => {
                const previous = player.CurrentLoadoutIndex;
                original(player, index);
                if (previous !== player.CurrentLoadoutIndex) PlayerLoader.Call(player, 'OnEquipmentLoadoutSwitched', previous, player.CurrentLoadoutIndex);
            });
        });
    }
}
