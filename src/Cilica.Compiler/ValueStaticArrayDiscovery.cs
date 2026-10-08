using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;

public sealed record StaticArrayInitialization(int TokenOffset, ObjectTypeId ArrayType, ImmutableArray<long> Values);

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ImmutableDictionary<int, StaticArrayInitialization> StaticArrays(Work work)
        {
            var result = ImmutableDictionary.CreateBuilder<int, StaticArrayInitialization>();
            var instructions = work.Input.Instructions;
            var boundaries = instructions.SelectMany(i => i.Targets).ToHashSet();
            foreach (var region in work.Regions)
            {
                boundaries.UnionWith(new[] { region.TryStart, region.TryEnd, region.HandlerStart, region.HandlerEnd });
                if (region.FilterStart is { } filter) boundaries.Add(filter);
            }
            for (int index = 0; index < instructions.Length; ++index)
            {
                var token = instructions[index];
                if (token.OpCode != "ldtoken" || (uint)(token.IntegerOperand ?? 0) >> 24 != 4) continue;
                int offset = token.Offset;
                if (index < 3 || index + 1 >= instructions.Length
                    || !instructions[index - 3].OpCode.StartsWith("ldc.i4", StringComparison.Ordinal)
                    || instructions[index - 2].OpCode != "newarr" || instructions[index - 1].OpCode != "dup"
                    || instructions[index + 1].OpCode != "call"
                    || instructions.Skip(index - 2).Take(4).Any(i => boundaries.Contains(i.Offset)))
                    Fail(work, "Field tokens require one uninterrupted constant/newarr/dup/ldtoken/InitializeArray sequence.", offset);
                var constant = instructions[index - 3];
                int count = constant.OpCode is "ldc.i4" or "ldc.i4.s" ? constant.IntegerOperand ?? -1
                    : constant.OpCode == "ldc.i4.m1" ? -1 : constant.OpCode[^1] - '0';
                if (count is < 1 or > 256) Fail(work, "Static array initialization supports 1 through 256 constant elements.", offset, DiagnosticIds.TranslationLimit);
                var allocation = instructions[index - 2];
                var element = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, allocation, 0x01, 0x02, 0x1b))), work, offset);
                var vector = Vector(element, work, offset);
                int width = vector.ArrayElement!.Kind switch {
                    ObjectArrayElementKind.Boolean or ObjectArrayElementKind.Byte => 1, ObjectArrayElementKind.Char => 2,
                    ObjectArrayElementKind.Int32 or ObjectArrayElementKind.Enum32 => 4, ObjectArrayElementKind.Int64 => 8, _ => 0 };
                if (width == 0) Fail(work, "Static array initialization requires Boolean, Byte, Char, Int32 or Int64 elements.", offset);
                CheckOperandTypeUses(work, token);
                var call = instructions[index + 1]; CheckOperandTypeUses(work, call);
                var target = Require(resolver.ResolveClosedCall(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, call, 0x06, 0x0a)), _metadata), work, call.Offset);
                if (target.DeclaringType.Definition != resolver.ResolveType(library, "System.Runtime.CompilerServices", "RuntimeHelpers").Value
                    || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0)
                    Fail(work, "Static array initialization requires the selected RuntimeHelpers declaration.", call.Offset);
                if (_metadata.Remaining == 0) Fail(work, "Static array declaration work limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                var declaration = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(declaration.MetadataMembersRead)) Fail(work, "Static array declaration work limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                if (declaration.DeclaredName != "InitializeArray" || declaration.OwnerGenericArity != 0 || declaration.IsReferenceAssembly
                    || declaration.HasBody || declaration.HasTypeInitializer || declaration.HasModuleInitializer
                    || declaration.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || declaration.Implementation != MethodImplAttributes.InternalCall || declaration.Signature.Header != 0
                    || declaration.Signature.GenericArity != 0 || declaration.Signature.Parameters.Count != 2
                    || declaration.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Void)
                    || declaration.Signature.Parameters[0] is not TypeSignature.Named array || array.Definition != resolver.ResolveType(library, "System", "Array").Value
                    || declaration.Signature.Parameters[1] is not TypeSignature.Named handle || handle.Kind != SignatureTypeKind.ValueType
                    || handle.Definition != resolver.ResolveType(library, "System", "RuntimeFieldHandle").Value)
                    Fail(work, "Static array initialization requires the exact bodyless owned helper signature.", call.Offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.StaticArrayInitialization), work, offset);
                var data = Require(resolver.ReadStaticFieldData(work.Assembly,
                    (FieldDefinitionHandle)MetadataTokens.EntityHandle(Token(work, token, 0x04)), checked(count * width),
                    Require(resolver.ResolveType(library, "System", "ValueType"), work, offset), _metadata), work, offset);
                var values = ImmutableArray.CreateBuilder<long>(count);
                for (int i = 0; i < count; ++i)
                {
                    var bytes = data.Bytes.AsSpan(i * width, width);
                    values.Add(width switch { 1 => bytes[0], 2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                        4 => BinaryPrimitives.ReadInt32LittleEndian(bytes), _ => BinaryPrimitives.ReadInt64LittleEndian(bytes) });
                }
                result.Add(call.Offset, new(token.Offset, vector, values.ToImmutable()));
            }
            return result.ToImmutable();
        }
    }
}
