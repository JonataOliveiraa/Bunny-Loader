namespace Terraria;

public readonly partial struct Player
{
    public bool HasBuff(int type) => FindBuffIndex(type) != -1;
}
