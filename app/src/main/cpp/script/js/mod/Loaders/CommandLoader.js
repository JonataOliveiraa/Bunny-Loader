// Os comandos de chat dos mods, como o CommandLoader do tModLoader. O texto
// digitado passa pelo processador do jogo: o que ele não conhece chega como
// "Say" com a barra. Sozinho (e no servidor), no ProcessIncomingMessage; no
// cliente do multijogador, os de tipo Chat antes de irem para o servidor
// (ChatHelper.SendChatMessageFromClient). O /help do jogo lista os de mod.
class CommandLoader {
    static #byName = new Map();   // nome (minúsculo) -> [ModCommand]
    static #all = [];

    static Add(cmd) {
        const names = [cmd.Command, ...(Array.isArray(cmd.Aliases) ? cmd.Aliases : [])];
        for (const raw of names) {
            const name = CommandLoader.#Normalize(raw);
            if (!name) continue;
            if (!CommandLoader.#byName.has(name)) CommandLoader.#byName.set(name, []);
            CommandLoader.#byName.get(name).push(cmd);
        }
        CommandLoader.#all.push(cmd);
        Hooks.Once('command.chat', () => CommandLoader.#Hook());
    }

    static #Normalize(name) {
        return String(name ?? '').trim().replace(/^\/+/, '').toLowerCase();
    }

    // O CommandType.World vale como Chat sozinho e como Server no servidor.
    static Matches(commandType, callerType) {
        const netMode = Terraria.Main.netMode;
        if (commandType & CommandType.World) {
            if (netMode === 2) commandType |= CommandType.Server;
            else if (netMode === 0) commandType |= CommandType.Chat;
        }
        return (callerType & commandType) !== 0;
    }

    // Quem chamou: o chat do próprio aparelho, ou um jogador no servidor.
    static #ChatCaller() {
        return {
            CommandType: CommandType.Chat,
            get Player() { return Terraria.Main.player[Terraria.Main.myPlayer]; },
            Reply(text, color) { CommandLoader.#Lines(text, (line) => CommandLoader.#NewText(line, color)); },
        };
    }

    static #PlayerCaller(whoAmI) {
        return {
            CommandType: CommandType.Server,
            get Player() { return Terraria.Main.player[whoAmI]; },
            Reply(text, color) {
                const c = color || Color.White;
                CommandLoader.#Lines(text, (line) => {
                    Terraria.Chat.ChatHelper['void SendChatMessageToClient(NetworkText text, Color color, int playerId)'](
                        Terraria.Localization.NetworkText.FromLiteral(line), Color.new(c.R, c.G, c.B, 255), whoAmI);
                });
            },
        };
    }

    static #Lines(text, fn) {
        for (const line of String(text ?? '').split('\n')) fn(line);
    }

    static #NewText(line, color) {
        const c = color || Color.White;
        Terraria.Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](line, c.R, c.G, c.B, false);
    }

    // O comando pelo nome ('mod:nome' escolhe o mod), só dos que valem para
    // quem chamou. undefined: não é comando de mod (o jogo segue); null: é,
    // mas com o erro já respondido.
    static #Find(caller, name) {
        let modName = null;
        if (name.includes(':')) [modName, name] = name.split(':', 2);
        const list = (CommandLoader.#byName.get(name) || []).filter((c) => CommandLoader.Matches(c.Type, caller.CommandType));
        if (!list.length) return undefined;

        const red = Color.new(255, 25, 25);
        if (modName !== null) {
            const found = list.find((c) => c.Mod && (c.Mod.id === modName || c.Mod.uuid === modName));
            if (!found) caller.Reply('O mod ' + modName + ' não tem o comando /' + name + '.', red);
            return found || null;
        }
        if (list.length > 1) {
            caller.Reply('O comando /' + name + ' existe em mais de um mod. Use:', red);
            for (const c of list) caller.Reply('/' + (c.Mod ? c.Mod.id : '?') + ':' + name, Color.LawnGreen);
            return null;
        }
        return list[0];
    }

    // true: era um comando de mod (rodou, ou respondeu o erro).
    static Handle(input, caller) {
        const text = String(input ?? '').trim();
        if (!text.startsWith('/')) return false;

        const space = text.indexOf(' ');
        const name = CommandLoader.#Normalize(space >= 0 ? text.slice(0, space) : text);
        const cmd = CommandLoader.#Find(caller, name);
        if (cmd === undefined) return false;
        if (cmd === null) return true;

        const line = cmd.IsCaseSensitive ? text : text.toLowerCase();
        const args = line.split(/\s+/).slice(1);
        const red = Color.new(255, 25, 25);
        try {
            if (cmd.Action(caller, line, args) === false) caller.Reply(cmd.Usage, ModCommand.ResponseColor);
        } catch (e) {
            if (e instanceof UsageException && e.msg) {
                caller.Reply(e.msg, e.color || red);
            } else {
                if (!(e instanceof UsageException)) bl.log(cmd.constructor.name + '.Action: ' + e + (e && e.stack ? '\n' + e.stack : ''));
                caller.Reply('Uso: ' + cmd.Usage, red);
            }
        }
        return true;
    }

    // "/nome descrição" de cada comando que vale para este tipo de chamada (o
    // GetHelp do tModLoader); o mod na frente quando o nome se repete.
    static GetHelp(callerType) {
        const out = [];
        for (const cmd of CommandLoader.#all) {
            if (!CommandLoader.Matches(cmd.Type, callerType)) continue;
            const name = CommandLoader.#Normalize(cmd.Command);
            const shared = (CommandLoader.#byName.get(name) || []).length > 1;
            out.push({ command: (shared && cmd.Mod ? cmd.Mod.id + ':' : '') + name, description: cmd.Description });
        }
        return out;
    }

    static #Hook() {
        const Chat = Terraria.Chat;

        Chat.ChatCommandProcessor['void ProcessIncomingMessage(ChatMessage message, int clientId)'].hook((original, self, message, clientId) => {
            const netMode = Terraria.Main.netMode;
            const text = message && message.Text;
            if (netMode !== 1 && typeof text === 'string' && text.startsWith('/')) {
                const caller = netMode === 2 ? CommandLoader.#PlayerCaller(clientId) : CommandLoader.#ChatCaller();
                if (Safe.Run('ModCommand', () => CommandLoader.Handle(text, caller))) {
                    message.Consume();
                    return;
                }
            }
            original(self, message, clientId);
        });

        // Cliente do multijogador: os de tipo Chat rodam aqui, sem ir ao servidor.
        Chat.ChatHelper['void SendChatMessageFromClient(ChatMessage message)'].hook((original, message) => {
            const text = message && message.Text;
            if (Terraria.Main.netMode === 1 && typeof text === 'string' && text.startsWith('/') &&
                Safe.Run('ModCommand', () => CommandLoader.Handle(text, CommandLoader.#ChatCaller()))) return;
            original(message);
        });

        // O /help do jogo com os comandos de mod no fim.
        Chat.Commands.HelpCommand['NetworkText ComposeMessage(Dictionary`2 aliases)'].hook((original, aliases) => {
            const text = original(aliases);
            const callerType = Terraria.Main.netMode === 2 ? CommandType.Server : CommandType.Chat;
            const lines = CommandLoader.GetHelp(callerType).map((h) => '/' + h.command + (h.description ? ' ' + h.description : ''));
            if (!lines.length) return text;
            return Terraria.Localization.NetworkText.FromLiteral(text.ToString() + '\n' + lines.join('\n'));
        });
    }
}
