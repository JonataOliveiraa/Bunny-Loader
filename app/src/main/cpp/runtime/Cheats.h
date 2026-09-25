#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl::runtime {

// Menu de cheats (acoes nativas). Por ora: dar um item ao jogador.
//
// A UI (botao) vive no launcher e escreve um arquivo de comando; a lib le esse
// arquivo a cada frame (hook em Main.DoUpdate) e executa a acao na thread do
// jogo. Resolve as refs necessarias na primeira chamada.
void installCheats();

// Da `stack` unidades de `type` ao jogador local (spawna no mundo, na posicao
// dele). No-op fora do mundo (sem jogador). Deve ser chamada na thread do jogo.
//
// `stack` importa: municao a 1 unidade e inutil — a Minishark nao atira com uma
// bala so. O menu pede 999 para itens empilhaveis.
//
// `player` e o indice em Main.player (-1 = o local). No servidor, e para quem
// pediu pela rede; no cliente, vira pedido ao servidor (ver NetRequests).
void giveItem(int type, int stack, int player = -1);

// Pede pra dar `type` ao jogador. THREAD-SAFE: pode ser chamada de qualquer
// thread (ex.: a UI thread, no onClick do botao). Apenas registra o pedido; o
// hook de Main.DoUpdate o executa no proximo frame, na thread do jogo (onde as
// chamadas il2cpp sao validas).
void requestGive(int type, int stack);

/**
 * Invoca o NPC `type` perto do jogador (chefe, monstro ou morador).
 *
 * Mesma mecânica do item: `requestSpawn` pode vir de qualquer thread e o hook
 * de DoUpdate executa no próximo quadro, na thread do jogo.
 */
void spawnNpc(int type, int player = -1);

/**
 * Nomes de TODO item e TODO NPC, vindos da Localization do jogo.
 *
 * Sao ~6800 chamadas a Lang.GetItemNameValue/GetNPCNameValue, e elas so podem
 * acontecer na thread do jogo. Feitas de uma vez, seriam um engasgo visivel no
 * primeiro quadro; entao saem em fatias, algumas centenas por quadro, a partir
 * do DoUpdate. Ate ficarem prontas, `namesReady()` e false e o menu mostra que
 * esta carregando.
 *
 * UTF-16 porque e o que o C# guarda e o que o Java quer: converter para UTF-8
 * no meio so perderia tempo e acentos.
 */
bool namesReady();
const std::vector<std::u16string>& itemNames();
const std::vector<std::u16string>& npcNames();

/**
 * Quantos quadros tem a tira de cada NPC (Main.npcFrameCount).
 *
 * O PNG de um NPC e uma tira vertical de quadros; sem isto a lista mostra o
 * bicho repetido varias vezes espremido. So o jogo sabe o numero — ele preenche
 * esse array no Initialize.
 */
const std::vector<int>& npcFrames();
void requestSpawn(int type, int count = 1);
/** O buff `type` no jogador local por `seconds`. THREAD-SAFE, como o requestGive. */
void requestBuff(int type, int seconds);

/**
 * Secao do menu de cada item, decidida pelo PROPRIO jogo.
 *
 * Vem dos campos que o SetDefaults preenche (`melee`, `magic`, `ranged`,
 * `summon`, `pick`, `accessory`...), lidos nos itens-amostra que o jogo monta no
 * boot (ContentSamples.ItemsByType). Lista escolhida a mao deixava de fora
 * quase tudo: eram 15 magias num jogo que tem centenas.
 *
 * Os numeros sao o contrato com o CheatBridge.java (constantes CL_*): mudar um
 * aqui sem mudar la troca a secao dos itens.
 */
enum ItemClass : uint8_t {
    kClassOther = 0,
    kClassMelee = 1,
    kClassRanged = 2,
    kClassMagic = 3,
    kClassSummon = 4,
    kClassAmmo = 5,
    kClassTool = 6,
    kClassAccessory = 7,
    kClassArmor = 8,
    kClassPotion = 9,
    kClassBlock = 10,
    kClassNone = 255,   // id sem amostra (vazio ou removido do jogo)
};

/**
 * O jogador esta dentro de um mundo (Main.gameMenu falso)?
 *
 * Lido uma vez por quadro no DoUpdate e guardado: a UI thread pergunta a toda
 * hora para esconder o botao do menu na tela de titulo, e ela nao pode chamar
 * o il2cpp.
 */
bool inWorld();

// Prontas junto com os nomes (`namesReady`). Vazias se a leitura falhou.
const std::vector<uint8_t>& itemClasses();
// A subcategoria dentro da secao (0 = Outros); contrato com SUB_NAMES no Java.
const std::vector<uint8_t>& itemSubClasses();
// Teto de pilha que o menu entrega: 1 para arma, ferramenta, acessorio e
// armadura; o maxStack do jogo para o resto. NAO e o Item.maxStack puro, que
// nesta versao vale 9999 ate para espada.
const std::vector<int32_t>& itemMaxStacks();

} // namespace bl::runtime
