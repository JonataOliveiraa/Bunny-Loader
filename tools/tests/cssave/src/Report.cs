using log4net;
using Terraria.ModLoader;

namespace CsSave;

public static class Report
{
    static ILog Log => ModContent.GetInstance<CsSaveMod>().Logger;

    public static void Check(string name, bool ok, string detail = "") => Log.Info($"cssave {name}: {(ok ? "ok" : "FAIL")} {detail}");

    public static void Info(string text) => Log.Info($"cssave {text}");
}
