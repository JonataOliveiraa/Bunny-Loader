// As asas, como o ItemLoader do tModLoader: o VerticalWingSpeeds (subida e
// queda, dentro do WingMovement), o HorizontalWingSpeeds (a corrida no ar,
// depois do WingAirLogicTweaks) e o WingUpdate (quem anima as asas: true pula
// o WingFrame do jogo). O item das asas é o vestido; sem ele (asa posta pelo
// FrameEffects), a textura do slot. Os GlobalItem valem para toda asa.
class WingLoader {
    static #global = false;   // algum GlobalItem pediu: sem filtro nativo (asas do jogo também)
    static #wanted = new Set();

    // method: 'Vertical' | 'Horizontal' | 'Update'. Instalado com o conteúdo
    // pronto: aí o Count de fábrica das asas é conhecido e o filtro nativo usa.
    static Want(method, global = false) {
        if (global) WingLoader.#global = true;
        if (WingLoader.#wanted.has(method)) return;

        WingLoader.#wanted.add(method);
        const install = { Vertical: WingLoader.#HookVertical, Horizontal: WingLoader.#HookHorizontal, Update: WingLoader.#HookUpdate }[method];
        Ready.Add(() => Hooks.Once('wings.' + method, () => Safe.Run('asas (' + method + ')', install)));
    }

    // Só as asas de mod entram no JS, a não ser que um GlobalItem queira todas.
    static #Filter(field) {
        const vanilla = EquipLoader.VanillaCount(EquipType.Wings);
        return WingLoader.#global || !vanilla ? undefined : { minType: vanilla, field };
    }

    static #Overrides(g, method) { return Hooks.Overrides(g.constructor, GlobalItem, method); }

    // O item vestido com essas asas, nas casas de acessório liberadas.
    static #Equipped(player, slot) {
        const armor = player.armor;
        for (let i = 3; i < 10; i++) {
            const item = armor[i];
            if (item.wingSlot === slot && player.IsItemSlotUnlockedAndUsable(i)) return item;
        }
        return null;
    }

    // Chama o gancho no ModItem do item vestido, ou na textura, e nos Globais.
    static #Dispatch(player, slot, name, withItem, withTexture, withGlobal) {
        const item = WingLoader.#Equipped(player, slot);
        const m = item ? ItemLoader.Of(item) : undefined;
        if (m) Safe.Run(m.constructor.name + '.' + name, () => withItem(m, item));
        else if (!item) {
            const tex = EquipLoader.GetEquipTexture(EquipType.Wings, slot);
            if (tex) Safe.Run(tex.Name + '.' + name, () => withTexture(tex));
        }

        for (const g of globalItems.list) {
            if (WingLoader.#Overrides(g, name)) Safe.Run(g.constructor.name + '.' + name, () => withGlobal(g, item));
        }
    }

    // O WingMovement do jogo, com os cinco valores passando pelos mods antes de
    // valer (onde o tModLoader chama o VerticalWingSpeeds). A asa 4 subindo
    // tem caminho próprio e segue no do jogo.
    static #HookVertical() {
        Terraria.Player['void WingMovement()'].hook((original, self) => {
            if (self.wingsLogic === 4 && self.TryingToHoverUp) return original(self);

            WingLoader.#WingMovement(self);
            return undefined;
        }, WingLoader.#Filter('wingsLogic'));
    }

    static #HookHorizontal() {
        Terraria.Player['void WingAirLogicTweaks()'].hook((original, self) => {
            original(self);
            if (self.wings <= 0) return;

            const speed = new Ref(self.accRunSpeed), acceleration = new Ref(self.runAcceleration);
            WingLoader.#Dispatch(self, self.wingsLogic, 'HorizontalWingSpeeds',
                (m, item) => m.HorizontalWingSpeeds(item, self, speed, acceleration),
                (tex) => tex.HorizontalWingSpeeds(self, speed, acceleration),
                (g, item) => g.HorizontalWingSpeeds(item, self, speed, acceleration));
            self.accRunSpeed = speed.value;
            self.runAcceleration = acceleration.value;
        }, WingLoader.#Filter('wingsLogic'));
    }

    // Pelo slot desenhado (player.wings), como o tModLoader.
    static #HookUpdate() {
        Terraria.Player['void WingFrame(bool wingFlap)'].hook((original, self, wingFlap) => {
            const wings = self.wings;
            let custom = false;
            const tex = EquipLoader.GetEquipTexture(EquipType.Wings, wings);
            if (tex) custom = Safe.Run(tex.Name + '.WingUpdate', () => tex.WingUpdate(self, wingFlap)) === true;
            for (const g of globalItems.list) {
                if (WingLoader.#Overrides(g, 'WingUpdate') &&
                    Safe.Run(g.constructor.name + '.WingUpdate', () => g.WingUpdate(wings, self, wingFlap)) === true) custom = true;
            }

            if (!custom) original(self, wingFlap);
        }, WingLoader.#Filter('wings'));
    }

    static #WingMovement(player) {
        const logic = player.wingsLogic;
        const jump = Terraria.Player.jumpSpeed;
        const velocity = player.velocity;
        const v = { constant: 0.1, falling: 0.5, maxAscent: 1.5, maxCan: 0.5, rising: 0.1 };

        if (logic === 26 || logic === 37) Object.assign(v, { falling: 0.75, rising: 0.15, maxCan: 1, maxAscent: 2.5, constant: 0.125 });
        if ([8, 11, 24, 27, 22].includes(logic)) v.maxAscent = 1.66;
        if ([21, 12, 20, 23].includes(logic)) v.maxAscent = 1.805;
        if (logic === 44) {
            Object.assign(v, { falling: 0.85, rising: 0.15, maxCan: 1, maxAscent: 2.75, constant: 0.125 });
            WingLoader.#Hover(player, velocity, jump);
        }
        if (logic === 45) {
            Object.assign(v, { falling: 0.95, rising: 0.15, maxCan: 1, maxAscent: 4.5 });
            WingLoader.#Hover(player, velocity, jump);
        }
        if (logic === 29 || logic === 32) Object.assign(v, { falling: 0.85, rising: 0.15, maxCan: 1, maxAscent: 3, constant: 0.135 });
        if (logic === 30 || logic === 31) {
            Object.assign(v, { maxCan: 1, maxAscent: 2.45 });
            if (!player.TryingToHoverDown) v.constant = 0.15;
        }

        const refs = {
            falling: new Ref(v.falling), rising: new Ref(v.rising), maxCan: new Ref(v.maxCan),
            maxAscent: new Ref(v.maxAscent), constant: new Ref(v.constant),
        };
        WingLoader.#Dispatch(player, logic, 'VerticalWingSpeeds',
            (m, item) => m.VerticalWingSpeeds(item, player, refs.falling, refs.rising, refs.maxCan, refs.maxAscent, refs.constant),
            (tex) => tex.VerticalWingSpeeds(player, refs.falling, refs.rising, refs.maxCan, refs.maxAscent, refs.constant),
            (g, item) => g.VerticalWingSpeeds(item, player, refs.falling, refs.rising, refs.maxCan, refs.maxAscent, refs.constant));

        const grav = player.gravDir;
        const falling = refs.falling.value, rising = refs.rising.value;
        const maxCan = refs.maxCan.value, maxAscent = refs.maxAscent.value;
        let y = velocity.Y - refs.constant.value * grav;
        if (grav === 1) {
            if (y > 0) y -= falling;
            else if (y > -jump * maxCan) y -= rising;
            if (y < -jump * maxAscent) y = -jump * maxAscent;
        } else {
            if (y < 0) y += falling;
            else if (y < jump * maxCan) y += rising;
            if (y > jump * maxAscent) y = jump * maxAscent;
        }
        velocity.Y = y;

        const slowDown = [22, 28, 30, 31, 37, 45].includes(logic) && player.TryingToHoverDown && !player.controlLeft && !player.controlRight;
        player.wingTime -= slowDown ? 0.5 : 1;
        if (player.empressBrooch && player.wingTime !== 0) player.wingTime = player.wingTimeMax;
    }

    // O pairar das asas 44 e 45 (subir segurando, descer segurando para baixo).
    static #Hover(player, velocity, jump) {
        if (player.TryingToHoverUp) {
            const grav = player.gravDir;
            let y = velocity.Y - 0.4 * grav;
            if (grav === 1) {
                if (y > 0) y -= 1;
                else if (y > -jump) y -= 0.2;
                if (y < -jump * 3) y = -jump * 3;
            } else {
                if (y < 0) y += 1;
                else if (y < jump) y += 0.2;
                if (y > jump * 3) y = jump * 3;
            }
            velocity.Y = y;
        }
        if (player.TryingToHoverDown && !player.controlJump && velocity.Y !== 0) velocity.Y = velocity.Y + 0.4;
    }
}
