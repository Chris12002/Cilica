// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded managed adaptation of Decimal.DecCalc.cs. See ../provenance.json.
using System;
namespace Cilica.Library
{
    // A coefficient, sign and decimal scale, not a binary/native Decimal ABI.
    public readonly struct DecimalParts
    {
        public readonly uint Low, Mid, High;
        public readonly int Flags;
        public DecimalParts(uint low, uint mid, uint high, bool negative, int scale)
        {
            if ((uint)scale > 28) throw new ArgumentOutOfRangeException("scale");
            Low = low; Mid = mid; High = high;
            Flags = (scale << 16) | (negative ? (-2147483647 - 1) : 0);
        }
    }

    public static class DecimalArithmetic
    {
        public static DecimalParts FromInt32(int value) => new DecimalParts(
            value < 0 ? unchecked(0U - (uint)value) : (uint)value, 0, 0, value < 0, 0);

        public static DecimalParts Multiply(DecimalParts left, DecimalParts right)
        {
            int scale = Scale(left) + Scale(right);
            bool negative = (left.Flags ^ right.Flags) < 0;
            bool small = (left.Mid | left.High | right.Mid | right.High) == 0;
            // Preserve DecCalc's observable zero scale on its two fast paths.
            if (small && scale > 47) return default;
            var a = Words(left); var b = Words(right); var product = new uint[6];
            for (int i = 0; i < 3; i++)
            {
                ulong carry = 0;
                for (int j = 0; j < 3; j++)
                {
                    ulong word = (ulong)a[i] * b[j] + product[i + j] + carry;
                    product[i + j] = (uint)word; carry = word >> 32;
                }
                product[i + 3] = (uint)carry;
            }
            if (!small && Zero(product)) return default;
            uint remainder = 0; bool sticky = false;
            while (scale > 28 || !Fits(product))
            {
                if (scale == 0) throw new OverflowException("Value was either too large or too small for a Decimal.");
                sticky = sticky || remainder != 0;
                remainder = DivideTen(product); scale--;
            }
            if (remainder > 5 || remainder == 5 && (sticky || (product[0] & 1) != 0))
            {
                Add(product, 1);
                if (!Fits(product))
                {
                    if (scale == 0) throw new OverflowException("Value was either too large or too small for a Decimal.");
                    // Match ScaleResult: after a rounding carry, reset sticky.
                    remainder = DivideTen(product); scale--;
                    if (remainder > 5 || remainder == 5 && (product[0] & 1) != 0) Add(product, 1);
                }
            }
            return Pack(product, negative, scale);
        }

        public static DecimalParts Divide(DecimalParts left, DecimalParts right)
        {
            var numerator = Words(left); var denominator = Words(right);
            if (Zero(denominator)) throw new DivideByZeroException();
            var quotient = new uint[6]; var remainder = new uint[6]; var scratch = new uint[6];
            // One bounded binary long division, then at most 56 decimal digits.
            for (int bit = 95; bit >= 0; bit--)
            {
                Times(remainder, 2);
                remainder[0] = remainder[0] | (numerator[bit / 32] >> (bit % 32)) & 1;
                if (Compare(remainder, denominator) >= 0)
                {
                    Subtract(remainder, denominator);
                    quotient[bit / 32] = quotient[bit / 32] | 1U << (bit % 32);
                }
            }
            int scale = Scale(left) - Scale(right); bool unscale = false;
            while (true)
            {
                bool nonzero = !Zero(remainder);
                if (!nonzero && scale >= 0) break;
                if (nonzero) unscale = true;
                Copy(quotient, scratch); Times(scratch, 10);
                if (scale == 28 || !Fits(scratch))
                {
                    if (scale < 0) throw new OverflowException("Value was either too large or too small for a Decimal.");
                    Times(remainder, 2);
                    int comparison = Compare(remainder, denominator);
                    if (comparison > 0 || comparison == 0 && (quotient[0] & 1) != 0)
                    {
                        Add(quotient, 1);
                        if (!Fits(quotient)) scale = OverflowUnscale(quotient, scale, true);
                    }
                    break;
                }
                Copy(scratch, quotient); scale++;
                Times(remainder, 10); uint digit = 0;
                while (Compare(remainder, denominator) >= 0)
                {
                    Subtract(remainder, denominator); digit++;
                }
                Add(quotient, digit);
                if (!Fits(quotient))
                {
                    scale = OverflowUnscale(quotient, scale, !Zero(remainder));
                    break;
                }
            }
            if (unscale)
            {
                while (scale > 0)
                {
                    Copy(quotient, scratch);
                    if (DivideTen(scratch) != 0) break;
                    Copy(scratch, quotient); scale--;
                }
            }
            return Pack(quotient, (left.Flags ^ right.Flags) < 0, scale);
        }

        public static string InvariantText(DecimalParts value)
        {
            var words = Words(value); bool negative = value.Flags < 0 && !Zero(words);
            var reverse = new char[29]; int digits = 0;
            do { reverse[digits++] = (char)('0' + DivideTen(words)); } while (!Zero(words));
            int scale = Scale(value); int integral = digits > scale ? digits - scale : 1;
            int size = integral + (scale == 0 ? 0 : scale + 1) + (negative ? 1 : 0);
            var text = new char[size]; int position = 0;
            if (negative) text[position++] = '-';
            for (int i = integral - 1; i >= 0; i--)
            {
                int digit = scale + i;
                text[position++] = digit < digits ? reverse[digit] : '0';
            }
            if (scale != 0) text[position++] = '.';
            for (int i = scale - 1; i >= 0; i--) text[position++] = i < digits ? reverse[i] : '0';
            return new string(text);
        }

        private static int Scale(DecimalParts value) => (value.Flags >> 16) & 255;
        private static DecimalParts Pack(uint[] words, bool negative, int scale) => new DecimalParts(words[0], words[1], words[2], negative, scale);
        private static uint[] Words(DecimalParts value)
        {
            var words = new uint[6]; words[0] = value.Low; words[1] = value.Mid; words[2] = value.High; return words;
        }
        private static bool Fits(uint[] words) => (words[3] | words[4] | words[5]) == 0;
        private static bool Zero(uint[] words) => (words[0] | words[1] | words[2] | words[3] | words[4] | words[5]) == 0;
        private static void Copy(uint[] source, uint[] destination)
        { for (int i = 0; i < 6; i++) destination[i] = source[i]; }
        private static void Times(uint[] words, uint multiplier)
        {
            ulong carry = 0;
            for (int i = 0; i < 6; i++)
            {
                ulong word = (ulong)words[i] * multiplier + carry;
                words[i] = (uint)word; carry = word >> 32;
            }
        }
        private static void Add(uint[] words, uint value)
        {
            ulong carry = value;
            for (int i = 0; i < 6 && carry != 0; i++)
            {
                ulong word = words[i] + carry; words[i] = (uint)word; carry = word >> 32;
            }
        }
        private static int Compare(uint[] left, uint[] right)
        {
            for (int i = 5; i >= 0; i--)
            { if (left[i] != right[i]) return left[i] < right[i] ? -1 : 1; }
            return 0;
        }
        private static void Subtract(uint[] left, uint[] right)
        {
            ulong borrow = 0;
            for (int i = 0; i < 6; i++)
            {
                ulong sub = right[i] + borrow; ulong before = left[i];
                left[i] = unchecked((uint)(before - sub)); borrow = before < sub ? 1UL : 0UL;
            }
        }
        private static uint DivideTen(uint[] words)
        {
            ulong remainder = 0;
            for (int i = 5; i >= 0; i--)
            {
                ulong word = (remainder << 32) | words[i];
                words[i] = (uint)(word / 10); remainder = word % 10;
            }
            return (uint)remainder;
        }
        private static int OverflowUnscale(uint[] words, int scale, bool sticky)
        {
            if (scale <= 0) throw new OverflowException("Value was either too large or too small for a Decimal.");
            uint remainder = DivideTen(words);
            if (remainder > 5 || remainder == 5 && (sticky || (words[0] & 1) != 0)) Add(words, 1);
            return scale - 1;
        }
    }
}
