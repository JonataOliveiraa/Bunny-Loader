using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BunnyLoader.Compiler;

public static class ModBuild
{
    public const string OutputFolder = "bin";
    public const string ReferencesFile = "references.txt";
    const int StampFormat = 1;

    static readonly string[] Ignored = { "bin", "obj" };

    public static string AssemblyName(string entryFile) => Path.GetFileNameWithoutExtension(entryFile);

    public static string DllPath(string projectDir, string entryFile) =>
        Path.Combine(projectDir, OutputFolder, AssemblyName(entryFile) + ".dll");

    public static string StampPath(string projectDir, string entryFile) =>
        Path.Combine(projectDir, OutputFolder, AssemblyName(entryFile) + ".build.json");

    public static bool IsSourceEntry(string entry) => entry.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    public static List<string> Sources(string projectDir) => Files(projectDir, "*.cs");

    public static List<string> Libraries(string projectDir) => Files(projectDir, "*.dll");

    static List<string> Files(string projectDir, string pattern)
    {
        var root = Path.GetFullPath(projectDir);
        var found = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (Array.Exists(relative.Split('/')[..^1], part => Array.Exists(Ignored, i => i.Equals(part, StringComparison.OrdinalIgnoreCase))))
                continue;
            found.Add(relative);
        }
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    public static List<string> References(string runtimeDir)
    {
        var list = new List<string>();
        foreach (string line in File.ReadAllLines(Path.Combine(runtimeDir, ReferencesFile)))
        {
            string name = line.Trim();
            if (name.Length > 0 && !name.StartsWith('#')) list.Add(Path.Combine(runtimeDir, name));
        }
        return list;
    }

    public static string ApiVersion(IEnumerable<string> references)
    {
        foreach (string path in references)
        {
            if (Path.GetFileName(path).Equals("BunnyLoader.dll", StringComparison.OrdinalIgnoreCase))
                return System.Reflection.AssemblyName.GetAssemblyName(path).Version?.ToString() ?? "0.0.0.0";
        }
        throw new FileNotFoundException("BunnyLoader.dll is not among the references");
    }

    public static string ApiVersion(IEnumerable<string> references, IReadOnlyList<string> modReferences)
    {
        var api = new StringBuilder(ApiVersion(references));
        var sorted = new List<string>(modReferences);
        sorted.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
        foreach (string path in sorted)
            api.Append('+').Append(Path.GetFileNameWithoutExtension(path)).Append('@').Append(Sha256(File.ReadAllBytes(path))[..16]);
        return api.ToString();
    }

    public static string Fingerprint(string projectDir, IReadOnlyList<string> sources, IReadOnlyList<string> libraries, string api)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Text(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\n"));
        Text($"bl-build-{StampFormat}");
        Text(api);
        foreach (var (kind, list) in new[] { ("source", sources), ("library", libraries) })
        {
            foreach (string relative in list)
            {
                byte[] data = File.ReadAllBytes(Path.Combine(projectDir, relative));
                Text($"{kind} {relative} {data.Length}");
                hash.AppendData(data);
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public sealed record Stamp(string Api, string Compiler, string Fingerprint, string Dll);

    public static Stamp? ReadStamp(string projectDir, string entryFile)
    {
        string path = StampPath(projectDir, entryFile);
        if (!File.Exists(path)) return null;
        string text = File.ReadAllText(path);
        string? Field(string key)
        {
            Match m = Regex.Match(text, "\"" + key + "\"\\s*:\\s*\"([^\"\\\\]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
        Match format = Regex.Match(text, "\"format\"\\s*:\\s*(\\d+)");
        if (!format.Success || format.Groups[1].Value != StampFormat.ToString()) return null;
        string? api = Field("api"), compiler = Field("compiler"), fingerprint = Field("fingerprint"), dll = Field("dll");
        return api == null || fingerprint == null || dll == null ? null : new Stamp(api, compiler ?? "", fingerprint, dll);
    }

    public static void WriteStamp(string projectDir, string entryFile, Stamp stamp)
    {
        string text =
            "{\n" +
            $"  \"format\": {StampFormat},\n" +
            $"  \"api\": \"{stamp.Api}\",\n" +
            $"  \"compiler\": \"{stamp.Compiler}\",\n" +
            $"  \"fingerprint\": \"{stamp.Fingerprint}\",\n" +
            $"  \"dll\": \"{stamp.Dll}\"\n" +
            "}\n";
        WriteAtomic(StampPath(projectDir, entryFile), Encoding.UTF8.GetBytes(text));
    }

    public static bool IsCurrent(string projectDir, string entryFile, string api, out string reason)
    {
        string dll = DllPath(projectDir, entryFile);
        if (!File.Exists(dll)) { reason = "not compiled yet"; return false; }
        Stamp? stamp = ReadStamp(projectDir, entryFile);
        if (stamp == null) { reason = "no build stamp"; return false; }
        if (stamp.Api != api) { reason = $"compiled for API {stamp.Api}, this is {api}"; return false; }
        string now = Fingerprint(projectDir, Sources(projectDir), Libraries(projectDir), api);
        if (stamp.Fingerprint != now) { reason = "the sources changed"; return false; }
        if (stamp.Dll != Sha256(File.ReadAllBytes(dll))) { reason = "the .dll does not match its stamp"; return false; }
        reason = "up to date";
        return true;
    }

    internal static void WriteAtomic(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, data);
        File.Move(temp, path, overwrite: true);
    }
}
