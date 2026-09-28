using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Cilica.Metadata.Tests;

/// <summary>
/// Builds a small assembly with hand-written method bodies, for IL shapes that C#
/// compilers never emit. Every method is a public static method of <c>Synthetic.Methods</c>
/// taking <c>int32</c> parameters. The assembly is written as bytes and never loaded.
/// </summary>
internal sealed class SyntheticAssembly
{
    private readonly MetadataBuilder _metadata = new();
    private readonly MethodBodyStreamEncoder _bodies = new(new BlobBuilder());
    private readonly TypeReferenceHandle _object;
    private MethodDefinitionHandle _firstMethod;

    public SyntheticAssembly()
    {
        _metadata.AddModule(0, _metadata.GetOrAddString("Synthetic.dll"), _metadata.GetOrAddGuid(new Guid(1, 2, 3, [4, 5, 6, 7, 8, 9, 10, 11])), default, default);
        _metadata.AddAssembly(_metadata.GetOrAddString("Synthetic"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        AssemblyReferenceHandle netstandard = _metadata.AddAssemblyReference(
            _metadata.GetOrAddString("netstandard"), new Version(2, 1, 0, 0), default,
            _metadata.GetOrAddBlob(new byte[] { 0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51 }), 0, default);
        _object = _metadata.AddTypeReference(netstandard, _metadata.GetOrAddString("System"), _metadata.GetOrAddString("Object"));
        Exception = _metadata.AddTypeReference(netstandard, _metadata.GetOrAddString("System"), _metadata.GetOrAddString("Exception"));
        _metadata.AddTypeDefinition(default, default, _metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
    }

    public TypeReferenceHandle Exception { get; }

    public const string TypeName = "Synthetic.Methods";

    /// <summary>Adds a method. <paramref name="emit"/> writes its instructions and exception regions.</summary>
    public MethodDefinitionHandle AddMethod(string name, Action<InstructionEncoder> emit, int parameters = 0, bool returnsInt = false, int maxStack = 8)
    {
        var il = new InstructionEncoder(new BlobBuilder(), new ControlFlowBuilder());
        emit(il);
        int offset = _bodies.AddMethodBody(il, maxStack, default, MethodBodyAttributes.None);

        var signature = new BlobBuilder();
        new BlobEncoder(signature).MethodSignature().Parameters(
            parameters,
            returnType => { if (returnsInt) returnType.Type().Int32(); else returnType.Void(); },
            parameterList => { for (int index = 0; index < parameters; index++) parameterList.AddParameter().Type().Int32(); });

        MethodDefinitionHandle handle = _metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            MethodImplAttributes.IL,
            _metadata.GetOrAddString(name),
            _metadata.GetOrAddBlob(signature),
            offset,
            MetadataTokens.ParameterHandle(1));
        if (_firstMethod.IsNil)
        {
            _firstMethod = handle;
        }
        return handle;
    }

    public byte[] Build()
    {
        _metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            _metadata.GetOrAddString("Synthetic"), _metadata.GetOrAddString("Methods"), _object,
            MetadataTokens.FieldDefinitionHandle(1), _firstMethod);
        var builder = new ManagedPEBuilder(
            PEHeaderBuilder.CreateLibraryHeader(),
            new MetadataRootBuilder(_metadata),
            _bodies.Builder,
            deterministicIdProvider: _ => new BlobContentId(new Guid(1, 2, 3, [4, 5, 6, 7, 8, 9, 10, 11]), 0x01020304));
        var blob = new BlobBuilder();
        builder.Serialize(blob);
        return blob.ToArray();
    }
}
