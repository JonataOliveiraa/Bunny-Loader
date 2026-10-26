namespace BunnyLoader;

// A classe de entrada de um mod C#: o carregador instancia cada classe nao
// abstrata que a estende e chama o Load.
public abstract class Mod
{
    public string Uid { get; internal set; } = "";

    public virtual void Load() { }
}
