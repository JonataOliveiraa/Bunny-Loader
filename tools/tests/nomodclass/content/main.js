// Um mod sem a classe Mod: o topo do arquivo roda, mas a carga falha com a
// mensagem do carregador, e os outros mods seguem. Quem confere é o
// tools/tests/autoload (ModLoader.HasMod dá false).
globalThis.__nomodclassTop = true;
bl.log('nomodclass FIM: topo rodou (a carga tem de falhar: sem export default class ... extends Mod)');
