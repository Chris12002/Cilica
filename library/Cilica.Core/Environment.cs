// Cilica-owned single-managed-thread profile contract; see ADR AC0024.
namespace System
{
    public static class Environment
    {
        // Managed thread identities are scoped to the independent runtime instance.
        // Serialized host entry may use a different OS thread without creating a
        // second managed thread. Multi-threaded profiles must replace this core.
        public static int CurrentManagedThreadId => 1;
    }
}
