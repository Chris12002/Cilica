using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Reads the exact emitted mscorlib FieldRVA as data. Never loads candidate or Unity IL.
class InspectCasingData
{
    static int Main(string[] args)
    {
        const string name = "4E0B9E024FA510B6F03C92D95BB204E78CDC6E3FD2EC8D35787B7BC76F0655A0";
        using var stream = File.OpenRead(args[0]); using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        foreach (var handle in md.FieldDefinitions)
        {
            var field = md.GetFieldDefinition(handle);
            if (md.GetString(field.Name) != name) continue;
            int rva = field.GetRelativeVirtualAddress(); if (rva == 0) throw new Exception("No field data");
            var content = pe.GetSectionData(rva).GetContent(0, 2382);
            var data = new byte[2382]; content.CopyTo(data);
            string hash = Convert.ToHexString(SHA256.HashData(data));
            if (hash != name) throw new Exception("Field data hash differs from initializer identity");
            int index = 0x017f - 0x00e0;
            int value = data[index * 2] | (data[index * 2 + 1] << 8);
            Console.WriteLine(JsonSerializer.Serialize(new { assembly = Path.GetFullPath(args[0]),
                assembly_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))).ToLowerInvariant(),
                field = name, field_rva = rva, field_bytes = data.Length, field_sha256 = hash.ToLowerInvariant(),
                table_first_code_unit = 0x00e0, table_index = index, input_code_unit = 0x017f,
                output_code_unit = value, inspected_as_data_only = true }));
            return value == 0x017f ? 0 : 1;
        }
        throw new Exception("Exact casing initializer not found");
    }
}
