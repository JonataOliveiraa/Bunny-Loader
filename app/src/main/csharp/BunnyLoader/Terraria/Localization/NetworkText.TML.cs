using System.IO;
using BunnyLoader.Interop;

namespace Terraria.Localization;

public readonly partial struct NetworkText
{
    public override string ToString() => Pointer == 0 ? "" : ToString_() ?? "";

    public static NetworkText FromKey(string key, params object?[] substitutions) => Build(key, Mode.LocalizationKey, substitutions);

    public static NetworkText FromFormattable(string text, params object?[] substitutions) => Build(text, Mode.Formattable, substitutions);

    static NetworkText Build(string text, Mode mode, object?[]? substitutions)
    {
        NetworkText result = New(text, mode);
        int count = substitutions?.Length ?? 0;
        GameArray<NetworkText> list = GameArray<NetworkText>.New(Class, count);
        for (int i = 0; i < count; i++)
            list[i] = substitutions![i] is NetworkText network && !network.IsNull ? network : FromLiteral(substitutions[i]?.ToString() ?? "");
        result._substitutions = list;
        return result;
    }

    public void Serialize(BinaryWriter writer)
    {
        if (IsNull)
        {
            writer.Write((byte)Mode.Literal);
            writer.Write("");
            return;
        }

        Mode mode = _mode;
        writer.Write((byte)mode);
        writer.Write(_text ?? "");
        if (mode == Mode.Literal)
            return;
        GameArray<NetworkText> list = _substitutions;
        int count = list.IsNull ? 0 : list.Length;
        writer.Write((byte)count);
        for (int i = 0; i < count; i++)
            list[i].Serialize(writer);
    }

    public static NetworkText Deserialize(BinaryReader reader)
    {
        var mode = (Mode)reader.ReadByte();
        string text = reader.ReadString();
        if (mode == Mode.Literal)
            return FromLiteral(text);
        int count = reader.ReadByte();
        var substitutions = new object?[count];
        for (int i = 0; i < count; i++)
            substitutions[i] = Deserialize(reader);
        return Build(text, mode, substitutions);
    }
}
