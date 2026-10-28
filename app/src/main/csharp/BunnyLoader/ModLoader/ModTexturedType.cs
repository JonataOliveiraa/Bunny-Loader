namespace Terraria.ModLoader;

// Um ModType com uma textura so (ModBuff, ModDust... quando vierem).
public abstract class ModTexturedType : ModType
{
    // Sem extensao, relativo a pasta da entrada do mod: o namespace + nome,
    // como no tModLoader ("MeuMod/Buffs/Veneno").
    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');
}
