# Entrada num servidor: sincronização de mods e texturas

A tela de Host (Multijogador > Host > o mundo) tem duas opções do Bunny
Loader, depois da Senha, numa lista que rola (`HostSettingsMenu`). As duas
começam ligadas e valem enquanto o jogo está aberto:

| Opção | Ligada | Desligada |
|---|---|---|
| Sincronização de mods | Quem entra com mods diferentes é recusado e recebe a oferta de sincronizar. | Entra como sempre (com mods diferentes, pode travar ao carregar o mundo). |
| Permitir texturas | Quem entra pode ter pacotes de textura ligados. | Quem tem algum pacote de textura ligado é recusado, com o motivo. |

## A conversa

1. O cliente, antes de pedir o mundo (mensagem 6), se apresenta: os mods
   carregados, na ordem de carga (uid, id, nome, versão), e quantos pacotes de
   textura tem ligados (`ServerSyncLoader`).
2. A apresentação vai numa **mensagem 68**, a do UUID do cliente: o
   `Main.clientUUID` vira `bunnyloader-sync:` + JSON só durante o envio. O
   servidor recusa, com "operação inválida neste estado", qualquer mensagem
   acima de 12 de quem ainda está entrando, exceto 16, 38, 42, 50, 68, 93,
   147, 161 e 164 (conferido no `MessageBuffer.ProcessData` do celular). A 82
   dos pacotes de mod não serve aqui.
3. O host lê a 68 num gancho no `MessageBuffer.ProcessData`, instalado só
   quando ele hospeda, e não a passa adiante: o UUID de verdade do cliente,
   que chega em outra 68, fica intacto.
4. O host decide:
   - texturas proibidas e o cliente com alguma: `NetMessage.BootPlayer` com o
     motivo;
   - sincronização ligada e mods diferentes (conjunto, **ordem** ou versão):
     manda a lista dele (pacote de mod `sync-plan`) e recusa. A ordem conta
     porque é ela que dá os números de item, NPC e projétil.
5. O cliente recebe a lista e chama `bl.__offerServerSync(json)`, que leva ao
   Kotlin (`dev.bunnyloader.game.ServerSync.offer`) a lista, o endereço, a
   porta, a senha, o arquivo do personagem e os pacotes de textura ligados.

## A sincronização (ServerSync.kt)

Cada mod vira uma ação:

- do servidor, instalado na versão dele: entra;
- do servidor, na loja na versão dele: baixa da loja;
- do servidor, nem um nem outro: a tela "Mods do servidor" lista o que
  instalar, e para aí;
- do jogador, que o servidor não tem: fica de fora desta vez.

A tela é do jogo (`ServerSyncMenu`, no lugar da tela de desconexão): ela lê o
andamento em `bl.__serverSyncState()` e responde com `bl.__serverSyncApply()`
ou `bl.__serverSyncCancel()`. Cada mod aparece com a ação à direita (Baixar,
Ligar, Desligar, Falta) e, baixando, com a porcentagem.

Aceitando, ele baixa, grava `files/server_session.json` (os mods do servidor
na ordem dele, mais os pacotes de textura do jogador, e o servidor) e
reinicia pela `RestartActivity`. Na abertura, a `GameActivity` lê e apaga o
arquivo (`takeSession`): os mods vão ao núcleo no lugar da seleção do
jogador, e os campos `join*` da `NativeConfig` fazem o título escolher o
personagem e conectar (`joinServer`, em `boot/QuickStart.cpp`, sem depender
do Início rápido). Vale uma abertura só: a seguinte é a do jogador.

## O que ainda não tem

- Mod fora da loja não vem do host pela rede: o jogador instala à mão.
- As opções da tela de Host não ficam salvas entre aberturas do jogo.
- Sem sincronização, mods diferentes ainda podem travar a entrada.
