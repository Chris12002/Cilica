// Cilica-owned bounded SHA-256 provider surface. Lifecycle/null ordering follows
// pinned dotnet/runtime HashAlgorithm; the digest uses the FIPS 180-4 algorithm.
namespace System.Security.Cryptography
{
    // Only its IDisposable inheritance is admitted. Streaming transform members
    // remain unmapped and cannot silently use whole-message hashing semantics.
    public interface ICryptoTransform : IDisposable { }
    public abstract class HashAlgorithm : IDisposable, ICryptoTransform
    {
        private bool disposed;
        private readonly string objectName;
        // An owned bridge, not the protected framework subclass constructor.
        // Custom providers must not accidentally inherit SHA-256 behavior.
        protected HashAlgorithm(string objectName) { this.objectName = objectName; }
        public byte[] ComputeHash(byte[] buffer)
        {
            if (disposed) throw new ObjectDisposedException(objectName);
            if (buffer == null) throw new ArgumentNullException("buffer");
            return ComputeDigest(buffer);
        }
        public void Dispose() { disposed = true; }
        // Ordinary public abstract dispatch follows the existing Encoding pattern.
        // This helper is not mapped to a framework member.
        public abstract byte[] ComputeDigest(byte[] buffer);
    }
    public abstract class SHA256 : HashAlgorithm
    {
        protected SHA256() : base("System.Security.Cryptography.SHA256+Implementation") { }
        public static SHA256 Create() => new Implementation();
        public sealed class Implementation : SHA256
        {
            public Implementation() { }
            public override byte[] ComputeDigest(byte[] buffer) => Cilica.Library.ManagedSha256.Digest(buffer);
        }
    }
}
