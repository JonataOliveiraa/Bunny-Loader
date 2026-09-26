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
    Rain = 12,       // garoa / chuva / tempestade
    Wind = 13,       // calmo / brisa / ventania
    Bestiary = 14,   // ACAO: desbloqueia o bestiario do mundo e volta a 0
    NoSpawns = 15,   // sem spawn natural / e ainda sem inimigo nenhum por perto
    MapTeleport = 16,// segurar 2,5 s parado no mapa grande teleporta para la
    ClearInventory = 17, // ACAO: esvazia a mochila (fica favorito, moeda, municao)
    RevealMap = 18,  // ACAO: revela o mapa inteiro, em partes, quadro a quadro
    Hardmode = 19,   // COMANDO: 1 liga (o evento do jogo), 2 desliga
    Difficulty = 20, // COMANDO: 1..4 = Classico, Expert, Mestre, Jornada
    FastRespawn = 21,// morto, volta no quadro seguinte (sem a contagem)
    Count
};

/** Muda o nivel de um poder. THREAD-SAFE: vem da UI thread, pelo menu. */
void setPower(int id, int level);

/**
 * Pedido de um cliente (ver NetRequests): so aceita os poderes de MUNDO
 * (tempo parado, chuva, vento, sem inimigos, hardmode, dificuldade). Os de
 * jogador sao de cada um e nao vem pela rede. false = recusado.
 */
bool setWorldPowerFromNet(int id, int level);

/**
 * Hora do dia, como os botoes da Jornada: 0 amanhecer, 1 meio-dia,
 * 2 anoitecer, 3 meia-noite. THREAD-SAFE; roda no proximo quadro, no mundo.
 */
void setTimeOfDay(int which);

/**
 * O estado do mundo aberto, para o menu mostrar: hardmode (0/1) e modo de
 * jogo (0 Classico, 1 Expert, 2 Mestre, 3 Jornada). -1 fora do mundo.
 * THREAD-SAFE: copia feita pela thread do jogo a cada quadro.
 */
int worldHardmode();
int worldGameMode();

/**
 * A parte que precisa da thread do jogo: instala os hooks na primeira vez que
 * um poder liga (quem nunca abre o menu nao paga nada), segura o relogio
 * parado e o clima, e executa as acoes. Chamada do hook de Main.DoUpdate, todo
 * quadro, ANTES do original.
 */
void tickPowers();

} // namespace bl::runtime
