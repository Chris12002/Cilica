# ADR AC0132: Bounded SHA-256 provider

Recorded 2026-10-07 for AC7c.2.

Map selected SHA256, HashAlgorithm and ICryptoTransform declarations to owned
managed code. SHA256.Create returns an independent provider; ComputeHash(byte[])
uses Cilica.Library.ManagedSha256 without mutating caller input and returns an
independent 32-byte array. Public and IDisposable disposal share one idempotent
state transition. Disposed state is checked before the buffer null argument.
Dispose is the exact final virtual method required by the contract; AC8.35 keeps
its class-call null check even in null-only closures.

The selected disposed name is System.Security.Cryptography.SHA256+Implementation,
matching the pinned .NET 10.0.11 provider. Exception text/name follows AC8.34.
The lifecycle reference is dotnet/runtime v10.0.11 HashAlgorithm.cs:
https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/HashAlgorithm.cs
The small lifecycle adapter is Cilica-owned. The digest is the independent owned
FIPS 180-4 implementation recorded in AC0129; no native/platform provider is used.

Use an owned abstract ComputeDigest bridge, following the existing Encoding
pattern, and an owned HashAlgorithm constructor that takes its provider name.
Do not map the framework protected parameterless constructor: custom providers
must not accidentally inherit SHA behavior. ICryptoTransform admits IDisposable
inheritance only. Streaming/offset APIs, Hash state, custom HashAlgorithm/SHA256
subclasses and other algorithms remain diagnostic. No new intrinsic grants or
compiler/native profile versions are introduced by this library slice.

Seven provider probes supply 4,536 repeated native/.NET comparisons at O0/O2,
forced GC and ASan/UBSan: all digest bytes at block boundaries, separate instances,
fresh results, repeated hashing, idempotent direct/interface disposal, disposed
before null-buffer faults, null-receiver faults and malformed-surrogate UTF-8.
UTF-8 composition checks exactly one retained encoding singleton. Five unsupported
API/subclass probes fail closed. The public SHA256 probe advances only with native
comparison evidence. Full frozen fingerprint execution and AC8/AC9 remain open.

Review checked exact framework flags, initialization, final/abstract dispatch,
null and disposal ordering, ownership, independent arrays and fixed provider-name
scope. Evidence and build pins: artifacts/alchemy-sha256-provider. Production
limits, frame quotas and subprocess budgets are unchanged.

All 21 selected provider, algorithm and public-baseline checks pass. The public
baseline is 44 native controls and three retained diagnostic gaps.
