using System;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Formatting;

/// <summary>Formats literal field values and parameter defaults from the Constant table.</summary>
internal static class ConstantFormatter
{
    public static string Format(MetadataReader reader, ConstantHandle handle)
    {
        Constant constant = reader.GetConstant(handle);
        BlobReader blob = reader.GetBlobReader(constant.Value);
        object? value = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => blob.ReadBoolean(),
            ConstantTypeCode.Char => blob.ReadChar(),
            ConstantTypeCode.SByte => blob.ReadSByte(),
            ConstantTypeCode.Byte => blob.ReadByte(),
            ConstantTypeCode.Int16 => blob.ReadInt16(),
            ConstantTypeCode.UInt16 => blob.ReadUInt16(),
            ConstantTypeCode.Int32 => blob.ReadInt32(),
            ConstantTypeCode.UInt32 => blob.ReadUInt32(),
            ConstantTypeCode.Int64 => blob.ReadInt64(),
            ConstantTypeCode.UInt64 => blob.ReadUInt64(),
            ConstantTypeCode.Single => blob.ReadSingle(),
            ConstantTypeCode.Double => blob.ReadDouble(),
            ConstantTypeCode.String => blob.ReadUTF16(blob.Length),
            ConstantTypeCode.NullReference => null,
            _ => throw new BadImageFormatException($"Unknown constant type code {(int)constant.TypeCode}."),
        };
        return Invariant.Literal(value);
    }
}
