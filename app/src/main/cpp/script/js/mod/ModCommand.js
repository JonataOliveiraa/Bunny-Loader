// Um comando de chat, como o ModCommand do tModLoader: "/<Command> args".
// Uma instância por comando. Aliases é do ExMod: outros nomes para o mesmo.
// Usage e Description vêm, se o mod não os escrever, do Localization em
// Commands.<Classe>.Usage/Description (como no tModLoader).
class ModCommand {
    get Command() { return ''; }
    get Type() { return CommandType.Chat; }
    get Aliases() { return []; }
    get Usage() { return ModCommand.#Text(this, 'Usage') || '/' + this.Command; }
    get Description() { return ModCommand.#Text(this, 'Description'); }
    get IsCaseSensitive() { return false; }

    SetStaticDefaults() {}
    // caller: { Player, CommandType, Reply(texto, cor) }. Lance
    // new UsageException(texto) para responder o erro; false (do ExMod)
    // responde o Usage.
    Action(caller, input, args) {}

    // A cor das respostas do jogo (a do /help).
    static get ResponseColor() { return Color.new(255, 240, 20); }

    // Commands.<Classe>.<o quê> no Localization do mod.
    static #Text(inst, what) {
        if (!inst.Mod) return '';
        const key = 'Mods.' + (inst.Mod.id || inst.Mod.uuid) + '.Commands.' + inst.constructor.name + '.' + what;
        return ModLocalization.TryTranslate(key);
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModCommand)) {
            throw new TypeError('ModCommand.register(Classe): passe a classe, que estende ModCommand');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        CommandLoader.Add(inst);
        Ready.Add(() => Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults()), 'setup');
        return inst;
    }
}

// O UsageException do tModLoader: a mensagem (e a cor) da resposta; sem
// mensagem, responde o Usage do comando.
class UsageException extends Error {
    constructor(msg, color) {
        super(msg || '');
        this.name = 'UsageException';
        this.msg = msg;
        this.color = color;
    }
}
