using System;
using System.Collections.Generic;

namespace BunnyLoader.Interop;

// Os structs espelhados pelo gerador tem o layout do dump em que foram
// gerados. Uma versao do jogo que muda um deles faria cada leitura cair no
// byte errado: na subida, o tamanho de cada um e conferido contra o jogo.
public static unsafe class Layouts
{
    public static List<string> Verify()
    {
        var bad = new List<string>();
        foreach (var (cls, size) in BunnyLoader.Generated.StructLayouts.All)
        {
            int real;
            try { real = Host.Native()->ClassValueSize(cls.Handle); }
            catch (TypeLoadException) { continue; }
            if (real != size) bad.Add($"{cls.FullName}: {size} bytes here, {real} in the game");
        }
        return bad;
    }
}
