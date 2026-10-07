using System.Runtime.CompilerServices;

namespace System.Reflection
{
    public abstract class MemberInfo { protected MemberInfo() { } }
}

namespace System
{
    // Metadata shape matches the reference contract. Reflection members are
    // deliberately absent; callers must stay inside the admitted identity API.
    public abstract class Type : Reflection.MemberInfo
    {
        protected Type() { }
        public new Type GetType() => base.GetType();
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => handle.Resolve();
        public virtual RuntimeTypeHandle TypeHandle => new RuntimeTypeHandle(this);
        public static bool operator ==(Type left, Type right) => (object)left == (object)right;
        public static bool operator !=(Type left, Type right) => (object)left != (object)right;
        public override bool Equals(object other) => (object)this == other;
        public virtual bool Equals(Type other) => (object)this == (object)other;
        public override int GetHashCode() => Cilica.Runtime.TypeIdentity.Hash(this);
    }

    public struct RuntimeTypeHandle
    {
        // Runtime-coupled layout: a copied, precisely traced canonical Type.
        internal Type Value;
        internal RuntimeTypeHandle(Type value) { Value = value; }
        internal Type Resolve() => Value;
    }
}

namespace Cilica.Runtime
{
    internal sealed class RuntimeTypeObject : System.Type
    {
        private RuntimeTypeObject() { }
    }
    internal static class TypeIdentity
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int Hash(System.Type value);
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int ObjectHash(object value);
    }
    internal static class TypeKeyOperations
    {
        internal static bool Equal(ref System.Type left, System.Type right) => left == right;
        internal static int Hash(ref System.Type value) => value.GetHashCode();
    }
}

namespace System
{
    // Only a metadata operand in the admitted constant-array construction pattern.
    public struct RuntimeFieldHandle { }
}
namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void InitializeArray(System.Array array, System.RuntimeFieldHandle fieldHandle);
    }
}
