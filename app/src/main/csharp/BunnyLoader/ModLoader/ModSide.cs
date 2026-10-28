namespace Terraria.ModLoader;

// Onde o conteudo vale, como no tModLoader. No celular nao ha servidor
// dedicado: Server nunca carrega, e os outros sempre.
public enum ModSide
{
    Both,
    Client,
    Server,
    NoSync,
}
