using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.IL;

/// <summary>
/// Decodes and validates method bodies as data. The input is never loaded or run.
/// Malformed bodies produce diagnostics, not exceptions.
/// </summary>
public static class MethodBodyInspector
{
    /// <param name="methodFilter">Selects methods by their <c>Type::Name(parameter types)</c> name; null selects all.</param>
    public static ILReport Inspect(string path, Func<string, bool>? methodFilter = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        byte[]? image = AssemblyImage.ReadFile(path, out Diagnostic? failure);
        return image is null
            ? new ILReport(Path.GetFileName(path), [], [failure!])
            : Inspect(image, Path.GetFileName(path), methodFilter);
    }

    public static ILReport Inspect(byte[] image, string inputName, Func<string, bool>? methodFilter = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(inputName);
        using AssemblyImage? opened = AssemblyImage.Open(image, inputName, out Diagnostic? failure);
        if (opened is null)
        {
            return new ILReport(inputName, [], [failure!]);
        }
        return new Inspection(opened, methodFilter).Run();
    }

    private sealed class Inspection
    {
        private readonly AssemblyImage _image;
        private readonly MetadataReader _reader;
        private readonly NameFormatter _names;
        private readonly Func<string, bool>? _filter;
        private readonly List<Diagnostic> _diagnostics = [];

        public Inspection(AssemblyImage image, Func<string, bool>? filter)
        {
            _image = image;
            _reader = image.Reader;
            _names = new NameFormatter(image.Reader);
            _filter = filter;
        }

        public ILReport Run()
        {
            var methods = new List<(MethodBodyReport Report, int Token)>();
            foreach (TypeDefinitionHandle typeHandle in _reader.TypeDefinitions)
            {
                IEnumerable<MethodDefinitionHandle> handles = Guard(
                    () => _reader.GetTypeDefinition(typeHandle).GetMethods().ToList(),
                    DiagnosticIds.MalformedEntity, Token(typeHandle), []);
                foreach (MethodDefinitionHandle handle in handles)
                {
                    MethodBodyReport? report = Guard(() => ReadMethod(handle), DiagnosticIds.MalformedEntity, Token(handle), null);
                    if (report is not null)
                    {
                        methods.Add((report, MetadataTokens.GetToken(handle)));
                    }
                }
            }

            return new ILReport(
                _image.InputName,
                [.. methods.OrderBy(m => m.Report.Name, StringComparer.Ordinal).ThenBy(m => m.Token).Select(m => m.Report)],
                [.. _diagnostics
                    .OrderBy(d => d.Location ?? "", StringComparer.Ordinal)
                    .ThenBy(d => d.Id, StringComparer.Ordinal)
                    .ThenBy(d => d.Message, StringComparer.Ordinal)]);
        }

        private MethodBodyReport? ReadMethod(MethodDefinitionHandle handle)
        {
            MethodDefinition method = _reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
            {
                return null;
            }
            string name = _names.MethodName(handle);
            if (_filter is not null && !_filter(name))
            {
                return null;
            }

            var context = new GenericContext(
                _names.GenericParameterNames(_reader.GetTypeDefinition(method.GetDeclaringType()).GetGenericParameters()),
                _names.GenericParameterNames(method.GetGenericParameters()));
            void Report(string id, string message, int offset) =>
                _diagnostics.Add(new Diagnostic(id, DiagnosticSeverity.Error, message, name + " " + ILDecoder.Label(offset)));

            MethodBodyBlock body;
            try
            {
                body = _image.PEReader.GetMethodBody(method.RelativeVirtualAddress);
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                Report(DiagnosticIds.MalformedMethodBody, $"The method body header is malformed: {exception.Message}", 0);
                return new MethodBodyReport(name, 0, 0, false, [], [], [], []);
            }

            bool analyzable = true;
            ImmutableArray<string> locals = [];
            if (!body.LocalSignature.IsNil)
            {
                try
                {
                    StandaloneSignature signature = _reader.GetStandaloneSignature(body.LocalSignature);
                    if (signature.GetKind() != StandaloneSignatureKind.LocalVariables)
                    {
                        throw new BadImageFormatException("The local variable signature token refers to a method signature.");
                    }
                    locals = signature.DecodeLocalSignature(_names, context);
                }
                catch (Exception exception) when (MalformedInput.IsMalformed(exception))
                {
                    Report(DiagnosticIds.MalformedMethodBody, $"The local variable signature is malformed: {exception.Message}", 0);
                    analyzable = false;
                }
            }

            MethodSignature<bool>? shape = null;
            try
            {
                shape = method.DecodeSignature(VoidTypeProvider.Instance, null);
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                Report(DiagnosticIds.MalformedEntity, $"The method signature is malformed: {exception.Message}", 0);
                analyzable = false;
            }

            // MethodBodyBlock.Size also counts the body header and exception tables; IL offsets use the code length.
            BlobReader il = body.GetILReader();
            int codeSize = il.Length;
            List<Instruction> instructions = ILDecoder.Decode(il, out string? decodeError);
            if (decodeError is not null)
            {
                Report(DiagnosticIds.MalformedMethodBody, decodeError, instructions.Count == 0 ? 0 : instructions[^1].End);
                analyzable = false;
            }

            ImmutableArray<InstructionReport> instructionReports =
                [.. instructions.Select(i => new InstructionReport(i.Offset, i.Code.Name, OperandText(i, context, Report)))];

            ImmutableArray<BasicBlockReport> blocks = [];
            ImmutableDictionary<int, string> filterKinds = ImmutableDictionary<int, string>.Empty;
            if (analyzable && shape is MethodSignature<bool> signatureShape)
            {
                int argumentCount = signatureShape.ParameterTypes.Length
                    + (signatureShape.Header.IsInstance && !signatureShape.Header.HasExplicitThis ? 1 : 0);
                var analyzer = new ControlFlowAnalyzer(
                    instructions,
                    codeSize,
                    body.MaxStack,
                    argumentCount,
                    locals.Length,
                    returnsValue: !signatureShape.ReturnType,
                    CallShapeOf,
                    Report);
                ControlFlowAnalyzer.Result result = analyzer.Analyze(body.ExceptionRegions);
                blocks = result.Blocks;
                filterKinds = result.FilterKinds;
            }

            ImmutableArray<ExceptionRegionReport> regions = [.. body.ExceptionRegions.Select((region, index) => new ExceptionRegionReport(
                region.Kind switch
                {
                    ExceptionRegionKind.Catch => "catch",
                    ExceptionRegionKind.Filter => "filter",
                    ExceptionRegionKind.Finally => "finally",
                    ExceptionRegionKind.Fault => "fault",
                    _ => "unknown",
                },
                region.TryOffset,
                region.TryOffset + region.TryLength,
                region.HandlerOffset,
                region.HandlerOffset + region.HandlerLength,
                region.Kind == ExceptionRegionKind.Filter ? region.FilterOffset : null,
                region.Kind == ExceptionRegionKind.Catch ? CatchTypeText(region, context, Report) : null,
                filterKinds.GetValueOrDefault(index)))];

            return new MethodBodyReport(name, codeSize, body.MaxStack, body.LocalVariablesInitialized, locals, regions, blocks, instructionReports);
        }

        private string CatchTypeText(ExceptionRegion region, GenericContext context, Action<string, string, int> report)
        {
            try
            {
                return _names.TypeName(Resolve(MetadataTokens.GetToken(region.CatchType), HandleKind.TypeDefinition, HandleKind.TypeReference, HandleKind.TypeSpecification), context);
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                report(DiagnosticIds.InvalidExceptionRegion, $"The catch type is invalid: {exception.Message}", region.TryOffset);
                return "<invalid catch type>";
            }
        }

        private string? OperandText(Instruction instruction, GenericContext context, Action<string, string, int> report)
        {
            try
            {
                return instruction.Code.Operand switch
                {
                    OperandKind.None => null,
                    OperandKind.ShortBranch or OperandKind.Branch => ILDecoder.Label(instruction.Targets[0]),
                    OperandKind.Switch => "(" + string.Join(", ", instruction.Targets.Select(ILDecoder.Label)) + ")",
                    OperandKind.MethodToken => _names.MethodName(
                        Resolve(instruction.Token, HandleKind.MethodDefinition, HandleKind.MemberReference, HandleKind.MethodSpecification), context),
                    OperandKind.FieldToken => _names.FieldName(
                        Resolve(instruction.Token, HandleKind.FieldDefinition, HandleKind.MemberReference), context),
                    OperandKind.TypeToken => _names.TypeName(
                        Resolve(instruction.Token, HandleKind.TypeDefinition, HandleKind.TypeReference, HandleKind.TypeSpecification), context),
                    OperandKind.StringToken => UserString(instruction.Token),
                    OperandKind.SignatureToken => _names.CallSiteSignature(
                        (StandaloneSignatureHandle)Resolve(instruction.Token, HandleKind.StandaloneSignature), context),
                    OperandKind.AnyToken => AnyTokenText(instruction.Token, context),
                    _ => Invariant.Literal(instruction.Operand),
                };
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                report(DiagnosticIds.InvalidOperand, $"{instruction.Code.Name} has an invalid operand: {exception.Message}", instruction.Offset);
                return "<invalid 0x" + ((int)instruction.Operand!).ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + ">";
            }
        }

        private string AnyTokenText(int token, GenericContext context)
        {
            EntityHandle handle = Resolve(token,
                HandleKind.TypeDefinition, HandleKind.TypeReference, HandleKind.TypeSpecification,
                HandleKind.MethodDefinition, HandleKind.MethodSpecification, HandleKind.FieldDefinition, HandleKind.MemberReference);
            return handle.Kind switch
            {
                HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification => _names.TypeName(handle, context),
                HandleKind.FieldDefinition => "field " + _names.FieldName(handle, context),
                HandleKind.MemberReference when _reader.GetMemberReference((MemberReferenceHandle)handle).GetKind() == MemberReferenceKind.Field =>
                    "field " + _names.FieldName(handle, context),
                _ => "method " + _names.MethodName(handle, context),
            };
        }

        private string UserString(int token)
        {
            int offset = token & 0x00FFFFFF;
            if ((token >>> 24) != 0x70 || offset == 0 || offset >= _reader.GetHeapSize(HeapIndex.UserString))
            {
                throw new BadImageFormatException($"0x{token:x8} is not a valid user string token.");
            }
            return Invariant.Quote(_reader.GetUserString(MetadataTokens.UserStringHandle(offset)), '"');
        }

        /// <summary>Resolves a metadata token, checking its table and row range.</summary>
        private EntityHandle Resolve(int token, params HandleKind[] allowed)
        {
            EntityHandle handle;
            try
            {
                // Tokens with the high bit set would become virtual handles, which never appear in IL.
                if (token < 0)
                {
                    throw new ArgumentException("Virtual token.", nameof(token));
                }
                handle = MetadataTokens.EntityHandle(token);
            }
            catch (ArgumentException)
            {
                throw new BadImageFormatException($"0x{token:x8} is not a metadata table token.");
            }
            if (!allowed.Contains(handle.Kind))
            {
                throw new BadImageFormatException($"0x{token:x8} refers to a {handle.Kind}, which is not valid here.");
            }
            int row = MetadataTokens.GetRowNumber(handle);
            if (!MetadataTokens.TryGetTableIndex(handle.Kind, out TableIndex table) || row == 0 || row > _reader.GetTableRowCount(table))
            {
                throw new BadImageFormatException($"0x{token:x8} refers to a row that does not exist.");
            }
            return handle;
        }

        private CallShape? CallShapeOf(Instruction instruction)
        {
            try
            {
                MethodSignature<bool> signature;
                if (instruction.Code.Operand == OperandKind.SignatureToken)
                {
                    var handle = (StandaloneSignatureHandle)Resolve(instruction.Token, HandleKind.StandaloneSignature);
                    signature = _reader.GetStandaloneSignature(handle).DecodeMethodSignature(VoidTypeProvider.Instance, null);
                    int thisCount = signature.Header.IsInstance && !signature.Header.HasExplicitThis ? 1 : 0;
                    return new CallShape(signature.ParameterTypes.Length + thisCount + 1, signature.ReturnType ? 0 : 1);
                }

                EntityHandle method = Resolve(instruction.Token, HandleKind.MethodDefinition, HandleKind.MemberReference, HandleKind.MethodSpecification);
                if (method.Kind == HandleKind.MethodSpecification)
                {
                    method = _reader.GetMethodSpecification((MethodSpecificationHandle)method).Method;
                }
                signature = method.Kind switch
                {
                    HandleKind.MethodDefinition => _reader.GetMethodDefinition((MethodDefinitionHandle)method).DecodeSignature(VoidTypeProvider.Instance, null),
                    HandleKind.MemberReference => _reader.GetMemberReference((MemberReferenceHandle)method).DecodeMethodSignature(VoidTypeProvider.Instance, null),
                    _ => throw new BadImageFormatException("A method specification must refer to a method definition or reference."),
                };
                int parameters = signature.ParameterTypes.Length;
                if (instruction.Code.Name == "newobj")
                {
                    return new CallShape(parameters, 1);
                }
                int receiver = signature.Header.IsInstance && !signature.Header.HasExplicitThis ? 1 : 0;
                return new CallShape(parameters + receiver, signature.ReturnType ? 0 : 1);
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                // The invalid operand is already reported when it is formatted.
                return null;
            }
        }

        private T Guard<T>(Func<T> read, string id, string location, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                _diagnostics.Add(new Diagnostic(id, DiagnosticSeverity.Error, $"Malformed metadata: {exception.Message}", location));
                return fallback;
            }
        }

        private static string Token(EntityHandle handle) =>
            "0x" + MetadataTokens.GetToken(handle).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Decodes a signature only far enough to tell whether each type is void.</summary>
    private sealed class VoidTypeProvider : ISignatureTypeProvider<bool, object?>
    {
        public static VoidTypeProvider Instance { get; } = new();

        public bool GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode == PrimitiveTypeCode.Void;
        public bool GetModifiedType(bool modifier, bool unmodifiedType, bool isRequired) => unmodifiedType;
        public bool GetArrayType(bool elementType, ArrayShape shape) => false;
        public bool GetByReferenceType(bool elementType) => false;
        public bool GetFunctionPointerType(MethodSignature<bool> signature) => false;
        public bool GetGenericInstantiation(bool genericType, ImmutableArray<bool> typeArguments) => false;
        public bool GetGenericMethodParameter(object? genericContext, int index) => false;
        public bool GetGenericTypeParameter(object? genericContext, int index) => false;
        public bool GetPinnedType(bool elementType) => false;
        public bool GetPointerType(bool elementType) => false;
        public bool GetSZArrayType(bool elementType) => false;
        public bool GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => false;
        public bool GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => false;
        public bool GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => false;
    }
}
