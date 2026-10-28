using System;
using log4net;
using Terraria;
using Terraria.ModLoader;

// Roda o CompilerBench dentro do mundo, depois do benchcs (quadro 120) e do
// benchjs (quadro 400), na thread do jogo: o jogo para enquanto compila, e a
// conta sai limpa. Loga "compile ..." e avisa no chat.
public sealed class CompileBench : Mod
{
    static ILog log = null!;

    const int WarnFrame = 900, RunFrame = 910;
    static int frames;
    static bool done;

    public override void Load()
    {
        log = Logger;
        On_Player.Update += OnUpdate;
        log.Info("compile: loaded");
    }

    static void OnUpdate(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu) return;
        frames++;
        if (frames == WarnFrame)
            Main.NewText("Medindo o compilador C#: o jogo vai parar por alguns minutos.", 255, 240, 120);
        if (frames != RunFrame) return;
        done = true;
        try { CompilerBench.RunAll(line => log.Info(line), AppContext.BaseDirectory); }
        catch (Exception e) { log.Error($"compile error: {e}"); }
        Main.NewText("Medição do compilador concluída. Configurações > Diagnóstico > Ver relatório > Copiar.", 120, 255, 120);
    }
}
