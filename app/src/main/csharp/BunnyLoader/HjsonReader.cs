using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BunnyLoader;

internal sealed class HjsonReader
{
    static readonly Regex Literal = new(@"^(-?\d+(\.\d+)?([eE][+-]?\d+)?|true|false|null)\s*(,|\}|\]|#|//|/\*|$)", RegexOptions.CultureInvariant);

    readonly string text;
    readonly Dictionary<string, string> values;
    int pos;

    HjsonReader(string text, Dictionary<string, string> values)
    {
        this.text = text;
        this.values = values;
    }

    public static Dictionary<string, string> Flatten(string text, string prefix)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = new HjsonReader(text, values);
        reader.SkipBlank();
        if (reader.Peek() == '{')
        {
            reader.pos++;
            reader.Members(prefix, '}');
        }
        else
        {
            reader.Members(prefix, '\0');
        }
        reader.SkipBlank();
        if (reader.pos < text.Length) throw reader.Error("unexpected text after the end");
        return values;
    }

    char Peek() => pos < text.Length ? text[pos] : '\0';

    void Members(string prefix, char close)
    {
        while (true)
        {
            SkipBlank();
            if (pos >= text.Length)
            {
                if (close != '\0') throw Error($"missing '{close}'");
                return;
            }
            if (text[pos] == close)
            {
                pos++;
                return;
            }
            if (close == '\0' && text[pos] == '}') throw Error("unexpected '}'");
            string key = Key();
            SkipInline();
            if (Peek() != ':') throw Error($"expected ':' after '{key}'");
            pos++;
            SkipBlank();
            Value(Join(prefix, key));
        }
    }

    void Elements(string prefix)
    {
        int index = 0;
        while (true)
        {
            SkipBlank();
            if (pos >= text.Length) throw Error("missing ']'");
            if (text[pos] == ']')
            {
                pos++;
                return;
            }
            Value(Join(prefix, index.ToString(CultureInfo.InvariantCulture)));
            index++;
        }
    }

    void Value(string path)
    {
        char c = Peek();
        switch (c)
        {
            case '\0':
                throw Error($"missing the value of '{path}'");
            case '{':
                pos++;
                Members(path, '}');
                return;
            case '[':
                pos++;
                Elements(path);
                return;
            case '"':
                values[path] = Quoted('"');
                return;
            case '\'':
                values[path] = string.CompareOrdinal(text, pos, "'''", 0, 3) == 0 ? Multiline() : Quoted('\'');
                return;
        }
        int end = text.IndexOf('\n', pos);
        if (end < 0) end = text.Length;
        string line = text[pos..end];
        Match literal = Literal.Match(line);
        if (literal.Success)
        {
            values[path] = literal.Groups[1].Value;
            pos += literal.Groups[1].Length;
            return;
        }
        values[path] = line.TrimEnd();
        pos = end;
    }

    string Key()
    {
        char c = Peek();
        if (c == '"' || c == '\'') return Quoted(c);
        int start = pos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != ':' && "{}[],".IndexOf(text[pos]) < 0) pos++;
        if (pos == start) throw Error($"expected a name, found '{c}'");
        return text[start..pos];
    }

    string Quoted(char quote)
    {
        pos++;
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= text.Length || text[pos] == '\n') throw Error("unterminated string");
            char c = text[pos++];
            if (c == quote) return sb.ToString();
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }
            if (pos >= text.Length) throw Error("unterminated string");
            char e = text[pos++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if (pos + 4 > text.Length || !int.TryParse(text.AsSpan(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        throw Error("bad \\u escape");
                    sb.Append((char)code);
                    pos += 4;
                    break;
                default: sb.Append(e); break;
            }
        }
    }

    string Multiline()
    {
        int indent = pos - (text.LastIndexOf('\n', Math.Max(0, pos - 1)) + 1);
        pos += 3;
        while (pos < text.Length && text[pos] != '\n' && char.IsWhiteSpace(text[pos])) pos++;
        if (Peek() == '\n')
        {
            pos++;
            SkipIndent(indent);
        }
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= text.Length) throw Error("unterminated '''");
            if (string.CompareOrdinal(text, pos, "'''", 0, 3) == 0)
            {
                pos += 3;
                if (sb.Length > 0 && sb[^1] == '\n') sb.Length--;
                if (sb.Length > 0 && sb[^1] == '\r') sb.Length--;
                return sb.ToString();
            }
            char c = text[pos++];
            if (c == '\r') continue;
            sb.Append(c);
            if (c == '\n') SkipIndent(indent);
        }
    }

    void SkipIndent(int indent)
    {
        for (int i = 0; i < indent && pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'); i++) pos++;
    }

    void SkipInline()
    {
        while (pos < text.Length && text[pos] != '\n' && char.IsWhiteSpace(text[pos])) pos++;
    }

    void SkipBlank()
    {
        while (pos < text.Length)
        {
            char c = text[pos];
            if (char.IsWhiteSpace(c) || c == ',')
            {
                pos++;
            }
            else if (c == '#' || (c == '/' && Peek(1) == '/'))
            {
                while (pos < text.Length && text[pos] != '\n') pos++;
            }
            else if (c == '/' && Peek(1) == '*')
            {
                int end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                if (end < 0) throw Error("unterminated /* comment");
                pos = end + 2;
            }
            else
            {
                return;
            }
        }
    }

    char Peek(int ahead) => pos + ahead < text.Length ? text[pos + ahead] : '\0';

    static string Join(string prefix, string key) => prefix.Length == 0 ? key : prefix + "." + key;

    FormatException Error(string message)
    {
        int line = 1, column = 1;
        for (int i = 0; i < pos && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return new FormatException($"line {line}, column {column}: {message}");
    }
}
