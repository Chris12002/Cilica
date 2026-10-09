// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Cilica-owned bounded Nullable<T>. Storage and accessors use ordinary managed
// value operations; nullable box/copy operations use the managed helper below.
namespace System
{
    public struct Nullable<T> where T : struct
    {
        private readonly bool hasValue;
        // Pinned Nullable permits a mutable value override to update this payload.
        private T value;

        public Nullable(T value) { this.value = value; hasValue = true; }
        public readonly bool HasValue => hasValue;
        public readonly T Value
        {
            get
            {
                if (!hasValue) throw new InvalidOperationException("Nullable object must have a value.");
                return value;
            }
        }
        public readonly T GetValueOrDefault() => value;
        public readonly T GetValueOrDefault(T defaultValue) => hasValue ? value : defaultValue;

        // Keep inherited/default value semantics from masquerading as nullable
        // semantics. Equality/hash remain explicit gaps; text uses the pinned payload call.
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public override string ToString() => hasValue ? value.ToString() : "";
        public static implicit operator Nullable<T>(T value) => new Nullable<T>(value);
        public static explicit operator T(Nullable<T> value) => value.Value;
    }
}

namespace Cilica.Runtime
{
    // Compiler-selected only for the exact core Nullable<T>. Ordinary bodies
    // preserve empty/null behavior and reuse T's existing checked box operation.
    public static class NullableBoxing<T> where T : struct
    {
        public static object Box(System.Nullable<T> value)
        {
            if (!value.HasValue) return null;
            return (object)value.GetValueOrDefault();
        }
        public static System.Nullable<T> Unbox(object value)
        {
            if (value == null) return default(System.Nullable<T>);
            return new System.Nullable<T>((T)value);
        }
    }
}
