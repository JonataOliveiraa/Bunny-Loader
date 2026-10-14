// Um fragment shader de efeito de tela (GLSL ES 3.00, ou no estilo 1.00 com
// gl_FragColor/texture2D). O núcleo põe na frente o preludio: vUV, fragColor,
// os uniforms padrão (uScreen, uCenter, uRadius, uIntensity, uColor, uTime,
// uAge, uProgress, uAspect, uResolution) e blScreen(uv); `#include <bl/...>`
// traz a biblioteca (screen, math, noise, color, shapes). Compila na thread
// de render no primeiro desenho; o erro vai para o painel, com a linha.
class ScreenShader {
    static #files = new Set();   // os que vieram de arquivo (recarregáveis)

    #id;
    #source;
    Name;
    Path = null;   // o arquivo, absoluto (FromFile), ou null

    constructor(name, source) {
        if (typeof source !== 'string' || !source.includes('main')) {
            throw new TypeError('ScreenGraphics.Shader: ' + name + ' has no GLSL source with a main()');
        }

        this.Name = String(name);
        this.#source = source;
        this.#id = bl.screen.shader(this.Name, source);
    }

    get Id() {
        return this.#id;
    }

    // 'pending' (ainda não compilou), 'ready' ou 'failed'.
    get Status() {
        return bl.screen.shaderStatus(this.#id).state;
    }

    get Error() {
        return bl.screen.shaderStatus(this.#id).error;
    }

    static FromSource(source, name = 'shader') {
        return new ScreenShader(name, source);
    }

    // Caminho relativo à pasta do mod de quem chama, como o bl.loadTexture.
    static FromFile(path) {
        const file = /\.(frag|glsl|fs)$/i.test(path) ? path : path + '.frag';
        if (!bl.file.exists(file)) throw new Error('ScreenGraphics.Shader.FromFile: ' + file + ' not found');

        const shader = new ScreenShader(file, String(bl.file.read(file)));
        shader.Path = bl.path.resolve(file);
        ScreenShader.#files.add(shader);
        return shader;
    }

    // Lê o arquivo de novo e, se mudou, recompila: os efeitos que usam este
    // shader passam ao novo no próximo quadro (com erro, param de desenhar e
    // o erro vai para o painel). true se mudou.
    Reload() {
        if (!this.Path) return false;
        const source = bl.file.read(this.Path);
        if (typeof source !== 'string' || source === this.#source) return false;

        this.#source = source;
        this.#id = bl.screen.shader(this.Name, source);
        return true;
    }

    // Os shaders de arquivo (o /fx reload e o ScreenEffects.HotReload).
    static get Files() {
        return [...ScreenShader.#files];
    }
}
