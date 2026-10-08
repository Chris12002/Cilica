// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded Decimal surface; ordinary managed arithmetic, no native decimal ABI.
namespace System
{
    public readonly struct Decimal : IConvertible
    {
        private readonly Cilica.Library.DecimalParts value;
        public Decimal(int value) { this.value = Cilica.Library.DecimalArithmetic.FromInt32(value); }
        private Decimal(Cilica.Library.DecimalParts value) { this.value = value; }
        public static implicit operator Decimal(int value) => new Decimal(value);
        public static Decimal operator *(Decimal left, Decimal right) => new Decimal(Cilica.Library.DecimalArithmetic.Multiply(left.value, right.value));
        public static Decimal operator /(Decimal left, Decimal right) => new Decimal(Cilica.Library.DecimalArithmetic.Divide(left.value, right.value));
        // Admission proves the exact InvariantCulture expression before this body runs.
        public string ToString(IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture) throw new NotSupportedException("Only the invariant provider is supported.");
            return Cilica.Library.DecimalArithmetic.InvariantText(value);
        }
        public override string ToString() => Cilica.Runtime.UnsupportedLibraryMember<string>.Invoke();
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
}
