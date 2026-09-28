using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata;

/// <summary>
/// Reads an assembly's metadata as data. The input is never loaded into the
/// runtime or executed. Malformed input produces diagnostics, not exceptions.
/// </summary>
public static class AssemblyInspector
{
    public static AssemblyReport Inspect(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        byte[]? image = AssemblyImage.ReadFile(path, out Diagnostic? failure);
        return image is null
            ? AssemblyReport.Failed(Path.GetFileName(path), failure!)
            : Inspect(image, Path.GetFileName(path));
    }

    /// <param name="inputName">Name used in the report and diagnostics; never an absolute path.</param>
    public static AssemblyReport Inspect(byte[] image, string inputName)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(inputName);

        using AssemblyImage? opened = AssemblyImage.Open(image, inputName, out Diagnostic? failure);
        return opened is null
            ? AssemblyReport.Failed(inputName, failure!)
            : new Inspection(inputName, opened.CorHeader, opened.Reader).Run();
    }

    private sealed class Inspection
    {
        private static readonly StringComparer Ordinal = StringComparer.Ordinal;

        private readonly string _inputName;
        private readonly CorHeader _corHeader;
        private readonly MetadataReader _reader;
        private readonly NameFormatter _names;
        private readonly AttributeFormatter _attributes;
        private readonly List<Diagnostic> _diagnostics = [];

        public Inspection(string inputName, CorHeader corHeader, MetadataReader reader)
        {
            _inputName = inputName;
            _corHeader = corHeader;
            _reader = reader;
            _names = new NameFormatter(reader);
            _attributes = new AttributeFormatter(reader, _names);
        }

        public AssemblyReport Run()
        {
            ImmutableArray<string> imageFlags = ImageFlags(_corHeader.Flags);
            if ((_corHeader.Flags & CorFlags.ILOnly) == 0)
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticIds.ContainsNativeCode, DiagnosticSeverity.Warning,
                    $"'{_inputName}' contains native code (a mixed-mode image). It can be inspected but not compiled."));
            }

            AssemblyIdentityReport? identity = null;
            var attributes = new List<string>();
            if (_reader.IsAssembly)
            {
                AssemblyDefinition assembly = _reader.GetAssemblyDefinition();
                identity = Guard<AssemblyIdentityReport?>(() => Identity(assembly.Name, assembly.Version, assembly.Culture, assembly.PublicKey, isToken: false), "assembly manifest", null);
                attributes.AddRange(Attributes(assembly.GetCustomAttributes(), "assembly manifest"));
            }
            else
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticIds.NoAssemblyManifest, DiagnosticSeverity.Warning,
                    $"'{_inputName}' has no assembly manifest (it is a .netmodule). Multi-module assemblies are not supported."));
            }

            ModuleDefinition module = _reader.GetModuleDefinition();
            string? moduleName = Guard<string?>(() => _reader.GetString(module.Name), "module", null);
            attributes.AddRange(Attributes(module.GetCustomAttributes(), "module", prefix: "module: "));

            string? targetFramework = _reader.IsAssembly ? FindTargetFramework() : null;
            if (_reader.IsAssembly && IsReferenceAssembly())
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticIds.ReferenceAssembly, DiagnosticSeverity.Info,
                    $"'{_inputName}' is a reference assembly: it describes an API surface, and its method bodies are not the implementation."));
            }

            return new AssemblyReport(
                _inputName,
                identity,
                moduleName,
                targetFramework,
                imageFlags,
                EntryPoint(),
                [.. attributes.Order(Ordinal)],
                References(),
                ExportedTypes(),
                Types(),
                [.. _diagnostics
                    .OrderBy(d => d.Id, Ordinal)
                    .ThenBy(d => d.Location ?? "", Ordinal)
                    .ThenBy(d => d.Message, Ordinal)]);
        }

        private static ImmutableArray<string> ImageFlags(CorFlags flags)
        {
            var result = new List<string>();
            if ((flags & CorFlags.ILOnly) != 0) result.Add("il-only");
            if ((flags & CorFlags.Requires32Bit) != 0) result.Add("32-bit-required");
            if ((flags & CorFlags.Prefers32Bit) != 0) result.Add("32-bit-preferred");
            if ((flags & CorFlags.StrongNameSigned) != 0) result.Add("strong-name-signed");
            if ((flags & CorFlags.NativeEntryPoint) != 0) result.Add("native-entry-point");
            return [.. result];
        }

        private AssemblyIdentityReport Identity(StringHandle name, Version version, StringHandle culture, BlobHandle key, bool isToken)
        {
            string cultureName = _reader.GetString(culture);
            string? token = null;
            if (!key.IsNil)
            {
                byte[] bytes = _reader.GetBlobBytes(key);
                if (bytes.Length > 0)
                {
                    token = Invariant.Hex(isToken ? bytes : PublicKeyToken(bytes));
                }
            }
            return new AssemblyIdentityReport(
                _reader.GetString(name),
                version.ToString(),
                cultureName.Length == 0 ? "neutral" : cultureName,
                token);
        }

        /// <summary>ECMA-335 II.6.2.1.3: the last eight bytes of the key's SHA-1 hash, reversed.</summary>
        private static byte[] PublicKeyToken(byte[] publicKey)
        {
#pragma warning disable CA5350 // SHA-1 is mandated by the public key token format, not used for security.
            byte[] hash = SHA1.HashData(publicKey);
#pragma warning restore CA5350
            byte[] token = hash[^8..];
            Array.Reverse(token);
            return token;
        }

        private string? FindTargetFramework()
        {
            foreach (CustomAttributeHandle handle in _reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                string? framework = Guard<string?>(() =>
                {
                    CustomAttribute attribute = _reader.GetCustomAttribute(handle);
                    if (_names.AttributeTypeName(attribute.Constructor) is not ("[netstandard]System.Runtime.Versioning.TargetFrameworkAttribute"
                        or "[System.Runtime]System.Runtime.Versioning.TargetFrameworkAttribute"
                        or "[mscorlib]System.Runtime.Versioning.TargetFrameworkAttribute"
                        or "System.Runtime.Versioning.TargetFrameworkAttribute"))
                    {
                        return null;
                    }
                    BlobReader blob = _reader.GetBlobReader(attribute.Value);
                    if (blob.ReadUInt16() != 1)
                    {
                        throw new BadImageFormatException("Invalid custom attribute prolog.");
                    }
                    return blob.ReadSerializedString();
                }, "assembly manifest", null);
                if (framework is not null)
                {
                    return framework;
                }
            }
            return null;
        }

        private bool IsReferenceAssembly() =>
            _reader.GetAssemblyDefinition().GetCustomAttributes().Any(handle => Guard(() =>
                _names.AttributeTypeName(_reader.GetCustomAttribute(handle).Constructor)
                    .EndsWith("]System.Runtime.CompilerServices.ReferenceAssemblyAttribute", StringComparison.Ordinal),
                "assembly manifest", false));

        private string? EntryPoint()
        {
            if ((_corHeader.Flags & CorFlags.NativeEntryPoint) != 0)
            {
                return "native code";
            }
            int token = _corHeader.EntryPointTokenOrRelativeVirtualAddress;
            if (token == 0)
            {
                return null;
            }
            return Guard(() =>
            {
                int row = token & 0x00FFFFFF;
                // A managed entry point in our single-module profile must be a MethodDef.
                // Check before constructing a handle: invalid token tags can throw ArgumentException.
                if ((token >>> 24) != 0x06 || row == 0 || row > _reader.GetTableRowCount(TableIndex.MethodDef))
                {
                    throw new BadImageFormatException($"Entry point 0x{token:x8} is not a valid method definition token.");
                }
                return _names.MethodName(MetadataTokens.MethodDefinitionHandle(row));
            }, "entry point", "<malformed entry point>");
        }

        private ImmutableArray<AssemblyIdentityReport> References()
        {
            var references = new List<AssemblyIdentityReport>();
            foreach (AssemblyReferenceHandle handle in _reader.AssemblyReferences)
            {
                AssemblyIdentityReport? reference = Guard<AssemblyIdentityReport?>(() =>
                {
                    AssemblyReference assembly = _reader.GetAssemblyReference(handle);
                    bool isToken = (assembly.Flags & AssemblyFlags.PublicKey) == 0;
                    return Identity(assembly.Name, assembly.Version, assembly.Culture, assembly.PublicKeyOrToken, isToken);
                }, Token(handle), null);
                if (reference is not null)
                {
                    references.Add(reference);
                }
            }
            return [.. references.OrderBy(r => r.Name, Ordinal).ThenBy(r => r.DisplayName, Ordinal)];
        }

        private ImmutableArray<ExportedTypeReport> ExportedTypes()
        {
            var exported = new List<ExportedTypeReport>();
            foreach (ExportedTypeHandle handle in _reader.ExportedTypes)
            {
                ExportedTypeReport? report = Guard<ExportedTypeReport?>(() =>
                {
                    ExportedType type = _reader.GetExportedType(handle);
                    (string target, bool isForwarder) = ExportedTypeTarget(type);
                    return new ExportedTypeReport(_names.ExportedTypeName(handle), target, isForwarder);
                }, Token(handle), null);
                if (report is not null)
                {
                    exported.Add(report);
                }
            }
            return [.. exported.OrderBy(e => e.Name, Ordinal)];
        }

        private (string Target, bool IsForwarder) ExportedTypeTarget(ExportedType type)
        {
            // Nested exported types point at their declaring exported type. Only the root carries
            // the forwarder flag, and its implementation names the target.
            ExportedType root = type;
            for (int depth = 0; root.Implementation.Kind == HandleKind.ExportedType; depth++)
            {
                if (depth > 64)
                {
                    throw new BadImageFormatException("Exported type nesting is too deep or cyclic.");
                }
                root = _reader.GetExportedType((ExportedTypeHandle)root.Implementation);
            }
            EntityHandle implementation = root.Implementation;
            string target = implementation.Kind switch
            {
                HandleKind.AssemblyReference => _reader.GetString(_reader.GetAssemblyReference((AssemblyReferenceHandle)implementation).Name),
                HandleKind.AssemblyFile => "file " + _reader.GetString(_reader.GetAssemblyFile((AssemblyFileHandle)implementation).Name),
                _ => throw new BadImageFormatException($"Unexpected exported type implementation {implementation.Kind}."),
            };
            return (target, root.IsForwarder);
        }

        private ImmutableArray<TypeReport> Types()
        {
            var types = new List<TypeReport>();
            foreach (TypeDefinitionHandle handle in _reader.TypeDefinitions)
            {
                TypeReport? type = ReadType(handle);
                if (type is not null)
                {
                    types.Add(type);
                }
            }
            return [.. types.OrderBy(t => t.Name, Ordinal)];
        }

        private TypeReport? ReadType(TypeDefinitionHandle handle) =>
            Guard(() => ReadTypeCore(handle), Token(handle), new TypeReport(
                "<malformed type " + Token(handle) + ">", "unknown", "unknown", [], null, null, [], [], [], [], [], [], [], []));

        private TypeReport? ReadTypeCore(TypeDefinitionHandle handle)
        {
            TypeDefinition definition = _reader.GetTypeDefinition(handle);
            string name = Guard(() => _names.TypeDefinitionName(handle), Token(handle), "<malformed name " + Token(handle) + ">");
            string location = name;

            // The <Module> pseudo-type holds global members; omit it when it has none.
            if (name == "<Module>" && definition.GetFields().Count == 0 && definition.GetMethods().Count == 0)
            {
                return null;
            }

            TypeAttributes attributes = definition.Attributes;
            var genericContext = new GenericContext(
                Guard(() => _names.GenericParameterNames(definition.GetGenericParameters()), location, ImmutableArray<string>.Empty),
                []);

            string? baseType = definition.BaseType.IsNil
                ? null
                : Guard(() => _names.TypeName(definition.BaseType, genericContext), location, "<malformed base type>");

            return new TypeReport(
                name,
                TypeKind(name, attributes, definition.BaseType),
                TypeVisibility(attributes),
                TypeFlags(attributes),
                Guard(() => Layout(definition), location, null),
                baseType,
                [.. definition.GetInterfaceImplementations()
                    .Select(i => Guard(() => _names.TypeName(_reader.GetInterfaceImplementation(i).Interface, genericContext), location, "<malformed interface>"))
                    .Order(Ordinal)],
                GenericParameters(definition.GetGenericParameters(), genericContext, location),
                Attributes(definition.GetCustomAttributes(), location),
                [.. definition.GetFields().Select(f => Guard(() => ReadField(f, genericContext, name), location,
                    new FieldReport("<malformed field " + Token(f) + ">", "<malformed>", "unknown", [], null, null, [])))],
                [.. definition.GetMethods().Select(m => Guard(() => ReadMethod(m, genericContext, name), location,
                    new MethodReport("<malformed method " + Token(m) + ">", "<malformed>", "unknown", [], null, false, [])))
                    .OrderBy(m => m.Name, Ordinal).ThenBy(m => m.Signature, Ordinal)],
                [.. definition.GetProperties().Select(p => Guard(() => ReadProperty(p, genericContext, name), location,
                    new PropertyReport("<malformed property " + Token(p) + ">", "<malformed>", [], [])))
                    .OrderBy(p => p.Name, Ordinal).ThenBy(p => p.Signature, Ordinal)],
                [.. definition.GetEvents().Select(e => Guard(() => ReadEvent(e, genericContext, name), location,
                    new EventReport("<malformed event " + Token(e) + ">", "<malformed>", [], [])))
                    .OrderBy(e => e.Name, Ordinal).ThenBy(e => e.Type, Ordinal)],
                [.. definition.GetMethodImplementations()
                    .Select(i => Guard(() =>
                    {
                        MethodImplementation implementation = _reader.GetMethodImplementation(i);
                        return _names.MethodName(implementation.MethodBody) + " overrides " + _names.MethodName(implementation.MethodDeclaration);
                    }, location, "<malformed override>"))
                    .Order(Ordinal)]);
        }

        private string TypeKind(string name, TypeAttributes attributes, EntityHandle baseType)
        {
            if ((attributes & TypeAttributes.Interface) != 0)
            {
                return "interface";
            }
            string? baseName = baseType.IsNil ? null : Guard(() => UnscopedName(baseType), name, null);
            return baseName switch
            {
                "System.Enum" when name != "System.ValueType" => "enum",
                "System.ValueType" when name != "System.Enum" => "struct",
                "System.MulticastDelegate" when name != "System.Delegate" => "delegate",
                _ => "class",
            };
        }

        /// <summary>A TypeDef or TypeRef name without its resolution scope, used to classify base types.</summary>
        private string? UnscopedName(EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    TypeDefinition definition = _reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return _reader.GetString(definition.Namespace) + "." + _reader.GetString(definition.Name);
                case HandleKind.TypeReference:
                    TypeReference reference = _reader.GetTypeReference((TypeReferenceHandle)handle);
                    return _reader.GetString(reference.Namespace) + "." + _reader.GetString(reference.Name);
                default:
                    return null;
            }
        }

        private static string TypeVisibility(TypeAttributes attributes) => (attributes & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.Public or TypeAttributes.NestedPublic => "public",
            TypeAttributes.NotPublic or TypeAttributes.NestedAssembly => "internal",
            TypeAttributes.NestedPrivate => "private",
            TypeAttributes.NestedFamily => "protected",
            TypeAttributes.NestedFamANDAssem => "private protected",
            TypeAttributes.NestedFamORAssem => "protected internal",
            _ => "unknown",
        };

        private static ImmutableArray<string> TypeFlags(TypeAttributes attributes)
        {
            var flags = new List<string>();
            if ((attributes & TypeAttributes.Abstract) != 0 && (attributes & TypeAttributes.Interface) == 0) flags.Add("abstract");
            if ((attributes & TypeAttributes.Sealed) != 0) flags.Add("sealed");
            if ((attributes & TypeAttributes.BeforeFieldInit) != 0) flags.Add("beforefieldinit");
            if ((attributes & TypeAttributes.SpecialName) != 0) flags.Add("specialname");
            if ((attributes & TypeAttributes.RTSpecialName) != 0) flags.Add("rtspecialname");
            if ((attributes & TypeAttributes.Import) != 0) flags.Add("import");
#pragma warning disable SYSLIB0050 // The flag is still present in metadata even though formatter-based serialization is obsolete.
            if ((attributes & TypeAttributes.Serializable) != 0) flags.Add("serializable");
#pragma warning restore SYSLIB0050
            return [.. flags];
        }

        private static string? Layout(TypeDefinition definition)
        {
            string? kind = (definition.Attributes & TypeAttributes.LayoutMask) switch
            {
                TypeAttributes.SequentialLayout => "sequential",
                TypeAttributes.ExplicitLayout => "explicit",
                _ => null,
            };
            TypeLayout layout = definition.GetLayout();
            var parts = new List<string>();
            if (kind is not null) parts.Add(kind);
            if (layout.PackingSize != 0) parts.Add("pack " + Invariant.Format(layout.PackingSize));
            if (layout.Size != 0) parts.Add("size " + Invariant.Format(layout.Size));
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        private ImmutableArray<GenericParameterReport> GenericParameters(GenericParameterHandleCollection parameters, GenericContext context, string location) =>
            [.. parameters.Select(handle => Guard(() =>
            {
                GenericParameter parameter = _reader.GetGenericParameter(handle);
                GenericParameterAttributes attributes = parameter.Attributes;
                string? variance = (attributes & GenericParameterAttributes.VarianceMask) switch
                {
                    GenericParameterAttributes.Covariant => "out",
                    GenericParameterAttributes.Contravariant => "in",
                    _ => null,
                };
                var constraints = new List<string>();
                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Add("class");
                if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Add("valuetype");
                if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) constraints.Add("new()");
                if ((attributes & GenericParameterAttributes.AllowByRefLike) != 0) constraints.Add("byreflike");
                constraints.AddRange(parameter.GetConstraints()
                    .Select(c => _names.TypeName(_reader.GetGenericParameterConstraint(c).Type, context))
                    .Order(Ordinal));
                return new GenericParameterReport(_reader.GetString(parameter.Name), variance, [.. constraints]);
            }, location, new GenericParameterReport("<malformed>", null, [])))];

        private FieldReport ReadField(FieldDefinitionHandle handle, GenericContext context, string typeName)
        {
            FieldDefinition field = _reader.GetFieldDefinition(handle);
            string name = Guard(() => _reader.GetString(field.Name), typeName + " " + Token(handle), "<malformed name>");
            string location = typeName + "::" + name;
            FieldAttributes attributes = field.Attributes;

            var flags = new List<string>();
            if ((attributes & FieldAttributes.Static) != 0) flags.Add("static");
            if ((attributes & FieldAttributes.InitOnly) != 0) flags.Add("initonly");
            if ((attributes & FieldAttributes.Literal) != 0) flags.Add("literal");
            if ((attributes & FieldAttributes.SpecialName) != 0) flags.Add("specialname");
            if ((attributes & FieldAttributes.RTSpecialName) != 0) flags.Add("rtspecialname");
            if ((attributes & FieldAttributes.HasFieldRVA) != 0) flags.Add("hasfieldrva");
#pragma warning disable SYSLIB0050 // The flag is still present in metadata even though formatter-based serialization is obsolete.
            if ((attributes & FieldAttributes.NotSerialized) != 0) flags.Add("notserialized");
#pragma warning restore SYSLIB0050

            ConstantHandle constant = field.GetDefaultValue();
            int offset = Guard(() => field.GetOffset(), location, -1);
            return new FieldReport(
                name,
                Guard(() => field.DecodeSignature(_names, context), location, "<malformed signature>"),
                MemberVisibility((MethodAttributes)(int)(attributes & FieldAttributes.FieldAccessMask)),
                [.. flags],
                constant.IsNil ? null : Guard(() => ConstantFormatter.Format(_reader, constant), location, "<malformed constant>"),
                offset < 0 ? null : offset,
                Attributes(field.GetCustomAttributes(), location));
        }

        private MethodReport ReadMethod(MethodDefinitionHandle handle, GenericContext typeContext, string typeName)
        {
            MethodDefinition method = _reader.GetMethodDefinition(handle);
            string name = Guard(() => _reader.GetString(method.Name), typeName + " " + Token(handle), "<malformed name>");
            string location = typeName + "::" + name + " " + Token(handle);
            MethodAttributes attributes = method.Attributes;
            MethodImplAttributes implementation = method.ImplAttributes;

            var flags = new List<string>();
            if ((attributes & MethodAttributes.Static) != 0) flags.Add("static");
            if ((attributes & MethodAttributes.Abstract) != 0) flags.Add("abstract");
            if ((attributes & MethodAttributes.Virtual) != 0) flags.Add("virtual");
            if ((attributes & MethodAttributes.Final) != 0) flags.Add("final");
            if ((attributes & MethodAttributes.NewSlot) != 0) flags.Add("newslot");
            if ((attributes & MethodAttributes.SpecialName) != 0) flags.Add("specialname");
            if ((attributes & MethodAttributes.RTSpecialName) != 0) flags.Add("rtspecialname");
            if ((attributes & MethodAttributes.PinvokeImpl) != 0) flags.Add("pinvokeimpl");
            switch (implementation & MethodImplAttributes.CodeTypeMask)
            {
                case MethodImplAttributes.Native: flags.Add("native"); break;
                case MethodImplAttributes.OPTIL: flags.Add("optil"); break;
                case MethodImplAttributes.Runtime: flags.Add("runtime"); break;
            }
            if ((implementation & MethodImplAttributes.Unmanaged) != 0) flags.Add("unmanaged");
            if ((implementation & MethodImplAttributes.InternalCall) != 0) flags.Add("internalcall");
            if ((implementation & MethodImplAttributes.Synchronized) != 0) flags.Add("synchronized");
            if ((implementation & MethodImplAttributes.NoInlining) != 0) flags.Add("noinlining");
            if ((implementation & MethodImplAttributes.AggressiveInlining) != 0) flags.Add("aggressiveinlining");
            if ((implementation & MethodImplAttributes.NoOptimization) != 0) flags.Add("nooptimization");
            if ((implementation & MethodImplAttributes.AggressiveOptimization) != 0) flags.Add("aggressiveoptimization");
            if ((implementation & MethodImplAttributes.PreserveSig) != 0) flags.Add("preservesig");
            if ((implementation & MethodImplAttributes.ForwardRef) != 0) flags.Add("forwardref");

            string? import = null;
            if ((attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                import = Guard(() =>
                {
                    MethodImport methodImport = method.GetImport();
                    string module = _reader.GetString(_reader.GetModuleReference(methodImport.Module).Name);
                    return Invariant.Quote(module, '"') + " " + _reader.GetString(methodImport.Name);
                }, location, "<malformed import>");
            }

            return new MethodReport(
                name,
                Guard(() => MethodSignature(method, name, typeContext), location, name + " <malformed signature>"),
                MemberVisibility(attributes & MethodAttributes.MemberAccessMask),
                [.. flags],
                import,
                method.RelativeVirtualAddress != 0,
                Attributes(method.GetCustomAttributes(), location));
        }

        private string MethodSignature(MethodDefinition method, string name, GenericContext typeContext)
        {
            ImmutableArray<string> methodGenerics = _names.GenericParameterNames(method.GetGenericParameters());
            var context = typeContext with { MethodParameters = methodGenerics };
            MethodSignature<string> signature = method.DecodeSignature(_names, context);

            var parameters = new Dictionary<int, Parameter>();
            foreach (ParameterHandle handle in method.GetParameters())
            {
                Parameter parameter = _reader.GetParameter(handle);
                parameters.TryAdd(parameter.SequenceNumber, parameter);
            }

            var formatted = new List<string>(signature.ParameterTypes.Length);
            for (int index = 0; index < signature.ParameterTypes.Length; index++)
            {
                string text = signature.ParameterTypes[index];
                if (parameters.TryGetValue(index + 1, out Parameter parameter))
                {
                    string prefix = "";
                    if ((parameter.Attributes & ParameterAttributes.In) != 0) prefix += "[in] ";
                    if ((parameter.Attributes & ParameterAttributes.Out) != 0) prefix += "[out] ";
                    if ((parameter.Attributes & ParameterAttributes.Optional) != 0) prefix += "[opt] ";
                    text = prefix + text;
                    string parameterName = _reader.GetString(parameter.Name);
                    if (parameterName.Length > 0)
                    {
                        text += " " + parameterName;
                    }
                    ConstantHandle defaultValue = parameter.GetDefaultValue();
                    if (!defaultValue.IsNil)
                    {
                        text += " = " + ConstantFormatter.Format(_reader, defaultValue);
                    }
                }
                formatted.Add(text);
            }
            if (signature.Header.CallingConvention == SignatureCallingConvention.VarArgs)
            {
                formatted.Add("...");
            }

            string generics = methodGenerics.IsEmpty ? "" : "<" + string.Join(", ", methodGenerics) + ">";
            // Static and instance methods are distinguished by flags, so only unusual conventions are shown.
            string callingConvention = NameFormatter.CallingConvention(signature.Header).Replace("instance ", "", StringComparison.Ordinal);
            return callingConvention + signature.ReturnType + " " + name + generics + "(" + string.Join(", ", formatted) + ")";
        }

        private PropertyReport ReadProperty(PropertyDefinitionHandle handle, GenericContext context, string typeName)
        {
            PropertyDefinition property = _reader.GetPropertyDefinition(handle);
            string name = Guard(() => _reader.GetString(property.Name), typeName + " " + Token(handle), "<malformed name>");
            string location = typeName + "::" + name;
            string signature = Guard(() =>
            {
                MethodSignature<string> decoded = property.DecodeSignature(_names, context);
                string indexer = decoded.ParameterTypes.IsEmpty ? "" : "[" + string.Join(", ", decoded.ParameterTypes) + "]";
                return decoded.ReturnType + " " + name + indexer;
            }, location, name + " <malformed signature>");
            PropertyAccessors accessors = property.GetAccessors();
            return new PropertyReport(
                name,
                signature,
                Accessors(location, [("get", accessors.Getter), ("set", accessors.Setter), .. accessors.Others.Select(o => ("other", o))]),
                Attributes(property.GetCustomAttributes(), location));
        }

        private EventReport ReadEvent(EventDefinitionHandle handle, GenericContext context, string typeName)
        {
            EventDefinition definition = _reader.GetEventDefinition(handle);
            string name = Guard(() => _reader.GetString(definition.Name), typeName + " " + Token(handle), "<malformed name>");
            string location = typeName + "::" + name;
            EventAccessors accessors = definition.GetAccessors();
            return new EventReport(
                name,
                Guard(() => _names.TypeName(definition.Type, context), location, "<malformed type>"),
                Accessors(location, [("add", accessors.Adder), ("remove", accessors.Remover), ("raise", accessors.Raiser), .. accessors.Others.Select(o => ("other", o))]),
                Attributes(definition.GetCustomAttributes(), location));
        }

        private ImmutableArray<string> Accessors(string location, List<(string Kind, MethodDefinitionHandle Method)> accessors) =>
            [.. accessors
                .Where(accessor => !accessor.Method.IsNil)
                .Select(accessor => accessor.Kind + " " + Guard(
                    () => _reader.GetString(_reader.GetMethodDefinition(accessor.Method).Name), location, "<malformed accessor>"))];

        private static string MemberVisibility(MethodAttributes access) => access switch
        {
            MethodAttributes.Public => "public",
            MethodAttributes.Private => "private",
            MethodAttributes.Family => "protected",
            MethodAttributes.Assembly => "internal",
            MethodAttributes.FamANDAssem => "private protected",
            MethodAttributes.FamORAssem => "protected internal",
            MethodAttributes.PrivateScope => "compilercontrolled",
            _ => "unknown",
        };

        private ImmutableArray<string> Attributes(CustomAttributeHandleCollection handles, string location, string prefix = "") =>
            Guard(() => handles
                .Select(handle => Guard(() => _attributes.Format(handle, prefix), location, "[<malformed attribute " + Token(handle) + ">]"))
                .Order(Ordinal)
                .ToImmutableArray(), location, ["[<malformed attribute list>]"]);

        /// <summary>Reads one entity, recording a diagnostic and using a placeholder if its metadata is malformed.</summary>
        private T Guard<T>(Func<T> read, string location, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticIds.MalformedEntity, DiagnosticSeverity.Error,
                    $"Malformed metadata: {exception.Message}", location));
                return fallback;
            }
        }

        private static string Token(EntityHandle handle) =>
            "0x" + MetadataTokens.GetToken(handle).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
