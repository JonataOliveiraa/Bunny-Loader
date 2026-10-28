namespace ReLogic.Content;

// O do jogo, escrito a mao (o ProxyGen nao gera por cima). Nos pedidos de
// mod, os dois primeiros sao iguais: a textura carrega na primeira leitura do
// Value, na thread do jogo.
public enum AssetRequestMode
{
    DoNotLoad,
    AsyncLoad,
    ImmediateLoad,
}
