using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ProxyGen;

static class BinaryCompat
{
    public const string Prefix = "__BinaryCompat_";

    const int NameColumn = 8;

    public static int Patch(string dll)
    {
        byte[] image = File.ReadAllBytes(dll);
        var renames = new List<(int At, int From, int To)>();

        using (var pe = new PEReader(new MemoryStream(image, writable: false)))
        {
            MetadataReader md = pe.GetMetadataReader();
            int table = pe.PEHeaders.MetadataStartOffset + md.GetTableMetadataOffset(TableIndex.MethodDef);
            int rowSize = md.GetTableRowSize(TableIndex.MethodDef);

            foreach (TypeDefinitionHandle typeHandle in md.TypeDefinitions)
            {
                var names = new Dictionary<string, StringHandle>(StringComparer.Ordinal);
                var compat = new List<(MethodDefinitionHandle Method, StringHandle Name, string Target)>();

                foreach (MethodDefinitionHandle handle in md.GetTypeDefinition(typeHandle).GetMethods())
                {
                    StringHandle name = md.GetMethodDefinition(handle).Name;
                    string text = md.GetString(name);
                    if (text.StartsWith(Prefix, StringComparison.Ordinal)) compat.Add((handle, name, text.Substring(Prefix.Length)));
                    else names.TryAdd(text, name);
                }

                foreach (var (method, name, target) in compat)
                {
                    if (!names.TryGetValue(target, out StringHandle targetName))
                    {
                        Console.Error.WriteLine($"ProxyGen: {Prefix}{target} has no {target} beside it in {md.GetString(md.GetTypeDefinition(typeHandle).Name)}");
                        return 4;
                    }
                    int at = table + (MetadataTokens.GetRowNumber(method) - 1) * rowSize + NameColumn;
                    renames.Add((at, MetadataTokens.GetHeapOffset(name), MetadataTokens.GetHeapOffset(targetName)));
                }
            }
        }

        if (renames.Count == 0) return 0;

        foreach (var (at, from, to) in renames)
        {
            Span<byte> cell = image.AsSpan(at);
            if (BinaryPrimitives.ReadInt32LittleEndian(cell) == from)
            {
                BinaryPrimitives.WriteInt32LittleEndian(cell, to);
            }
            else if (BinaryPrimitives.ReadUInt16LittleEndian(cell) == from && to <= ushort.MaxValue)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(cell, (ushort)to);
            }
            else
            {
                Console.Error.WriteLine($"ProxyGen: unexpected MethodDef name cell at 0x{at:X} in {Path.GetFileName(dll)}");
                return 5;
            }
        }

        File.WriteAllBytes(dll, image);
        Console.WriteLine($"ProxyGen: {renames.Count} getter(s) kept for mods built against older versions in {Path.GetFileName(dll)}");
        return 0;
    }
}
