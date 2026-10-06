// Cilica-owned bounded Nullable<T>. Storage and accessors use ordinary managed
// value operations; special nullable boxing/unboxing remains a compiler gate.
namespace System
{
    public struct Nullable<T> where T : struct
    {
        private readonly bool hasValue;
        private readonly T value;

        public Nullable(T value) { this.value = value; hasValue = true; }
        public bool HasValue => hasValue;
        public T Value
        {
            get
            {
                if (!hasValue) throw new InvalidOperationException("Nullable object must have a value.");
                return value;
            }
        }
        public T GetValueOrDefault() => value;
        public T GetValueOrDefault(T defaultValue) => hasValue ? value : defaultValue;

        // Keep inherited/default value semantics from masquerading as nullable
        // semantics. These members advance with their own differential evidence.
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public override string ToString() => Cilica.Runtime.UnsupportedLibraryMember<string>.Invoke();
        public static implicit operator Nullable<T>(T value) => Cilica.Runtime.UnsupportedLibraryMember<Nullable<T>>.Invoke();
        public static explicit operator T(Nullable<T> value) => Cilica.Runtime.UnsupportedLibraryMember<T>.Invoke();
    }
}
