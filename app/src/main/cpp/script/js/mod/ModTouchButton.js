// Um botão de toque de mod. O jogador o põe nos controles pelo editor do
// próprio jogo (o mesmo de Pular, Curar e Zoom), e o mod o lê como o
// ModKeybind do tModLoader — de preferência no ModPlayer.ProcessTriggers:
//
//   if (MySystem.Dash.JustPressed) ...
//
// Criado pelo TouchButtonLoader.RegisterButton (ou KeybindLoader.RegisterKeybind).
class ModTouchButton {
    #entry;

    constructor(entry) {
        this.#entry = entry;
    }

    get Mod() { return this.#entry.mod; }
    get Name() { return this.#entry.name; }

    // 'ExampleMod/RandomBuff': o nome que o layout salvo guarda.
    get FullName() { return this.#entry.key; }

    get DisplayName() {
        const entry = this.#entry;
        return { Key: entry.textKey, get Value() { return TouchButtonLoader.Label(entry); }, toString() { return this.Value; } };
    }

    // Segurando agora.
    get Current() { return TouchButtonLoader.State(this.#entry).current; }

    // Tocou nesta atualização (uma vez por toque).
    get JustPressed() { return TouchButtonLoader.State(this.#entry).justPressed; }

    // Soltou nesta atualização.
    get JustReleased() { return TouchButtonLoader.State(this.#entry).justReleased; }

    // Estava apertado na atualização anterior.
    get Old() { return TouchButtonLoader.State(this.#entry).old; }

    // O Current, mas falso com o chat, uma conversa ou uma placa abertos.
    get RetroCurrent() {
        const Main = Terraria.Main;
        const player = Main.player[Main.myPlayer];
        if (Main.drawingPlayerChat || player.talkNPC !== -1 || player.sign !== -1) return false;
        return this.Current;
    }

    // Se o botão está no layout de controles em uso.
    get IsBound() { return TouchButtonLoader.IsBound(this.#entry); }

    // No tModLoader, as teclas ligadas. Aqui não há tecla: o próprio botão,
    // pelo nome, quando ele está no layout; senão, nada.
    GetAssignedKeys() {
        return this.IsBound ? [TouchButtonLoader.Label(this.#entry)] : [];
    }

    toString() { return 'ModTouchButton(' + this.#entry.key + ')'; }
}
