#pragma once

namespace bl::runtime {

/**
 * Superpoderes do menu: cada um e um nivel (0 = desligado).
 *
 * Os numeros sao o contrato com o CheatBridge.java (a ordem de POWER_NAME la):
 * mudar um aqui sem mudar la liga o poder errado.
 *
 * Quase todos vivem num hook so, depois do Player.ResetEffects. E ali que o
 * jogo zera, todo quadro, os mesmos campos que as pocoes e os acessorios mexem
 * (moveSpeed, dano por classe, pulo, sentidos); mexendo logo depois, o poder
 * soma com o equipamento e desligar desfaz sozinho no quadro seguinte. Conferido
 * na disassembly do ResetEffects de 1.4.5.6.4: cada campo usado aqui e
 * reescrito por ele.
 *
 * Os de mundo (chuva, vento) sao estaticos do Main, escritos a cada quadro
 * antes do DoUpdate; o voo e o raio-X tem hook proprio.
 *
 * Chuva, vento e taxa de inimigos sao as barras da coluna do X: 0 e "o jogo
 * decide", e 1..101 e a posicao da barra mais um (kSliderLevels).
 */
enum class Power : int {
    Damage = 0,      // x2 / x5 / x10 no dano de toda classe
    Speed = 1,       // x2 / x3 na corrida
    Jump = 2,        // pulo x2 / x3, sem dano de queda
    TimeStop = 3,    // NPCs, projeteis inimigos e relogio congelados
    God = 4,         // modo deus da Jornada + vida e folego cheios
    Mana = 5,        // mana cheia e custo zero
    InfiniteJump = 6,// pulo de nuvem que nunca acaba
    FastMining = 7,  // ferramentas 4x mais rapidas
    Vision = 8,      // cacador, espeleologo, perigo e visao noturna
    Fly = 9,         // voa e atravessa parede; normal / rapido
    XRay = 10,       // a tela inteira iluminada
    Minions = 11,    // lacaios e sentinelas sem limite
    Rain = 12,       // BARRA: 0 sem chuva .. 100 tempestade (Main.maxRaining)
    Wind = 13,       // BARRA: 0 ventania a esquerda, 50 calmo, 100 a direita
    Bestiary = 14,   // ACAO: desbloqueia o bestiario do mundo e volta a 0
    NoSpawns = 15,   // BARRA: a taxa de inimigos da Jornada; 0 nenhum, 50 x1, 100 x10
    MapTeleport = 16,// segurar 1 s parado no mapa grande teleporta para la
    ClearInventory = 17, // ACAO: esvazia a mochila (fica favorito, moeda, municao)
    RevealMap = 18,  // ACAO: revela o mapa inteiro, em partes, quadro a quadro
    Hardmode = 19,   // COMANDO: 1 liga (o evento do jogo), 2 desliga
    Difficulty = 20, // COMANDO: 1..4 = Classico, Expert, Mestre, Jornada
    FastRespawn = 21,// morto, volta no quadro seguinte (sem a contagem)
    ClearEnemies = 22, // ACAO: some com os inimigos que ja existem
    StopInfection = 23,// COMANDO: 1 para o alastramento (corrupcao, carmim, sagrado), 2 solta
    StopEvents = 24,   // ACAO: acaba com os eventos em andamento
    // ACAO: comeca o evento. Os de noite (luas) viram a noite antes, e o
    // eclipse vira o dia.
    BloodMoon = 25,
    Eclipse = 26,
    SlimeRain = 27,
    GoblinArmy = 28,
    FrostLegion = 29,
    Pirates = 30,
    Martians = 31,
    PumpkinMoon = 32,
    FrostMoon = 33,
    Party = 34,
    Sandstorm = 35,
    Count
};

/** Muda o nivel de um poder. THREAD-SAFE: vem da UI thread, pelo menu. */
void setPower(int id, int level);

/**
 * Pedido de um cliente (ver NetRequests): so aceita os poderes de MUNDO
 * (tempo parado, chuva, vento, sem inimigos, hardmode, dificuldade, eventos,
 * infeccao). Os de jogador sao de cada um e nao vem pela rede. false = recusado.
 */
bool setWorldPowerFromNet(int id, int level);

/**
 * Servidor: manda ao cliente `client` todas as partes do mundo que ele ainda
 * nao tem (pedido "reveal" do Revelar mapa dele), algumas por quadro. Sem
 * elas o cliente revela ar: so tem os tiles por onde ja andou.
 */
void revealMapForClient(int client);

/** Os niveis de uma barra: 0 desligada, 1..kSliderLevels = posicao 0..100. */
constexpr int kSliderLevels = 101;

/**
 * Hora do dia. 0..3 sao os botoes da Jornada (amanhecer, meio-dia, anoitecer,
 * meia-noite); kClockRequest + minuto (0..1439) e a hora do relogio, a da
 * barra. O mesmo numero vai ao servidor ("time <n>"). THREAD-SAFE; roda no
 * proximo quadro, no mundo.
 */
constexpr int kClockRequest = 100;
void setTimeOfDay(int request);

/**
 * O estado do mundo aberto, para o menu mostrar: hardmode (0/1) e modo de
 * jogo (0 Classico, 1 Expert, 2 Mestre, 3 Jornada). -1 fora do mundo.
 * THREAD-SAFE: copia feita pela thread do jogo a cada quadro.
 */
int worldHardmode();
int worldGameMode();
/** O alastramento parado (o poder da Jornada, salvo no mundo): 0/1, -1 fora do mundo. */
int worldInfectionStopped();

/**
 * O clima do mundo aberto, para as barras abrirem onde o mundo esta: a hora
 * no relogio (minuto 0..1439), a chuva (0..100) e o vento (0..100, 50 calmo).
 * -1 fora do mundo. THREAD-SAFE, como os de cima.
 */
int worldClockMinute();
int worldRainPosition();
int worldWindPosition();

/**
 * A parte que precisa da thread do jogo: instala os hooks na primeira vez que
 * um poder liga (quem nunca abre o menu nao paga nada), segura o relogio
 * parado e o clima, e executa as acoes. Chamada do hook de Main.DoUpdate, todo
 * quadro, ANTES do original.
 */
void tickPowers();

} // namespace bl::runtime
