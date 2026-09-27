// Uma base que outras estendem fica de fora (Autoload = false); a filha entra.
export class AutoBase extends ModItem {
    static Autoload = false;

    SetDefaults(item) {
        item.maxStack = 99;
    }
}

export class AutoDerived extends AutoBase {}

// O que não é classe de mod é ignorado.
export class NotMod {}
export function helper() { return 42; }
export const NUMBER = 7;
