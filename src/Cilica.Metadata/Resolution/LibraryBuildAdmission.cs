using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

/// <summary>Versioned capabilities, never a general unsafe-code permission.</summary>
public enum LibraryIntrinsicCapability
{
    StringLayout, ExceptionLayout, StringLength, StringCodeUnit, StringEqual, StringNotEqual, StringConcat,
    ExceptionConstructor, ExceptionMessage, ExceptionParameterName,
    CultureInfoLayout, InvariantCulture, InvariantIntegerFormatting, IntegerTextCacheFind, IntegerTextCachePublish,
    ClosedKeyEqual, ClosedKeyHash, ClosedKeyIsNull,
    ArrayInterfaceCount, ArrayInterfaceGet, ArrayInterfaceFalse, ArrayInterfaceTrue, ArrayInterfaceIdentity, StringFromCodeUnits, ArrayEmpty, ArrayInterfaceAdd, ArrayInterfaceSet, TypeInitializationExceptionConstructor, ExceptionTypeName, ExceptionInnerException, BoxedStringFormat, BoxedTextRender, StringOrdinalEqual, StringEmpty,
    RuntimeTypeLayout, RuntimeTypeHandleLayout, RuntimeTypeObjectLayout, ObjectGetType, RuntimeTypeHash, ObjectIdentityHash,
    ArrayInterfaceInsert, ArrayInterfaceInsertObject, ArrayInterfaceRemoveAt, ArrayInterfaceClearReadOnly, ArrayInterfaceClear,
    ArrayInterfaceIndexOf, ArrayInterfaceContains, ArrayInterfaceRemove, ClosedArraySearch, ArrayInterfaceEnumerate, ArgumentOutOfRangeActualConstructor, ArrayCopyStore, ArrayCopyCompatible, ArrayInterfaceCopyTo, ListCollectionConstruction, ClosedTypeContainsReferences, ClosedTypeIsValueType, ArrayInterfaceEnumerateObjects, IntegerComparerCreate, IntegerComparerCompare, InvalidOperationInnerConstructor, EnumIsDefinedQuery, EnumIsDefinedErrorMessage, EnumFormatting, ClosedTupleShape, DictionaryKeyText, StringCharacterConstruction, StringOrdinalPrefix, SortComparisonName, StaticArrayInitialization, ListOrdinalSort, EnumCompareTo, StringOrdinalCompare, HashSetOrdinalConstruction,
}

public sealed record LibraryBuildInput(string Role, string Path, string Sha256);
public sealed record LibraryIntrinsicGrant(int Token, int OwnerToken, string Owner, string Member,
    string SignatureSha256, LibraryIntrinsicCapability Capability);
public sealed record LibraryBuildArtifact(string AssemblyIdentity, string Sha256,
    ImmutableArray<LibraryBuildInput> Inputs, ImmutableArray<string> CompilerArguments,
    ImmutableArray<LibraryIntrinsicGrant> Grants);
public sealed record LibraryBuildManifest(string Format, int Version, string RuntimeProfile,
    ImmutableArray<LibraryBuildArtifact> Artifacts)
{
    public const string FormatName = "cilica.library-build";
    public const string CurrentRuntimeProfile = "cilica-library-intrinsics-v1";
}

public sealed record LibraryIntrinsicAuthorization(LibraryIntrinsicCapability Capability);

public sealed partial class AssemblyResolver
{
    private ImmutableHashSet<(string Hash, LibraryIntrinsicGrant Grant)> _libraryGrants = [];
    public string? ApprovedLibraryManifestSha256 { get; private set; }
    public const int MaximumLibraryManifestBytes = 1_048_576;
    private static readonly JsonSerializerOptions LibraryManifestJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>(allowIntegerValues: false) },
        MaxDepth = 16,
    };

    /// <summary>Produces reviewable build evidence, not approval. The build host must pin its digest separately.</summary>
    public static byte[] SerializeLibraryBuildManifest(LibraryBuildManifest manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, LibraryManifestJson);

    /// <summary>Describes an exact declaration without granting it any capability.</summary>
    public ResolutionResult<LibraryIntrinsicGrant> DescribeLibraryIntrinsic(ResolvedAssembly image, EntityHandle handle,
        LibraryIntrinsicCapability capability) => Bind(image, handle, () => IntrinsicDeclaration(image, handle, capability));

    /// <summary>Selects one emitted field declaration for build evidence; never grants its requested capability.</summary>
    public ResolutionResult<LibraryIntrinsicGrant> DescribeLibraryFieldIntrinsic(ResolvedType owner, string name,
        LibraryIntrinsicCapability capability, MemberResolutionBudget? budget = null)
    {
        CheckOwner(owner.Assembly); budget ??= new();
        return Bind(owner.Assembly, owner.Handle, () =>
        {
            if (string.IsNullOrEmpty(name) || name.Length > 1024)
                throw InvalidLibraryBuild("Intrinsic field selector requires a bounded name.");
            InspectMember(budget);
            var reader = owner.Assembly.Image.Reader; FieldDefinitionHandle selected = default;
            foreach (var handle in reader.GetTypeDefinition(owner.Handle).GetFields())
            {
                InspectMember(budget);
                if (!reader.StringComparer.Equals(reader.GetFieldDefinition(handle).Name, name)) continue;
                if (!selected.IsNil) throw InvalidLibraryBuild("Intrinsic field selector is ambiguous: " + name);
                selected = handle;
            }
            if (selected.IsNil) throw InvalidLibraryBuild("Intrinsic field was not emitted: " + name);
            return IntrinsicDeclaration(owner.Assembly, selected, capability);
        });
    }

    /// <summary>Selects one emitted method declaration for build evidence; never grants its requested capability.</summary>
    public ResolutionResult<LibraryIntrinsicGrant> DescribeLibraryMethodIntrinsic(ResolvedType owner, string name, int parameterCount,
        LibraryIntrinsicCapability capability, MemberResolutionBudget? budget = null)
    {
        CheckOwner(owner.Assembly); budget ??= new();
        return Bind(owner.Assembly, owner.Handle, () =>
        {
            if (string.IsNullOrEmpty(name) || name.Length > 1024 || parameterCount is < 0 or > 4096)
                throw InvalidLibraryBuild("Intrinsic selector requires a bounded name and parameter count.");
            InspectMember(budget);
            var reader = owner.Assembly.Image.Reader; MethodDefinitionHandle selected = default;
            foreach (var handle in reader.GetTypeDefinition(owner.Handle).GetMethods())
            {
                InspectMember(budget);
                if (!reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, name)) continue;
                if (MethodDefinitionSignature(owner.Assembly, handle).Parameters.Count != parameterCount) continue;
                if (!selected.IsNil) throw InvalidLibraryBuild("Intrinsic member selector is ambiguous: " + name);
                selected = handle;
            }
            if (selected.IsNil) throw InvalidLibraryBuild("Intrinsic member was not emitted: " + name);
            return IntrinsicDeclaration(owner.Assembly, selected, capability);
        });
    }

    /// <summary>
    /// Admits an independently approved build record. The caller must obtain approvedSha256
    /// from its trusted build/release policy, never from compiler input or its adjacent manifest.
    /// This checks an attestation against snapshots; it does not rebuild or prove the attested build.
    /// </summary>
    public ResolutionResult<AssemblyResolver> CreateTrustedLibraryCompilationScope(LibraryBindingPlan plan,
        ReadOnlySpan<byte> manifestBytes, string approvedSha256)
    {
        CheckLibraryPlan(plan);
        if (_librarySource is not null) throw new InvalidOperationException("Library compilation scopes cannot be nested.");
        try
        {
            if (manifestBytes.Length is <= 0 or > MaximumLibraryManifestBytes)
                throw InvalidLibraryBuild("Build manifest must contain 1..1048576 bytes.");
            if (!Sha256(approvedSha256) || !StringComparer.Ordinal.Equals(approvedSha256, Convert.ToHexStringLower(SHA256.HashData(manifestBytes))))
                throw InvalidLibraryBuild("Build manifest does not match the independently approved SHA-256.");
            // Duplicate properties are ambiguous to external signing/review tools, even if a JSON parser accepts them.
            using (var document = JsonDocument.Parse(manifestBytes.ToArray(), new() { MaxDepth = 16 }))
                RejectDuplicateProperties(document.RootElement);
            var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(manifestBytes, LibraryManifestJson);
            if (manifest is null || manifest.Format != LibraryBuildManifest.FormatName || manifest.Version != 1
                || manifest.RuntimeProfile != LibraryBuildManifest.CurrentRuntimeProfile || manifest.Artifacts.IsDefaultOrEmpty
                || manifest.Artifacts.Length > 256)
                throw InvalidLibraryBuild("Unsupported or incomplete build manifest/profile.");
            var expected = plan.Mappings.Values.Select(t => t.Assembly).DistinctBy(a => (a.Identity, a.ContentHash))
                .ToDictionary(a => a.Identity.DisplayName, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var grants = ImmutableHashSet.CreateBuilder<(string, LibraryIntrinsicGrant)>();
            int inputCount = 0, grantCount = 0;
            foreach (var artifact in manifest.Artifacts)
            {
                if (artifact is null || artifact.AssemblyIdentity is null || !seen.Add(artifact.AssemblyIdentity)
                    || !expected.TryGetValue(artifact.AssemblyIdentity, out var image) || image.IsReferenceAssembly
                    || artifact.Sha256 != image.ContentHash)
                    throw InvalidLibraryBuild("Build outputs must exactly match each selected implementation identity and SHA-256 once.");
                if (artifact.Inputs.IsDefaultOrEmpty || artifact.CompilerArguments.IsDefaultOrEmpty
                    || artifact.CompilerArguments.Length > 1024 || artifact.CompilerArguments.Any(a => string.IsNullOrEmpty(a) || a.Length > 4096)
                    || artifact.Grants.IsDefault)
                    throw InvalidLibraryBuild("Build output needs bounded effective compiler arguments, input hashes and an explicit grant list.");
                var paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var input in artifact.Inputs)
                {
                    if (++inputCount > 4096 || input is null || input.Role is not ("source" or "reference" or "compiler" or "options")
                        || string.IsNullOrEmpty(input.Path) || input.Path.Length > 4096 || !paths.Add(input.Path) || !Sha256(input.Sha256))
                        throw InvalidLibraryBuild("Build inputs require unique paths, supported roles and lowercase SHA-256 hashes; at most 4096 inputs are allowed.");
                }
                if (!artifact.Inputs.Any(i => i.Role == "source") || !artifact.Inputs.Any(i => i.Role == "compiler"))
                    throw InvalidLibraryBuild("Build provenance must identify source and compiler bytes.");
                foreach (var grant in artifact.Grants)
                {
                    if (++grantCount > 4096 || grant is null || !Enum.IsDefined(grant.Capability)
                        || !Sha256(grant.SignatureSha256)) throw InvalidLibraryBuild("Invalid or excessive intrinsic grants.");
                    var actual = IntrinsicDeclaration(image, MetadataTokens.EntityHandle(grant.Token), grant.Capability);
                    if (actual != grant || !grants.Add((image.ContentHash, grant)))
                        throw InvalidLibraryBuild("Intrinsic grants must uniquely match exact owner/member tokens, names and signature hashes.");
                }
            }
            if (seen.Count != expected.Count) throw InvalidLibraryBuild("Every selected implementation output needs approved build provenance.");
            var scope = new AssemblyResolver(this, plan) { _libraryGrants = grants.ToImmutable(), ApprovedLibraryManifestSha256 = approvedSha256 };
            return ResolutionResult<AssemblyResolver>.Resolved(scope);
        }
        catch (BindingFailureException failure) { return ResolutionResult<AssemblyResolver>.Failed(failure.Diagnostics); }
        catch (Exception error) when (error is JsonException or ArgumentException || MalformedInput.IsMalformed(error))
        { return ResolutionResult<AssemblyResolver>.Failed(DiagnosticIds.LibraryBuildInvalid, "Invalid library build evidence: " + error.Message); }
    }

    /// <summary>Existing strict profiles retain their explicit runtime selection; mapped profiles require exact approved grants.</summary>
    public ResolutionResult<LibraryIntrinsicAuthorization> AuthorizeLibraryIntrinsic(ResolvedAssembly image, EntityHandle handle,
        LibraryIntrinsicCapability capability) => Bind(image, handle, () =>
        {
            var declaration = IntrinsicDeclaration(image, handle, capability);
            if (_librarySource is not null && !_libraryGrants.Contains((image.ContentHash, declaration)))
                throw Failure(DiagnosticIds.LibraryIntrinsicDenied,
                    "Mapped library intrinsic has no exact approved build grant: " + capability + ". Supply independently approved build provenance and the reviewed member/type capability.");
            return new LibraryIntrinsicAuthorization(capability);
        });

    private static LibraryIntrinsicGrant IntrinsicDeclaration(ResolvedAssembly image, EntityHandle handle, LibraryIntrinsicCapability capability)
    {
        if (!Enum.IsDefined(capability)) throw InvalidLibraryBuild("Unknown intrinsic capability.");
        var reader = image.Image.Reader;
        bool layout = capability is LibraryIntrinsicCapability.StringLayout or LibraryIntrinsicCapability.ExceptionLayout or LibraryIntrinsicCapability.CultureInfoLayout or LibraryIntrinsicCapability.RuntimeTypeLayout or LibraryIntrinsicCapability.RuntimeTypeHandleLayout or LibraryIntrinsicCapability.RuntimeTypeObjectLayout;
        bool field = capability == LibraryIntrinsicCapability.StringEmpty;
        if (handle.IsNil || handle.Kind != (layout ? HandleKind.TypeDefinition : field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition))
            throw InvalidLibraryBuild("Intrinsic capability requires the appropriate non-nil type, method or field definition token.");
        var owner = layout ? (TypeDefinitionHandle)handle : field ? reader.GetFieldDefinition((FieldDefinitionHandle)handle).GetDeclaringType()
            : reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType();
        string name = layout ? "" : field ? reader.GetString(reader.GetFieldDefinition((FieldDefinitionHandle)handle).Name)
            : reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)handle).Name);
        var signatureReader = layout ? default : reader.GetBlobReader(field ? reader.GetFieldDefinition((FieldDefinitionHandle)handle).Signature
            : reader.GetMethodDefinition((MethodDefinitionHandle)handle).Signature);
        if (signatureReader.Length > 4096) throw InvalidLibraryBuild("Intrinsic declaration signature exceeds 4096 bytes.");
        byte[] signature = layout ? [] : signatureReader.ReadBytes(signatureReader.Length);
        return new(MetadataTokens.GetToken(handle), MetadataTokens.GetToken(owner), new NameFormatter(reader).TypeDefinitionName(owner), name,
            Convert.ToHexStringLower(SHA256.HashData(signature)), capability);
    }

    private static bool Sha256(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static BindingFailureException InvalidLibraryBuild(string message) => Failure(DiagnosticIds.LibraryBuildInvalid, message);
    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw InvalidLibraryBuild("Duplicate JSON properties are not valid build evidence.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
