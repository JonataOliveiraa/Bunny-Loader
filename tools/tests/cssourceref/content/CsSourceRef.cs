using CsModTarget;
using Terraria.ModLoader;

namespace CsSourceRef;

// So fontes, e usa um tipo da dependencia (csmodtarget): o celular compila
// com o .dll dela como referencia.
public sealed class CsSourceRefMod : Mod
{
    public override void Load()
    {
        bool ok = TargetApi.Loaded && TargetApi.Answer == 42 && ModLoader.GetMod("test-csmodtarget").Call("Add", 1, 1) is 2;
        Logger.Info($"cssourceref dependency types and Call: {(ok ? "ok" : "FAIL")}");
    }
}
