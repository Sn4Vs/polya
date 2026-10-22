using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gazik.Core
{
    // A small strict JSON codec. Unknown save fields survive a complete round trip.
    public static class Json
    {
        public static object Parse(string text) { return new Reader(text).ReadDocument(); }
        public static string Stringify(object value) { var b = new StringBuilder(); Write(b, value); return b.ToString(); }
        static void Write(StringBuilder b, object v)
        {
            if (v == null) { b.Append("null"); return; }
            if (v is string s) { Quote(b, s); return; }
            if (v is bool flag) { b.Append(flag ? "true" : "false"); return; }
            if (v is IDictionary<string, object> map)
            {
                b.Append('{'); bool first = true;
                foreach (var p in map) { if (!first) b.Append(','); first = false; Quote(b, p.Key); b.Append(':'); Write(b, p.Value); }
                b.Append('}'); return;
            }
            if (v is IEnumerable list)
            {
                b.Append('['); bool first = true;
                foreach (object item in list) { if (!first) b.Append(','); first = false; Write(b, item); }
                b.Append(']'); return;
            }
            double n = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("Non-finite JSON number");
            b.Append(n.ToString("R", CultureInfo.InvariantCulture));
        }
        static void Quote(StringBuilder b, string s)
        {
            b.Append('"'); foreach (char c in s)
            {
                switch (c) { case '"': b.Append("\\\""); break; case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break; case '\r': b.Append("\\r"); break; case '\t': b.Append("\\t"); break;
                    default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4")); else b.Append(c); break; }
            } b.Append('"');
        }
        sealed class Reader
        {
            readonly string text; int pos; int depth;
            public Reader(string s) { text = s ?? throw new ArgumentNullException(nameof(s)); }
            void Space() { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
            void Require(char c) { Space(); if (pos >= text.Length || text[pos++] != c) throw new FormatException("Invalid JSON at " + pos); }
            bool Take(char c) { Space(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
            public object ReadDocument() { object v = Value(); Space(); if (pos != text.Length) throw new FormatException("Trailing JSON"); return v; }
            object Value()
            {
                Space(); if (++depth > 100 || pos >= text.Length) throw new FormatException("Invalid JSON depth/end");
                object value; char c = text[pos];
                if (c == '{') value = Object(); else if (c == '[') value = Array(); else if (c == '"') value = String();
                else if (c == 't') value = Literal("true", true); else if (c == 'f') value = Literal("false", false);
                else if (c == 'n') value = Literal("null", null); else value = Number();
                depth--; return value;
            }
            object Literal(string token, object result) { if (pos + token.Length > text.Length || text.Substring(pos, token.Length) != token) throw new FormatException("Invalid literal"); pos += token.Length; return result; }
            Dictionary<string, object> Object()
            {
                Require('{'); var map = new Dictionary<string, object>(); if (Take('}')) return map;
                do { Space(); if (pos >= text.Length || text[pos] != '"') throw new FormatException("Missing JSON key"); string key = String(); Require(':'); map[key] = Value(); } while (Take(','));
                Require('}'); return map;
            }
            List<object> Array() { Require('['); var a = new List<object>(); if (Take(']')) return a; do { a.Add(Value()); } while (Take(',')); Require(']'); return a; }
            string String()
            {
                Require('"'); var b = new StringBuilder();
                while (pos < text.Length)
                {
                    char c = text[pos++]; if (c == '"') return b.ToString(); if (c < 32) throw new FormatException("Control character in JSON string");
                    if (c != '\\') { b.Append(c); continue; } if (pos >= text.Length) break;
                    c = text[pos++]; switch (c)
                    {
                        case '"': case '\\': case '/': b.Append(c); break; case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break;
                        case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                        case 'u': if (pos + 4 > text.Length) throw new FormatException("Unicode escape"); b.Append((char)int.Parse(text.Substring(pos, 4), NumberStyles.HexNumber)); pos += 4; break;
                        default: throw new FormatException("Invalid JSON escape");
                    }
                } throw new FormatException("Unterminated JSON string");
            }
            double Number()
            {
                int start = pos; if (pos < text.Length && text[pos] == '-') pos++;
                if (pos >= text.Length) throw new FormatException("Missing number");
                if (text[pos] == '0') pos++; else { if (text[pos] < '1' || text[pos] > '9') throw new FormatException("Invalid number"); while (pos < text.Length && char.IsDigit(text[pos])) pos++; }
                if (pos < text.Length && text[pos] == '.') { pos++; int mark = pos; while (pos < text.Length && char.IsDigit(text[pos])) pos++; if (mark == pos) throw new FormatException("Missing fraction"); }
                if (pos < text.Length && (text[pos] == 'e' || text[pos] == 'E')) { pos++; if (pos < text.Length && (text[pos] == '+' || text[pos] == '-')) pos++; int mark = pos; while (pos < text.Length && char.IsDigit(text[pos])) pos++; if (pos == mark) throw new FormatException("Missing exponent"); }
                double n = double.Parse(text.Substring(start, pos - start), CultureInfo.InvariantCulture); if (double.IsInfinity(n)) throw new FormatException("Number overflow"); return n;
            }
        }
    }
}
