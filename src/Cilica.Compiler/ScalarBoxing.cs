using System;
using System.Reflection.Metadata;

namespace Cilica.Compiler;

// Nominal box identity is separate from the evaluation-stack carrier. Only Int32
// and admitted Int32 enums share ordinary unbox compatibility; nullable is exact.
internal static class ScalarBoxing
{
    internal static bool Supported(PrimitiveTypeCode code) => code is PrimitiveTypeCode.Boolean or PrimitiveTypeCode.Char
        or PrimitiveTypeCode.SByte or PrimitiveTypeCode.Byte or PrimitiveTypeCode.Int16 or PrimitiveTypeCode.UInt16
        or PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64;
    internal static IrType Stack(PrimitiveTypeCode code) => code is PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 ? IrType.Int64 : IrType.Int32;
    internal static ObjectStorage Storage(PrimitiveTypeCode code) => Stack(code) == IrType.Int64 ? new ObjectStorage.Int64() : new ObjectStorage.Int32();
    internal static IntegerCastTarget? Narrow(PrimitiveTypeCode code) => IntegerCasts.NarrowStorage(new Cilica.Metadata.Resolution.TypeSignature.Primitive(code));
}
