using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace CsSave;

public static class TagChecks
{
    public static void Run()
    {
        var tag = new TagCompound
        {
            ["int"] = 5,
            ["float"] = 1.5f,
            ["bool"] = true,
            ["text"] = "hi",
            ["list"] = new List<int> { 1, 2, 3 },
            ["bytes"] = new byte[] { 1, 2 },
            ["inner"] = new TagCompound { ["x"] = 1 },
        };
        Report.Check("tag values", tag.GetInt("int") == 5 && tag.GetFloat("float") == 1.5f && tag.GetBool("bool") && tag.GetString("text") == "hi" &&
            tag.GetList<int>("list").SequenceEqual(new[] { 1, 2, 3 }) && tag.GetByteArray("bytes").SequenceEqual(new byte[] { 1, 2 }) &&
            tag.GetCompound("inner").GetInt("x") == 1);
        Report.Check("tag numeric conversion", tag.Get<float>("int") == 5f && tag.Get<long>("int") == 5L && tag.Get<int[]>("list").Length == 3);
        Report.Check("tag missing keys", tag.GetInt("nope") == 0 && tag.GetString("nope") == "" && tag.GetList<int>("nope").Count == 0 &&
            tag.GetCompound("nope").Count == 0 && !tag.ContainsKey("nope"));
        Throws<ArgumentException>("tag Add of an existing key", () => tag.Add("int", 6));
        tag["int"] = 7;
        Report.Check("tag indexer replaces", tag.GetInt("int") == 7);
        Throws<InvalidCastException>("tag Get of the wrong type", () => tag.Get<int>("text"));
        Report.Check("tag TryGet of the wrong type", !tag.TryGet("text", out int _) && tag.TryGet("int", out int seven) && seven == 7);
        Throws<ArgumentException>("tag refuses objects", () => tag["object"] = new object());
        var clone = (TagCompound)tag.Clone();
        clone.GetCompound("inner")["x"] = 2;
        Report.Check("tag Clone is deep", tag.GetCompound("inner").GetInt("x") == 1 && clone.GetCompound("inner").GetInt("x") == 2);
        tag.Set("text", null, true);
        Report.Check("tag null removes", !tag.ContainsKey("text"));
        Report.Check("tag ToString is JSON", tag.ToString().Contains("\"int\":7"), tag.ToString());

        foreach (bool compress in new[] { true, false })
        {
            using var stream = new MemoryStream();
            TagIO.ToStream(tag, stream, compress);
            stream.Position = 0;
            TagCompound back = TagIO.FromStream(stream, compress);
            Report.Check($"TagIO round trip ({(compress ? "compressed" : "raw")})",
                back.GetInt("int") == 7 && back.GetFloat("float") == 1.5f && back.GetBool("bool") &&
                back.GetList<int>("list").SequenceEqual(new[] { 1, 2, 3 }) && back.GetByteArray("bytes").Length == 2 &&
                back.GetCompound("inner").GetInt("x") == 1, $"{stream.Length} bytes");
        }
    }

    static void Throws<T>(string name, Action body) where T : Exception
    {
        try
        {
            body();
            Report.Check(name, false, "no exception");
        }
        catch (T e)
        {
            Report.Check(name, true, e.Message);
        }
        catch (Exception e)
        {
            Report.Check(name, false, $"{e.GetType().Name}: {e.Message}");
        }
    }
}
