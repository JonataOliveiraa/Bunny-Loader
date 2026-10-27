using System;
using System.IO;
using System.Linq;
using BunnyLoader.Compiler;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: csbuild <entry.cs> <runtime dir with references.txt> [--force]");
    return 2;
}
string entry = Path.GetFullPath(args[0]);
BuildResult result = ModCompiler.Build(Path.GetDirectoryName(entry)!, Path.GetFileName(entry),
    ModBuild.References(Path.GetFullPath(args[1])), force: args.Skip(2).Contains("--force"));
Console.Write(result.ToReport());
return result.Success ? 0 : 1;
