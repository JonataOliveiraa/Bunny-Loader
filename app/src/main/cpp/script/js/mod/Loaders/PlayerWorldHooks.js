class PlayerWorldHooks {
    static #fish = null;
    static #rewards = null;
    static #shop = null;
    static #nurse = null;
    static #catching = null;
    static #materials = new Map();

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['GetFishingLevel', 'ModifyFishingAttempt', 'ModifyCaughtFish'], 'player.Fishing', PlayerWorldHooks.#Fishing);
        want(cls, ['CanConsumeBait'], 'player.Bait', () => bl.installPlayerStage('CanConsumeBait',
            (player, bait) => PlayerLoader.Nullable(player, 'CanConsumeBait', bait)));
        want(cls, ['AnglerQuestReward'], 'player.AnglerRewards', PlayerWorldHooks.#Rewards);
        want(cls, ['GetDyeTraderReward'], 'player.DyeReward', () => bl.installPlayerStage('GetDyeTraderReward', (player, nativePool) => {
            const pool = Array.from(nativePool.ToArray());
            PlayerLoader.Call(player, 'GetDyeTraderReward', pool);
            nativePool.Clear();
            for (const type of pool) if (Number.isInteger(type) && type > 0) nativePool.Add(type);
            if (!nativePool.Count) nativePool.Add(0);
        }));
        want(cls, ['CanBuyItem', 'PostBuyItem', 'CanSellItem', 'PostSellItem'], 'player.Shop', PlayerWorldHooks.#Shop);
        want(cls, ['ModifyNursePrice', 'ModifyNurseHeal', 'PostNurseHeal'], 'player.Nurse', PlayerWorldHooks.#Nurse);
        want(cls, ['CanCatchNPC', 'OnCatchNPC'], 'player.Catching', PlayerWorldHooks.#Catching);
        want(cls, ['AddStartingItems', 'ModifyStartingInventory'], 'player.StartingItems', () => {
            bl.classOf('', 'GUIPlayerCreateMenu')['void SetupStartingItems()'].hook((original, menu) => {
                original(menu);
                const player = Terraria.Main.PendingPlayer;
                if (player) PlayerWorldHooks.#Starting(player, false);
            });
            Terraria.Player['void DropItems(bool gemsOnly)'].hook((original, player, gemsOnly) => {
                original(player, gemsOnly);
                if (!gemsOnly && player.difficulty === 1) PlayerWorldHooks.#Starting(player, true);
            });
        });
        want(cls, ['AddMaterialsForCrafting'], 'player.CraftingMaterials', PlayerWorldHooks.#Materials);
        want(cls, ['OnPickup'], 'player.Pickup', () => {
            Terraria.Player['void PickupItem(WorldItem itemToPickUp)'].hook((original, player, worldItem) => {
                const item = worldItem.inner;
                if (PlayerLoader.Veto(player, 'OnPickup', item)) {
                    worldItem['void TurnToAirAndSync()']();
                    return;
                }
                return original(player, worldItem);
            });
        });
        want(cls, ['HoverSlot'], 'player.HoverSlot', () => {
            Terraria.UI.ItemSlot['void OverrideHover(Item[] inv, int context, int slot)'].hook((original, inventory, context, slot) => {
                if (!PlayerLoader.First(PlayerWorldHooks.#Local(), 'HoverSlot', inventory, context, slot)) original(inventory, context, slot);
            });
        });
        want(cls, ['ShiftClickSlot'], 'player.ShiftClickSlot', () => {
            Terraria.UI.ItemSlot['bool OverrideLeftClick(Item[] inv, int context, int slot)'].hook((original, inventory, context, slot) => {
                if (Terraria.UI.ItemSlot.ShiftInUse && PlayerLoader.First(PlayerWorldHooks.#Local(), 'ShiftClickSlot', inventory, context, slot)) return true;
                return original(inventory, context, slot);
            });
        });
        want(cls, ['CanBeTeleportedTo'], 'player.Teleport', () => {
            Terraria.Player['void Teleport(Vector2 newPos, int Style, int extraInfo)'].hook((original, player, position, style, extra) => {
                const context = style === 1 ? 'TeleportRod' : style === 2 ? 'TeleportationPotion' : 'Teleport';
                if (!PlayerLoader.Veto(player, 'CanBeTeleportedTo', position, context)) original(player, position, style, extra);
            });
            Terraria.Player['void UnityTeleport(Vector2 telePos)'].hook((original, player, position) => {
                if (!PlayerLoader.Veto(player, 'CanBeTeleportedTo', position, 'Wormhole')) original(player, position);
            });
        });
    }

    static #Local() { return Terraria.Main.player[Terraria.Main.myPlayer]; }
    static #Vendor(player) { return player.talkNPC >= 0 ? Terraria.Main.npc[player.talkNPC] : null; }

    static #Fishing() {
        bl.installPlayerStage('GetFishingLevel', (player, result) => {
            const inventory = player.inventory;
            let rod = null, bait = null;
            for (let i = 0; i < inventory.length; i++) {
                const item = inventory[i];
                if (item && item.stack > 0) {
                    if (!rod && item.type === result.PoleItemType) rod = item;
                    if (!bait && item.type === result.BaitItemType) bait = item;
                }
            }
            const level = new Ref(result.FinalFishingLevel);
            PlayerLoader.Call(player, 'GetFishingLevel', rod, bait, level);
            if (Number.isFinite(level.value)) result.FinalFishingLevel = Math.max(0, Math.trunc(level.value));
            return result;
        });
        Terraria.Projectile['void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)'].hook((original, projectile, attempt) => {
            const player = Terraria.Main.player[projectile.owner];
            if (player) PlayerLoader.Call(player, 'ModifyFishingAttempt', attempt.value);
            return original(projectile, attempt);
        });
        Terraria.Projectile['void AI_061_FishingBobber_GiveItemToPlayer(Player thePlayer, int itemType)'].hook((original, projectile, player, type) => {
            const outer = PlayerWorldHooks.#fish;
            PlayerWorldHooks.#fish = player;
            try { return original(projectile, player, type); }
            finally { PlayerWorldHooks.#fish = outer; }
        });
        Terraria.Player['void QuickSpawnItem(IEntitySource source, Item item, GetItemSettings settings)'].hook((original, player, source, item, settings) => {
            if (PlayerWorldHooks.#fish && bl.addressOf(PlayerWorldHooks.#fish) === bl.addressOf(player)) {
                const outer = PlayerWorldHooks.#fish;
                PlayerWorldHooks.#fish = null;
                try { PlayerLoader.Call(player, 'ModifyCaughtFish', item); }
                finally { PlayerWorldHooks.#fish = outer; }
            }
            return original(player, source, item, settings);
        });
    }

    static #Rewards() {
        Terraria.Player['void GetAnglerReward(NPC angler, int questItemType)'].hook((original, player, angler, questType) => {
            const outer = PlayerWorldHooks.#rewards;
            const scope = { player, items: [], settings: null, multiplier: 1 };
            PlayerWorldHooks.#rewards = scope;
            try { original(player, angler, questType); }
            finally { PlayerWorldHooks.#rewards = outer; }
            PlayerLoader.Call(player, 'AnglerQuestReward', scope.multiplier, scope.items);
            for (const item of scope.items) if (item && item.type > 0 && item.stack > 0) {
                player['void GetOrDropItem(Item item, GetItemSettings settings)'](item, scope.settings || Terraria.GetItemSettings.NPCEntityToPlayerInventorySettings);
            }
        });
        Terraria.Player['float GetAnglerRewardRarityMultiplier(int questsDone)'].hook((original, quests) => {
            const value = original(quests);
            if (PlayerWorldHooks.#rewards) PlayerWorldHooks.#rewards.multiplier = value;
            return value;
        });
        Terraria.Player['void GetOrDropItem(Item item, GetItemSettings settings)'].hook((original, player, item, settings) => {
            const scope = PlayerWorldHooks.#rewards;
            if (!scope || bl.addressOf(scope.player) !== bl.addressOf(player)) return original(player, item, settings);
            scope.items.push(item);
            scope.settings = settings;
        });
    }

    static #Shop() {
        Terraria.UI.ItemSlot['void HandleShopSlot(Item[] inv, int slot, bool rightClickIsValid, bool leftClickIsValid)'].hook((original, inventory, slot, right, left) => {
            const outer = PlayerWorldHooks.#shop, player = PlayerWorldHooks.#Local();
            const scope = { player, inventory, item: inventory[slot], vendor: PlayerWorldHooks.#Vendor(player), bought: false };
            PlayerWorldHooks.#shop = scope;
            try { original(inventory, slot, right, left); }
            finally { PlayerWorldHooks.#shop = outer; }
            if (scope.bought) PlayerLoader.Call(player, 'PostBuyItem', scope.vendor, inventory, Terraria.Main.mouseItem);
        });
        Terraria.Player['bool BuyItem(long price, int customCurrency)'].hook((original, player, price, currency) => {
            const scope = PlayerWorldHooks.#shop;
            if (scope && bl.addressOf(scope.player) === bl.addressOf(player) && PlayerLoader.Veto(player, 'CanBuyItem', scope.vendor, scope.inventory, scope.item)) return false;
            const result = original(player, price, currency);
            if (result && scope && bl.addressOf(scope.player) === bl.addressOf(player)) scope.bought = true;
            if (result && PlayerWorldHooks.#nurse && bl.addressOf(PlayerWorldHooks.#nurse.player) === bl.addressOf(player)) PlayerWorldHooks.#nurse.paid = true;
            return result;
        });
        Terraria.Player['bool SellItem(Item item, int stack)'].hook((original, player, item, stack) => {
            const vendor = PlayerWorldHooks.#Vendor(player);
            if (!vendor) return original(player, item, stack);
            const inventory = Terraria.Main.shop[Terraria.Main.npcShop].item;
            if (PlayerLoader.Veto(player, 'CanSellItem', vendor, inventory, item)) return false;
            const result = original(player, item, stack);
            if (result) PlayerLoader.Call(player, 'PostSellItem', vendor, inventory, item);
            return result;
        });
    }

    static #NursePlan(player) {
        const nurse = PlayerWorldHooks.#Vendor(player), health = new Ref(Math.max(0, player.statLifeMax2 - player.statLife));
        const debuffs = new Ref(true), chat = new Ref(Terraria.Main.npcChatText);
        const allowed = !PlayerLoader.Veto(player, 'ModifyNurseHeal', nurse, health, debuffs, chat);
        return { player, nurse, health: Math.max(0, Math.trunc(health.value)), removeDebuffs: debuffs.value !== false, chat: chat.value, allowed, paid: false };
    }

    static #WithNurse(plan, run) {
        const player = plan.player, max = player.statLifeMax2, hidden = new Map(), debuff = Terraria.Main.debuff;
        if (!plan.removeDebuffs) {
            for (const type of player.buffType) {
                if (!hidden.has(type)) { hidden.set(type, debuff[type]); debuff[type] = false; }
            }
        }
        player.statLifeMax2 = player.statLife + Math.min(plan.health, Math.max(0, max - player.statLife));
        try { return run(); }
        finally {
            player.statLifeMax2 = max;
            for (const [type, value] of hidden) debuff[type] = value;
        }
    }

    static #Nurse() {
        Terraria.Main['int GetNurseHealCost()'].hook((original) => {
            const plan = PlayerWorldHooks.#NursePlan(PlayerWorldHooks.#Local());
            const price = new Ref(PlayerWorldHooks.#WithNurse(plan, () => original()));
            PlayerLoader.Call(plan.player, 'ModifyNursePrice', plan.nurse, plan.health, plan.removeDebuffs, price);
            return Number.isFinite(price.value) ? Math.max(0, Math.trunc(price.value)) : 0;
        });
        Terraria.Main['void NPCChatText_DoNurseHeal(int healCost)'].hook((original, price) => {
            const player = PlayerWorldHooks.#Local(), plan = PlayerWorldHooks.#NursePlan(player);
            if (!plan.allowed) { Terraria.Main.npcChatText = plan.chat; return; }
            const outer = PlayerWorldHooks.#nurse;
            plan.price = price;
            PlayerWorldHooks.#nurse = plan;
            try { PlayerWorldHooks.#WithNurse(plan, () => original(Math.max(1, price))); }
            finally { PlayerWorldHooks.#nurse = outer; }
            if (plan.paid) PlayerLoader.Call(player, 'PostNurseHeal', plan.nurse, plan.health, plan.removeDebuffs, price);
        });
        Hooks.Once('player.NursePayment', () => {
            Terraria.Player['bool BuyItem(long price, int customCurrency)'].hook((original, player, price, currency) => {
                const plan = PlayerWorldHooks.#nurse;
                const result = original(player, plan && bl.addressOf(plan.player) === bl.addressOf(player) ? plan.price : price, currency);
                if (result && plan && bl.addressOf(plan.player) === bl.addressOf(player)) plan.paid = true;
                return result;
            });
        });
    }

    static #Catching() {
        Terraria.Player['void ItemCheck_CatchCritters(Item sItem, Rectangle itemRectangle)'].hook((original, player, item, rectangle) => {
            const outer = PlayerWorldHooks.#catching, blocked = [], scope = { player, item };
            PlayerWorldHooks.#catching = scope;
            try {
                for (let i = 0; i < Terraria.Main.npc.length; i++) {
                    const npc = Terraria.Main.npc[i];
                    if (!npc || !npc.active || npc.catchItem <= 0 || !rectangle['bool Intersects(Rectangle rect)'](npc.Hitbox)) continue;
                    const decision = PlayerLoader.Nullable(player, 'CanCatchNPC', npc, item);
                    if (decision === false) { blocked.push([npc, npc.catchItem]); npc.catchItem = 0; }
                    else if (decision === true) Terraria.NPC['void CatchNPC(int i, int who)'](i, player.whoAmI);
                }
                return original(player, item, rectangle);
            } finally {
                for (const [npc, type] of blocked) npc.catchItem = type;
                PlayerWorldHooks.#catching = outer;
            }
        });
        Terraria.NPC['void CatchNPC(int i, int who)'].hook((original, index, who) => {
            const scope = PlayerWorldHooks.#catching;
            const player = scope ? scope.player : Terraria.Main.player[who >= 0 ? who : Terraria.Main.myPlayer];
            const npc = Terraria.Main.npc[index], item = scope ? scope.item : player.inventory[player.selectedItem];
            if (!scope && PlayerLoader.Nullable(player, 'CanCatchNPC', npc, item) === false) return;
            original(index, who);
            PlayerLoader.Call(player, 'OnCatchNPC', npc, item, false);
        });
        Terraria.Player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'].hook(
            (original, player, source, damage, direction, pvp, quiet, crit, cooldown, dodgeable) => {
                const scope = PlayerWorldHooks.#catching;
                if (scope && bl.addressOf(scope.player) === bl.addressOf(player) && source._sourceNPCIndex >= 0) {
                    const npc = Terraria.Main.npc[source._sourceNPCIndex];
                    PlayerLoader.Call(player, 'OnCatchNPC', npc, scope.item, true);
                }
                return original(player, source, damage, direction, pvp, quiet, crit, cooldown, dodgeable);
            });
    }

    static #Starting(player, death) {
        const vanilla = [];
        for (let i = 0; i < 50; i++) if (player.inventory[i].type > 0 && player.inventory[i].stack > 0) vanilla.push(player.inventory[i]);
        const byMod = new Map([['Terraria', vanilla]]);
        PlayerLoader.Each(player, 'AddStartingItems', (inst) => {
            const items = inst.AddStartingItems(player, death);
            if (!items) return;
            const owner = PlayerLoader.OwnerOf(inst.constructor), key = owner ? owner.uuid : 'sem-mod';
            if (!byMod.has(key)) byMod.set(key, []);
            byMod.get(key).push(...Array.from(items));
        });
        PlayerLoader.Call(player, 'ModifyStartingInventory', byMod, death);
        const items = [...byMod.values()].flat().filter((item) => item && item.type > 0 && item.stack > 0);
        for (let i = 0; i < 50; i++) {
            const item = i < items.length ? items[i] : Terraria.Item.new();
            if (i >= items.length) item['void .ctor()']();
            player.inventory[i] = item;
        }
        for (const item of items.slice(50)) player['void GetOrDropItem(Item item, GetItemSettings settings)'](item, Terraria.GetItemSettings.NPCEntityToPlayerInventorySettings);
    }

    static #Materials() {
        Terraria.Recipe['void BuildPlayerChestSourceList(Player player, List`1 outputList)'].hook((original, player, list) => {
            original(player, list);
            const playerKey = bl.addressOf(player);
            for (const [key, source] of PlayerWorldHooks.#materials) if (source.playerKey === playerKey) PlayerWorldHooks.#materials.delete(key);
            const seen = new Set();
            PlayerLoader.Each(player, 'AddMaterialsForCrafting', (inst) => {
                const callback = new Ref(null), result = inst.AddMaterialsForCrafting(player, callback);
                if (!result) return;
                const entries = Array.from(result).map((item, index) => ({ item, index })).filter(({ item }) => {
                    if (!item || item.type <= 0 || item.stack <= 0 || seen.has(bl.addressOf(item))) return false;
                    seen.add(bl.addressOf(item));
                    return true;
                });
                for (let start = 0; start < entries.length; start += 200) {
                    const chunk = entries.slice(start, start + 200), storage = Terraria.InventoryStorage.new();
                    storage.maxItems = chunk.length;
                    storage.item = chunk.map(({ item }) => item);
                    const destination = Terraria.GameContent.DestinationInventory.new();
                    destination['void .ctor(InventoryStorage storage, Vector2 position)'](storage, player.Center);
                    list.Add(destination);
                    PlayerWorldHooks.#materials.set(bl.addressOf(storage.item), { chunk, callback: callback.value, inst, playerKey });
                }
            });
        });
        Terraria.GameContent.CraftingRequests['void ConsumeItemsFrom(Item[] inventory, int maxItems, RequiredItemEntry req, ref int toConsume, List`1 consumedItems, int chestIndex)'].hook(
            (original, inventory, max, required, remaining, consumed, chest) => {
                const source = PlayerWorldHooks.#materials.get(bl.addressOf(inventory));
                if (!source) return original(inventory, max, required, remaining, consumed, chest);
                const before = source.chunk.map(({ item }) => item.stack);
                const consumedStart = consumed ? consumed.Count : 0;
                original(inventory, max, required, remaining, consumed, chest);
                source.chunk.forEach(({ item, index }, i) => {
                    if (bl.addressOf(inventory[i]) !== bl.addressOf(item)) {
                        if (consumed) for (let c = consumedStart; c < consumed.Count; c++) {
                            if (bl.addressOf(consumed.get_Item(c)) === bl.addressOf(item)) consumed.set_Item(c, item['Item Clone()']());
                        }
                        item['void TurnToAir()']();
                        inventory[i] = item;
                    }
                    if (item.stack < before[i] && typeof source.callback === 'function') {
                        Safe.Run(source.inst.constructor.name + '.ItemConsumed', () => source.callback(item, index));
                    }
                });
            });
    }
}
