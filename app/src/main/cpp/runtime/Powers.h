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
    Count
};

/** Muda o nivel de um poder. THREAD-SAFE: vem da UI thread, pelo menu. */
void setPower(int id, int level);

/**
 * A parte que precisa da thread do jogo: instala os hooks na primeira vez que
 * um poder liga (quem nunca abre o menu nao paga nada) e segura o relogio
 * parado. Chamada do hook de Main.DoUpdate, todo quadro.
 */
void tickPowers();

} // namespace bl::runtime
