using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

// Json.NET accepts JSON extensions. Validate RFC 8259 grammar before materializing the bounded body.
internal static class StrictWireJson
{
    public static JObject Read(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            new Grammar(text).Read();
            using var reader = new JsonTextReader(new StringReader(text))
            { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal, MaxDepth = 16 };
            return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        catch (Exception error) when (error is JsonException || error is FormatException || error is DecoderFallbackException || error is OverflowException)
        { throw new WireInputException(); }
    }
    private sealed class Grammar
    {
        private readonly string _text;
        private int _index;
        public Grammar(string text) { _text = text; }
        public void Read() { Value(0); White(); if (_index != _text.Length) Fail(); }
        private void White() { while (_index < _text.Length && " \t\r\n".IndexOf(_text[_index]) >= 0) _index++; }
        private char Current => _index < _text.Length ? _text[_index] : '\0';
        private static void Fail() => throw new FormatException();
        private void Take(char value) { White(); if (Current != value) Fail(); _index++; }
        private void Value(int depth)
        {
            if (depth > 15) Fail();
            White();
            switch (Current)
            {
                case '{':
                    _index++; White(); if (Current == '}') { _index++; return; }
                    while (true)
                    {
                        White(); String(); Take(':'); Value(depth + 1); White();
                        if (Current == '}') { _index++; return; } Take(',');
                    }
                case '[':
                    _index++; White(); if (Current == ']') { _index++; return; }
                    while (true) { Value(depth + 1); White(); if (Current == ']') { _index++; return; } Take(','); }
                case '"': String(); return;
                case 't': Literal("true"); return;
                case 'f': Literal("false"); return;
                case 'n': Literal("null"); return;
                default: Number(); return;
            }
        }
        private void Literal(string value)
        { if (_index + value.Length > _text.Length || string.CompareOrdinal(_text, _index, value, 0, value.Length) != 0) Fail(); _index += value.Length; }
        private void Number()
        {
            if (Current == '-') _index++;
            if (Current == '0') _index++;
            else { if (Current < '1' || Current > '9') Fail(); Digits(); }
            if (Current == '.') { _index++; if (!Digit(Current)) Fail(); Digits(); }
            if (Current == 'e' || Current == 'E') { _index++; if (Current == '+' || Current == '-') _index++; if (!Digit(Current)) Fail(); Digits(); }
        }
        private static bool Digit(char value) => value >= '0' && value <= '9';
        private void Digits() { while (Digit(Current)) _index++; }
        private void String()
        {
            if (Current != '"') Fail(); _index++;
            while (_index < _text.Length)
            {
                var value = _text[_index++];
                if (value == '"') return;
                if (value < 32) Fail();
                if (value != '\\') continue;
                if (_index >= _text.Length) Fail();
                var escape = _text[_index++];
                if (escape == 'u')
                {
                    var unicode = Unicode();
                    if (char.IsHighSurrogate(unicode))
                    {
                        if (_index + 2 > _text.Length || _text[_index++] != '\\' || _text[_index++] != 'u' || !char.IsLowSurrogate(Unicode())) Fail();
                    }
                    else if (char.IsLowSurrogate(unicode)) Fail();
                }
                else if ("\"\\/bfnrt".IndexOf(escape) < 0) Fail();
            }
            Fail();
        }
        private char Unicode()
        {
            if (_index + 4 > _text.Length) { Fail(); return '\0'; }
            var value = 0;
            for (var count = 0; count < 4; count++)
            {
                var digit = _text[_index++];
                var number = digit >= '0' && digit <= '9' ? digit - '0' : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                if (number < 0) Fail(); value = value * 16 + number;
            }
            return (char)value;
        }
    }
}
internal sealed class WireInputException : Exception
{
    public WireInputException(string? field = null) : base("The request is invalid.") { Field = field; }
    public string? Field { get; }
}
