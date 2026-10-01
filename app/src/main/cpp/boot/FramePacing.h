#pragma once

namespace bl::runtime {

// O alvo de 60 quadros do jogo vira 61 no Application.targetFrameRate do Unity.
//
// O Unity divide a taxa da tela pelo alvo e arredonda para saber quantas
// atualizacoes cada quadro ocupa. Numa tela de 90 Hz, 90/60 = 1,5 arredonda
// para 2: um quadro a cada duas atualizacoes, 45 fps. Com 61, 90/61 = 1,47
// vira 1 e quem segura os 60 e o passo fixo do XNA. Em 60 Hz (60/61 -> 1) e
// 120 Hz (120/61 -> 2) da o mesmo que 60. O 30 da economia de bateria fica.
//
// A Samsung so solta a tela em 60 Hz para app que ela conta como jogo (o TL Pro
// entra pela marca da Play Store); fora dessa lista, com a suavidade de
// movimento em "Alta", a tela fica em 90 Hz e o Bunny Loader rodava a 45.
void installFramePacing();

// Na thread do jogo (a API do Unity so aceita la): uma vez depois de instalar,
// o jogo refaz a escolha do alvo, agora passando pelo hook.
void tickFramePacing();

} // namespace bl::runtime
