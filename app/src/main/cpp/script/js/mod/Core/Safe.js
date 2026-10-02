class Safe {
    static #reported = new Set();

    // Um erro do mod num hook de todo quadro vira uma linha de log só.
    static Run(label, fn) {
        try {
            return fn();
        } catch (e) {
            Safe.Report(label, e);
            return undefined;
        }
    }

    // O mesmo aviso, para o try/catch escrito à mão nos hooks de cada entidade:
    // lá o Safe.Run custaria uma closure nova (e um rótulo montado) por chamada.
    // Em nível de erro: é o que abre o painel de erro dentro do jogo (com o
    // bl.log, o erro de um método de mod ficava só no log).
    static Report(label, e) {
        Safe.Once(label, 'error in ' + label + ': ' + e + (e && e.stack ? '\n' + e.stack : ''), true);
    }

    // Uma linha por chave. `error`: em nível de erro (o painel); senão, aviso.
    static Once(key, text, error = false) {
        if (Safe.#reported.has(key)) return;

        Safe.#reported.add(key);
        if (error) bl.error(text);
        else bl.log(text);
    }
}
