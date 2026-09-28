using System.Runtime.CompilerServices;

// Forwarding Outer also forwards its nested types.
[assembly: TypeForwardedTo(typeof(Cilica.Fixtures.Basic.Point))]
[assembly: TypeForwardedTo(typeof(Cilica.Fixtures.Basic.Outer))]
