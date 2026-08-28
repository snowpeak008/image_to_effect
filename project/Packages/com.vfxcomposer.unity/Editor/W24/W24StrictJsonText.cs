using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.W24
{
    /// <summary>RFC-8259-style text preflight run before Json.NET for security-bound documents.</summary>
    public static class W24StrictJsonText
    {
        public const int MaxDepth = 64;
        public const int MaxNodes = 100000;

        public static JObject ParseObject(string text, string label)
        {
            return ParseObject(text,label,MaxDepth,MaxNodes);
        }

        internal static JObject ParseObjectForTests(string text,string label,int maxDepth,int maxNodes)
        {
            return ParseObject(text,label,maxDepth,maxNodes);
        }

        private static JObject ParseObject(string text,string label,int maxDepth,int maxNodes)
        {
            var parser=new Parser(text,label,maxDepth,maxNodes);
            var normalized=parser.ParseDocument();
            JObject root;
            using(var source=new StringReader(normalized))
            using(var reader=new JsonTextReader(source){DateParseHandling=DateParseHandling.None,FloatParseHandling=FloatParseHandling.Decimal,MaxDepth=maxDepth})
                root=JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Ignore, LineInfoHandling = LineInfoHandling.Ignore });
            ValidateNormalizedRoundTrip(root,normalized,parser.StringSlices,label??"JSON");
            return root;
        }

        private struct StringSlice
        {
            internal readonly int Start;
            internal readonly int Length;
            internal StringSlice(int start,int length){Start=start;Length=length;}
        }

        private static void ValidateNormalizedRoundTrip(JObject root,string normalized,IReadOnlyList<StringSlice> slices,string label)
        {
            var sliceIndex=0;
            ValidateNormalizedToken(root,normalized,slices,ref sliceIndex,label);
            if(sliceIndex!=slices.Count)throw new JsonSerializationException(label+" normalized JSON string traversal count changed during Json.NET parsing.");
        }

        private static void ValidateNormalizedToken(JToken token,string normalized,IReadOnlyList<StringSlice> slices,ref int sliceIndex,string label)
        {
            var obj=token as JObject;
            if(obj!=null)
            {
                foreach(var property in obj.Properties())
                {
                    ValidateNormalizedString(property.Name,normalized,slices,ref sliceIndex,label);
                    ValidateNormalizedToken(property.Value,normalized,slices,ref sliceIndex,label);
                }
                return;
            }
            var array=token as JArray;
            if(array!=null){foreach(var item in array)ValidateNormalizedToken(item,normalized,slices,ref sliceIndex,label);return;}
            if(token!=null&&token.Type==JTokenType.String)ValidateNormalizedString((string)token,normalized,slices,ref sliceIndex,label);
        }

        private static void ValidateNormalizedString(string value,string normalized,IReadOnlyList<StringSlice> slices,ref int sliceIndex,string label)
        {
            if(value==null||sliceIndex>=slices.Count)throw new JsonSerializationException(label+" normalized JSON string traversal changed during Json.NET parsing.");
            ValidateFinalSurrogates(value,label);
            var slice=slices[sliceIndex++];
            if((long)slice.Length!=2L+6L*value.Length||normalized[slice.Start]!='"'||normalized[slice.Start+slice.Length-1]!='"')
                throw new JsonSerializationException(label+" normalized JSON string length changed during Json.NET parsing.");
            var position=slice.Start+1;
            foreach(var codeUnit in value)
            {
                if(normalized[position++]!='\\'||normalized[position++]!='u'
                    ||normalized[position++]!=HexDigit(codeUnit>>12)||normalized[position++]!=HexDigit((codeUnit>>8)&15)
                    ||normalized[position++]!=HexDigit((codeUnit>>4)&15)||normalized[position++]!=HexDigit(codeUnit&15))
                    throw new JsonSerializationException(label+" normalized JSON string code units changed during Json.NET parsing.");
            }
        }

        private static void ValidateFinalSurrogates(string value,string label)
        {
            for(var index=0;index<value.Length;index++)
            {
                if(char.IsHighSurrogate(value[index]))
                {
                    if(index+1>=value.Length||!char.IsLowSurrogate(value[index+1]))throw new JsonSerializationException(label+" Json.NET result contains an isolated high surrogate.");
                    index++;continue;
                }
                if(char.IsLowSurrogate(value[index]))throw new JsonSerializationException(label+" Json.NET result contains an isolated low surrogate.");
            }
        }

        private static char HexDigit(int value){return(char)(value<10?'0'+value:'A'+value-10);}

        private sealed class Parser
        {
            private readonly string text;
            private readonly string label;
            private readonly int maxDepth;
            private readonly int maxNodes;
            private readonly StringBuilder normalized;
            internal readonly List<StringSlice> StringSlices=new List<StringSlice>();
            private int index;
            private int depth;
            private int nodes;

            internal Parser(string text, string label, int maxDepth, int maxNodes)
            {
                if (string.IsNullOrWhiteSpace(text)) throw new JsonSerializationException((label ?? "JSON") + " is required.");
                if(maxDepth<1)throw new ArgumentOutOfRangeException(nameof(maxDepth),"Strict JSON maximum depth must be positive.");
                if(maxNodes<1)throw new ArgumentOutOfRangeException(nameof(maxNodes),"Strict JSON maximum node count must be positive.");
                this.text = text; this.label = label ?? "JSON"; this.maxDepth=maxDepth; this.maxNodes=maxNodes;normalized=new StringBuilder(text.Length);
            }

            internal string ParseDocument()
            {
                SkipWhitespace();
                if (Peek() != '{') Fail("root must be an object");
                ParseObject(); SkipWhitespace();
                if (index != text.Length) Fail("contains an extra root value or trailing text");
                return normalized.ToString();
            }

            private void ParseValue()
            {
                SkipWhitespace(); var value = Peek();
                if (value == '{') { EnsureCanEnterContainer("object"); ParseObject(); }
                else if (value == '[') { EnsureCanEnterContainer("array"); ParseArray(); }
                else if (value == '"') { AddNode("string value"); AppendNormalizedString(ParseString()); }
                else if (value == 't') { AddNode("boolean value"); Literal("true"); normalized.Append("true"); }
                else if (value == 'f') { AddNode("boolean value"); Literal("false"); normalized.Append("false"); }
                else if (value == 'n') { AddNode("null value"); Literal("null"); normalized.Append("null"); }
                else if (value == '-' || (value >= '0' && value <= '9')) { AddNode("number value"); ParseNumber(); }
                else Fail("contains a forbidden token (comments, single quotes, NaN, and Infinity are not JSON)");
            }

            private void ParseObject()
            {
                EnterContainer("object");
                try
                {
                    Require('{');normalized.Append('{'); SkipWhitespace(); var names = new HashSet<string>(StringComparer.Ordinal);
                    if (Take('}')){normalized.Append('}');return;}
                    while (true)
                    {
                        SkipWhitespace(); if (Peek() != '"') Fail("object property names must use double quotes");
                        var name = ParseString(); AddNode("object property"); if (!names.Add(name)) Fail("contains duplicate property '" + name + "'");
                        AppendNormalizedString(name);SkipWhitespace(); Require(':');normalized.Append(':'); ParseValue(); SkipWhitespace();
                        if (Take('}')){normalized.Append('}');return;}
                        Require(',');normalized.Append(','); SkipWhitespace(); if (Peek() == '}') Fail("contains a trailing object comma");
                    }
                }
                finally { depth--; }
            }

            private void ParseArray()
            {
                EnterContainer("array");
                try
                {
                    Require('[');normalized.Append('['); SkipWhitespace(); if (Take(']')){normalized.Append(']');return;}
                    while (true)
                    {
                        ParseValue(); SkipWhitespace(); if (Take(']')){normalized.Append(']');return;}
                        Require(',');normalized.Append(','); SkipWhitespace(); if (Peek() == ']') Fail("contains a trailing array comma");
                    }
                }
                finally { depth--; }
            }

            private string ParseString()
            {
                Require('"'); var builder = new StringBuilder(); char? pendingHigh=null;
                while (index < text.Length)
                {
                    var character = text[index++];
                    if (character == '"') { if(pendingHigh.HasValue)Fail("contains an isolated high surrogate"); return builder.ToString(); }
                    if (character < 0x20) Fail("contains an unescaped control character");
                    if (character == '\\')
                    {
                        if (index >= text.Length) Fail("contains an incomplete escape");
                        var escape = text[index++];
                        switch (escape)
                        {
                            case '"': AppendDecodedCodeUnit(builder,ref pendingHigh,'"'); break; case '\\': AppendDecodedCodeUnit(builder,ref pendingHigh,'\\'); break; case '/': AppendDecodedCodeUnit(builder,ref pendingHigh,'/'); break;
                            case 'b': AppendDecodedCodeUnit(builder,ref pendingHigh,'\b'); break; case 'f': AppendDecodedCodeUnit(builder,ref pendingHigh,'\f'); break; case 'n': AppendDecodedCodeUnit(builder,ref pendingHigh,'\n'); break; case 'r': AppendDecodedCodeUnit(builder,ref pendingHigh,'\r'); break; case 't': AppendDecodedCodeUnit(builder,ref pendingHigh,'\t'); break;
                            case 'u': AppendDecodedCodeUnit(builder,ref pendingHigh,ReadHexCodeUnit()); break;
                            default: Fail("contains an invalid string escape"); break;
                        }
                        continue;
                    }
                    AppendDecodedCodeUnit(builder,ref pendingHigh,character);
                }
                Fail("contains an unterminated string"); return null;
            }

            private void AppendDecodedCodeUnit(StringBuilder builder,ref char? pendingHigh,char value)
            {
                if(pendingHigh.HasValue)
                {
                    if(!char.IsLowSurrogate(value))Fail("contains an invalid decoded surrogate pair");
                    builder.Append(pendingHigh.Value);builder.Append(value);pendingHigh=null;return;
                }
                if(char.IsHighSurrogate(value)){pendingHigh=value;return;}
                if(char.IsLowSurrogate(value))Fail("contains an isolated low surrogate");
                builder.Append(value);
            }

            private char ReadHexCodeUnit()
            {
                if (index + 4 > text.Length) Fail("contains an incomplete unicode escape");
                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var character = text[index++]; var digit = character >= '0' && character <= '9' ? character - '0' : character >= 'a' && character <= 'f' ? character - 'a' + 10 : character >= 'A' && character <= 'F' ? character - 'A' + 10 : -1;
                    if (digit < 0) Fail("contains a non-hex unicode escape"); value = value * 16 + digit;
                }
                return (char)value;
            }

            private void AppendNormalizedString(string value)
            {
                var start=normalized.Length;normalized.Append('"');
                foreach(var codeUnit in value)
                {
                    normalized.Append("\\u");
                    normalized.Append(HexDigit(codeUnit>>12));normalized.Append(HexDigit((codeUnit>>8)&15));
                    normalized.Append(HexDigit((codeUnit>>4)&15));normalized.Append(HexDigit(codeUnit&15));
                }
                normalized.Append('"');StringSlices.Add(new StringSlice(start,normalized.Length-start));
            }

            private void ParseNumber()
            {
                var start = index; Take('-');
                if (Take('0')) { if (IsDigit(Peek())) Fail("contains a leading-zero number"); }
                else { if (!IsDigit(Peek())) Fail("contains an invalid number"); while (IsDigit(Peek())) index++; }
                if (Take('.')) { if (!IsDigit(Peek())) Fail("contains an invalid fraction"); while (IsDigit(Peek())) index++; }
                if (Peek() == 'e' || Peek() == 'E') { index++; if (Peek() == '+' || Peek() == '-') index++; if (!IsDigit(Peek())) Fail("contains an invalid exponent"); while (IsDigit(Peek())) index++; }
                double parsed; if (!double.TryParse(text.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed)) Fail("contains a non-finite or out-of-range number");
                normalized.Append(text,start,index-start);
            }

            private void Literal(string value)
            {
                if (index + value.Length > text.Length || !string.Equals(text.Substring(index, value.Length), value, StringComparison.Ordinal)) Fail("contains an invalid literal"); index += value.Length;
            }

            private void EnterContainer(string kind)
            {
                EnsureCanEnterContainer(kind);
                AddNode(kind+" container");depth++;
            }

            private void EnsureCanEnterContainer(string kind)
            {
                if(depth>=maxDepth)Fail("exceeds maximum depth "+maxDepth.ToString(CultureInfo.InvariantCulture));
                if(nodes>=maxNodes)Fail("exceeds maximum node count "+maxNodes.ToString(CultureInfo.InvariantCulture)+" before "+kind+" container");
            }

            private void AddNode(string kind)
            {
                if(nodes>=maxNodes)Fail("exceeds maximum node count "+maxNodes.ToString(CultureInfo.InvariantCulture)+" before "+kind);
                nodes++;
            }

            private void SkipWhitespace() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private char Peek() { return index < text.Length ? text[index] : '\0'; }
            private bool Take(char value) { if (Peek() != value) return false; index++; return true; }
            private void Require(char value) { if (!Take(value)) Fail("expected '" + value + "'"); }
            private static bool IsDigit(char value) { return value >= '0' && value <= '9'; }
            private void Fail(string message) { throw new JsonSerializationException(label + " " + message + " at character " + index.ToString(CultureInfo.InvariantCulture) + "."); }
        }
    }
}
