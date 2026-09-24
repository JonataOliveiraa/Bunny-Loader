#pragma once
#include <string>

namespace bl::runtime {

/**
 * Multijogador: o que so o servidor pode fazer, pedido pelo cliente.
 *
 * No cliente, NPC.NewNPC cria o NPC so no Main.npc dele (ninguem mais ve) e o
 * item pedido pelo menu nao chega. Quem cria NPC e item no mundo e o servidor.
 * O pedido viaja como mensagem de chat ("/bunny npc 697"), que o jogo ja manda
 * do cliente ao servidor com o indice de quem mandou; o servidor com Bunny
 * Loader intercepta em ChatCommandProcessor.ProcessIncomingMessage, antes de a
 * mensagem virar chat, e executa no proximo quadro. Servidor SEM Bunny Loader
 * mostra o texto no chat: legivel, e so.
 *
 * Pedidos: "npc <tipo>", "item <tipo> <pilha>", "power <id> <nivel>" (so os
 * poderes de mundo) e "time <0..3>".
 *
 * Main.netMode no celular e um campo de BITS (Main.get_NetHost le o bit 1):
 * 0 sozinho, 1 cliente, 2 servidor dedicado, 3 quem hospeda e joga.
 */
void installNetRequests();

int netMode();
inline bool isNetClientOnly() { return netMode() == 1; }
inline bool isNetHost() { return (netMode() & 2) != 0; }

/** NetMessage.SendData(msgType, -1, -1, null, number): do servidor para todos. */
void sendData(int msgType, int number);

/**
 * Manda "/bunny <command>" ao servidor. So faz sentido no cliente; devolve
 * false se as refs nao resolveram ou a chamada lancou.
 */
bool sendToServer(const std::string& command);

/** Executa os pedidos recebidos. Chamado do DoUpdate, na thread do jogo. */
void tickNetRequests();

} // namespace bl::runtime
