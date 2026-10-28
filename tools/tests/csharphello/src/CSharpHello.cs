using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.ModLoader;

public sealed class CSharpHello : Mod
{
    static ILog log = null!;

    const int GcRounds = 60;

    static int hookCalls, minisharkCalls;

    public override void Load()
    {
        log = Logger;
        On_Item.SetDefaults += SetDefaults;

        Item item = Item.New();
        item.SetDefaults(ItemID.Minishark, default);
        bool ok = item.useTime == 4 && item.useAnimation == 4 && item.shootSpeed == 10f;
        log.Info($"csharphello load useTime={item.useTime} useAnimation={item.useAnimation} shootSpeed={item.shootSpeed} damage={item.damage} {(ok ? "ok" : "FAIL")}");

        new Thread(GcStress) { IsBackground = true, Name = "csharphello-gc" }.Start();
    }

    static void SetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, ItemVariant variant)
    {
        orig(self, type, variant);
        Interlocked.Increment(ref hookCalls);
        if (type != ItemID.Minishark) return;
        self.useTime = 4;
        self.useAnimation = 4;
        self.shootSpeed = 10f;
        if (Interlocked.Increment(ref minisharkCalls) <= 3)
            log.Info($"csharphello hook Minishark on managed thread {Environment.CurrentManagedThreadId}");
    }

    // Coletas do SGen enquanto o jogo roda (e o Boehm dele coleta tambem): a
    // thread do jogo entra e sai do C# pelo hook no meio delas.
    static void GcStress()
    {
        var keep = new List<byte[]>();
        for (int round = 1; round <= GcRounds; round++)
        {
            for (int i = 0; i < 2000; i++) keep.Add(new byte[512]);
            if (keep.Count > 20000) keep.RemoveRange(0, 10000);
            GC.Collect();
            if (round % 10 == 0)
                log.Info($"csharphello gc round {round} {Collections()} hooks={hookCalls} minishark={minisharkCalls}");
            Thread.Sleep(1000);
        }
        log.Info($"csharphello FIM hooks={hookCalls} minishark={minisharkCalls} {Collections()}");
    }

    static string Collections() =>
        $"gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} heap={GC.GetTotalMemory(false) / 1024}KB";
}
