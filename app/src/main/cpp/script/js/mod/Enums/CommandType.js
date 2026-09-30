// Onde um ModCommand roda, como o CommandType do tModLoader (flags).
// Chat: no chat, no aparelho de quem digitou (sozinho ou no multijogador).
// Server: no servidor, pedido pelo chat de um jogador. World: no chat sozinho
// e no servidor no multijogador (o que mexe no mundo). Console: o console do
// servidor dedicado, que o celular não tem.
const CommandType = Object.freeze({ Chat: 1, Server: 2, Console: 4, World: 8 });
