using System.Text;

namespace VFXComposer.Protocol.Json;

/// <summary>
/// Canonicalizes a bounded JSON decimal directly from its lexical digits. No binary
/// floating-point or fixed-width decimal conversion is involved.
/// </summary>
internal static class ExactDecimalNormalizer
{
    internal const int MaximumCoefficientDigits = 256;
    internal const int MaximumExponentMagnitude = 100_000;
    private const int MaximumExponentDigits = 6;
    private const int MinimumPlainScientificExponent = -6;
    private const int MaximumPlainScientificExponent = 20;

    public static string Normalize(ReadOnlySpan<byte> raw)
    {
        if (raw.Length == 0)
        {
            throw Invalid();
        }

        var index = 0;
        var negative = false;
        if (raw[index] == (byte)'-')
        {
            negative = true;
            index++;
            if (index == raw.Length)
            {
                throw Invalid();
            }
        }

        var digits = new StringBuilder(MaximumCoefficientDigits);
        if (raw[index] == (byte)'0')
        {
            digits.Append('0');
            index++;
            if (index < raw.Length && IsDigit(raw[index]))
            {
                throw Invalid();
            }
        }
        else if (raw[index] is >= (byte)'1' and <= (byte)'9')
        {
            while (index < raw.Length && IsDigit(raw[index]))
            {
                AppendDigit(digits, raw[index]);
                index++;
            }
        }
        else
        {
            throw Invalid();
        }

        var fractionDigits = 0;
        if (index < raw.Length && raw[index] == (byte)'.')
        {
            index++;
            var fractionStart = index;
            while (index < raw.Length && IsDigit(raw[index]))
            {
                AppendDigit(digits, raw[index]);
                fractionDigits++;
                index++;
            }

            if (index == fractionStart)
            {
                throw Invalid();
            }
        }

        var explicitExponent = 0;
        if (index < raw.Length && raw[index] is (byte)'e' or (byte)'E')
        {
            index++;
            var exponentNegative = false;
            if (index < raw.Length && raw[index] is (byte)'+' or (byte)'-')
            {
                exponentNegative = raw[index] == (byte)'-';
                index++;
            }

            var exponentStart = index;
            var exponentDigits = 0;
            while (index < raw.Length && IsDigit(raw[index]))
            {
                exponentDigits++;
                if (exponentDigits > MaximumExponentDigits)
                {
                    throw new StrictJsonException(
                        "NUMBER_EXPONENT_LIMIT",
                        "JSON number exponent exceeds the lexical limit.");
                }

                explicitExponent = checked((explicitExponent * 10) + (raw[index] - (byte)'0'));
                index++;
            }

            if (index == exponentStart || explicitExponent > MaximumExponentMagnitude)
            {
                throw new StrictJsonException(
                    "NUMBER_EXPONENT_LIMIT",
                    "JSON number exponent exceeds the magnitude limit.");
            }

            if (exponentNegative)
            {
                explicitExponent = -explicitExponent;
            }
        }

        if (index != raw.Length)
        {
            throw Invalid();
        }

        var coefficient = digits.ToString();
        var firstNonZero = 0;
        while (firstNonZero < coefficient.Length && coefficient[firstNonZero] == '0')
        {
            firstNonZero++;
        }

        if (firstNonZero == coefficient.Length)
        {
            return "0";
        }

        coefficient = coefficient[firstNonZero..];
        var decimalExponent = explicitExponent - fractionDigits;
        var last = coefficient.Length - 1;
        while (last > 0 && coefficient[last] == '0')
        {
            last--;
            decimalExponent++;
        }

        coefficient = coefficient[..(last + 1)];
        var scientificExponent = checked(decimalExponent + coefficient.Length - 1);
        var sign = negative ? "-" : string.Empty;
        if (scientificExponent is >= MinimumPlainScientificExponent and <= MaximumPlainScientificExponent)
        {
            return sign + FormatPlain(coefficient, decimalExponent);
        }

        var mantissa = coefficient.Length == 1
            ? coefficient
            : coefficient[0] + "." + coefficient[1..];
        return sign + mantissa + "e" + scientificExponent.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatPlain(string coefficient, int decimalExponent)
    {
        var decimalPosition = coefficient.Length + decimalExponent;
        if (decimalPosition <= 0)
        {
            return "0." + new string('0', -decimalPosition) + coefficient;
        }

        if (decimalPosition >= coefficient.Length)
        {
            return coefficient + new string('0', decimalPosition - coefficient.Length);
        }

        return coefficient[..decimalPosition] + "." + coefficient[decimalPosition..];
    }

    private static void AppendDigit(StringBuilder digits, byte digit)
    {
        if (digits.Length >= MaximumCoefficientDigits)
        {
            throw new StrictJsonException(
                "NUMBER_DIGIT_LIMIT",
                "JSON number exceeds the coefficient digit limit.");
        }

        digits.Append((char)digit);
    }

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static StrictJsonException Invalid() =>
        new("INVALID_NUMBER", "JSON number syntax is invalid.");
}
