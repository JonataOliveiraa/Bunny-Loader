// A coleta de itens do chão, como o ItemLoader do tModLoader: CanPickup,
// OnPickup, ItemSpace, GrabRange e GrabStyle (ModItem e GlobalItem).
//
// O jogo móvel inline o GetItemGrabRange e o CanAcceptItemIntoInventory(WorldItem)
// dentro do GrabItems. O veto e o alcance entram pelo CanAcceptItemIntoInventory(Item),
// que o GrabItems chama para cada item antes da conta do alcance (e o CanPullItem
// da reserva, no WorldItem.FindOwner). A WorldItem vem do bl.items.worldItemOf.
// O alcance maior que o do jogo puxa daqui; o menor recusa o item fora dele.
class ItemPickupLoader {
    static METHODS = ['CanPickup', 'OnPickup', 'ItemSpace', 'GrabRange', 'GrabStyle'];

    // método -> a marca dos tipos de mod que o pedem (o filtro nativo)
    static #MARKS = {
        CanPickup: ['pickup.accept'],
        GrabRange: ['pickup.accept', 'pickup.range'],
        GrabStyle: ['pickup.pull'],
        OnPickup: ['pickup.on'],
        ItemSpace: ['pickup.space'],
    };

    static #wanted = new Set();   // os métodos pedidos
    static #global = new Set();   // as marcas pedidas por um GlobalItem: sem filtro (itens do jogo também)
    static #grab = null;          // o GrabItems, com scope()

    // type: o do ModItem que sobrescreve; -1 para um GlobalItem.
    static Want(method, type = -1) {
        for (const mark of ItemPickupLoader.#MARKS[method]) {
            if (type < 0) ItemPickupLoader.#global.add(mark);
            else bl.hookMarks.set(mark, type);
        }
        if (ItemPickupLoader.#wanted.has(method)) return;

        ItemPickupLoader.#wanted.add(method);
        Ready.Add(() => Hooks.Once('pickup.' + method, () => Safe.Run('coleta (' + method + ')', () => ItemPickupLoader.#Install(method))));
    }

    static #Install(method) {
        switch (method) {
            case 'CanPickup': return Hooks.Once('pickup.accept', ItemPickupLoader.#HookAccept);
            case 'GrabRange':
                Hooks.Once('pickup.accept', ItemPickupLoader.#HookAccept);
                return ItemPickupLoader.#HookRange();
            case 'GrabStyle': return ItemPickupLoader.#HookPull();
            case 'OnPickup': return ItemPickupLoader.#HookPickup();
            case 'ItemSpace': return ItemPickupLoader.#HookSpace();
        }
    }

    // Item no parâmetro 0 (world: uma WorldItem, pelo tipo do inner).
    static #Filter(mark, world) {
        if (ItemPickupLoader.#global.has(mark)) return undefined;
        return world ? { minType: FIRST_ITEM, on: 0, field: 'inner.type', marks: mark } : { minType: FIRST_ITEM, on: 0, marks: mark };
    }

    static #Mod(item, name) {
        const m = ItemLoader.Of(item);
        return m && Hooks.Overrides(m.constructor, ModItem, name) ? m : null;
    }

    // Os Globais e depois o ModItem, até um devolver `stop`.
    static #Until(item, name, stop, call) {
        for (const g of globalItems.For(item, name)) {
            if (Safe.Run(g.constructor.name + '.' + name, () => call(g)) === stop) return true;
        }
        const m = ItemPickupLoader.#Mod(item, name);
        return !!m && Safe.Run(m.constructor.name + '.' + name, () => call(m)) === stop;
    }

    static CanPickup(w, player) {
        return !ItemPickupLoader.#Until(w.inner, 'CanPickup', false, (h) => h.CanPickup(w, player));
    }

    static OnPickup(w, player) {
        return !ItemPickupLoader.#Until(w.inner, 'OnPickup', false, (h) => h.OnPickup(w, player));
    }

    static GrabStyle(w, player) {
        return ItemPickupLoader.#Until(w.inner, 'GrabStyle', true, (h) => h.GrabStyle(w, player));
    }

    // Como no ItemSpace do jogo: o item único que o jogador já tem não entra.
    static ItemSpace(item, player) {
        if (item.uniqueStack && player['bool HasItem(int type)'](item.type)) return false;
        return ItemPickupLoader.#Until(item, 'ItemSpace', true, (h) => h.ItemSpace(item, player));
    }

    // O ModItem e depois os Globais, sobre o alcance do jogo (em pixels).
    static GrabRange(w, player, base) {
        const item = w.inner;
        const range = new Ref(base);
        const m = ItemPickupLoader.#Mod(item, 'GrabRange');
        if (m) Safe.Run(m.constructor.name + '.GrabRange', () => m.GrabRange(w, player, range));
        for (const g of globalItems.For(item, 'GrabRange')) {
            Safe.Run(g.constructor.name + '.GrabRange', () => g.GrabRange(w, player, range));
        }
        return Math.trunc(Number(range.value)) || 0;
    }

    static #HookAccept() {
        const P = Terraria.Player;
        const ranged = ItemPickupLoader.#wanted.has('GrabRange');
        if (ranged) {
            ItemPickupLoader.#grab = P['void GrabItems(int i)'];
            ItemPickupLoader.#grab.scope();
        }

        P['bool CanAcceptItemIntoInventory(Item item)'].hook((original, self, item) => {
            if (!original(self, item)) return false;

            const w = bl.items.worldItemOf(item);
            if (!w) return true;
            if (!ItemPickupLoader.CanPickup(w, self)) return false;
            return !ranged || !ItemPickupLoader.#grab.scopeSelf() || ItemPickupLoader.#Reach(self, w, item);
        }, ItemPickupLoader.#Filter('pickup.accept', false));
    }

    // Dentro do GrabItems, antes da conta do alcance do jogo. false: o jogo
    // pula o item.
    static #Reach(player, w, item) {
        const vanilla = player['int GetItemGrabRange(Item item)'](item);
        const range = ItemPickupLoader.GrabRange(w, player, vanilla);
        if (range === vanilla) return true;

        const pos = player.position, at = w.position;
        const px = Math.trunc(pos.X), py = Math.trunc(pos.Y), pw = player.width, ph = player.height;
        const hx = Math.trunc(at.X), hy = Math.trunc(at.Y), hw = w.width, hh = w.height;
        // O Rectangle.Intersects do retângulo do jogador crescido r pixels.
        const meets = (r) => px - r < hx + hw && hx < px + pw + r && py - r < hy + hh && hy < py + ph + r;

        if (meets(0)) return true;   // encostou: o jogo pega
        if (range < vanilla) return meets(range);
        if (!meets(range) || meets(vanilla)) return true;

        ItemPickupLoader.#Pull(player, w);
        return true;
    }

    // O ramo de puxar do GrabItems, para o item entre o alcance do jogo e o do mod.
    static #Pull(player, w) {
        const status = player['Player.ItemSpaceStatus ItemSpace(WorldItem newItem)'](w);
        if (!player['bool CanPullItem(WorldItem item, Player.ItemSpaceStatus status)'](w, status)) return;

        w.shimmered = false;
        w.beingGrabbed = true;
        // O GrabStyle, quando algum mod o escreve, entra pelo hook do PullItem.
        const fast = status.ItemIsGoingToVoidVault || Terraria.ID.ItemID.Sets.NebulaPickup[w.type] || (player.goldRing && w.IsACoin);
        if (fast) player['void PullItem_Pickup(WorldItem itemToPickUp, float speed, int acc)'](w, 12, 5);
        else player['void PullItem_Common(WorldItem itemToPickUp, float xPullSpeed)'](w, 0.75);
    }

    // O alcance que o WorldItem.FindOwner usa para escolher o dono.
    static #HookRange() {
        Terraria.Player['int GetItemGrabRange(WorldItem item)'].hook(
            (original, self, w) => ItemPickupLoader.GrabRange(w, self, original(self, w)),
            ItemPickupLoader.#Filter('pickup.range', true));
    }

    // O jeito de puxar: true no GrabStyle pula o do jogo.
    static #HookPull() {
        const P = Terraria.Player;
        const filter = ItemPickupLoader.#Filter('pickup.pull', true);
        for (const signature of ['void PullItem_Common(WorldItem itemToPickUp, float xPullSpeed)',
                                 'void PullItem_Pickup(WorldItem itemToPickUp, float speed, int acc)']) {
            P[signature].hook((original, self, w) => {
                if (ItemPickupLoader.GrabStyle(w, self)) return undefined;
                return original();
            }, filter);
        }
    }

    // false no OnPickup: o item some do chão sem entrar no inventário.
    static #HookPickup() {
        Terraria.Player['void PickupItem(WorldItem itemToPickUp)'].hook((original, self, w) => {
            if (ItemPickupLoader.OnPickup(w, self)) return original(self, w);
            w['void TurnToAirAndSync()']();
            return undefined;
        }, ItemPickupLoader.#Filter('pickup.on', true));
    }

    // Os dois caminhos do jogo: o GrabItems (por ref) e o FindOwner (a reserva).
    static #HookSpace() {
        const P = Terraria.Player;
        const filter = ItemPickupLoader.#Filter('pickup.space', false);

        P['void ItemSpace(Item newItem, ref Player.ItemSpaceStatus result)'].hook((original, self, item, result) => {
            original(self, item, result);
            const status = result.value;
            if (status.CanTakeItem || !ItemPickupLoader.ItemSpace(item, self)) return;
            status.CanTakeItem = true;
            result.value = status;
        }, filter);

        P['Player.ItemSpaceStatus ItemSpace(Item newItem)'].hook((original, self, item) => {
            const status = original(self, item);
            if (!status.CanTakeItem && ItemPickupLoader.ItemSpace(item, self)) status.CanTakeItem = true;
            return status;
        }, filter);
    }
}
