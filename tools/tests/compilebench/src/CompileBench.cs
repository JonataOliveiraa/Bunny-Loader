using System;
using BunnyLoader;
using Terraria;

// Roda o CompilerBench dentro do mundo, depois do benchcs (quadro 120) e do
// benchjs (quadro 400), na thread do jogo: o jogo para enquanto compila, e a
// conta sai limpa. Loga "compile ..." e avisa no chat.
public sealed class CompileBench : Mod
{
    const int WarnFrame = 900, RunFrame = 910;
    static int frames;
    static bool done;

    public override void Load()
    {
        On_Player.Update += OnUpdate;
        Log.Info("compile: loaded");
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
        try { CompilerBench.RunAll(line => Log.Info(line), AppContext.BaseDirectory); }
        catch (Exception e) { Log.Error($"compile error: {e}"); }
        Main.NewText("Medição do compilador concluída. Configurações > Diagnóstico > Ver relatório > Copiar.", 120, 255, 120);
    }
}
