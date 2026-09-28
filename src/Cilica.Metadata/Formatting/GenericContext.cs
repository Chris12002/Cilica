using System.Collections.Immutable;

namespace Cilica.Metadata.Formatting;

/// <summary>
/// Names of the generic parameters in scope while decoding a signature.
/// Missing names fall back to ILASM index notation (<c>!0</c>, <c>!!0</c>).
/// </summary>
internal readonly record struct GenericContext(ImmutableArray<string> TypeParameters, ImmutableArray<string> MethodParameters)
{
    public static GenericContext Empty { get; } = new([], []);

    public string TypeParameter(int index) =>
        "!" + (TypeParameters.IsDefault || index >= TypeParameters.Length ? Invariant.Format(index) : TypeParameters[index]);

    public string MethodParameter(int index) =>
        "!!" + (MethodParameters.IsDefault || index >= MethodParameters.Length ? Invariant.Format(index) : MethodParameters[index]);
}
