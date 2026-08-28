using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.Validation
{
    public static class RecipeCanonicalizer
    {
        public static string Canonicalize(string json)
        {
            return CanonicalizeToken(JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
        }

        public static string ComputeSha256(string json)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(json)));
                var output = new StringBuilder(hash.Length * 2);
                foreach (var value in hash) output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return output.ToString();
            }
        }

        private static string CanonicalizeToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var objectBuilder = new StringBuilder(); objectBuilder.Append('{'); var first = true;
                    foreach (var property in ((JObject)token).Properties().OrderBy(property => property.Name, StringComparer.Ordinal)) { if (!first) objectBuilder.Append(','); first = false; objectBuilder.Append(JsonConvert.ToString(property.Name)); objectBuilder.Append(':'); objectBuilder.Append(CanonicalizeToken(property.Value)); }
                    objectBuilder.Append('}'); return objectBuilder.ToString();
                case JTokenType.Array:
                    var arrayBuilder = new StringBuilder(); arrayBuilder.Append('['); for (var index = 0; index < token.Count(); index++) { if (index > 0) arrayBuilder.Append(','); arrayBuilder.Append(CanonicalizeToken(token[index])); } arrayBuilder.Append(']'); return arrayBuilder.ToString();
                case JTokenType.Integer: return Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
                case JTokenType.Float:
                    var number = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture); if (double.IsNaN(number) || double.IsInfinity(number)) throw new JsonReaderException("Non-finite JSON numbers are not supported."); return number == 0d ? "0" : number.ToString("R", CultureInfo.InvariantCulture);
                case JTokenType.String: return JsonConvert.ToString(token.Value<string>());
                case JTokenType.Boolean: return token.Value<bool>() ? "true" : "false";
                case JTokenType.Null: return "null";
                default: throw new JsonReaderException("Unsupported JSON token type in canonicalizer: " + token.Type);
            }
        }
    }
}
