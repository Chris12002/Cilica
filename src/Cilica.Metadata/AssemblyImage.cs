using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Cilica.Metadata;

/// <summary>
/// An input file opened as data: its PE headers and CLI metadata, validated
/// enough to inspect. Failures become the input-level diagnostics CIL1001 to CIL1004.
/// </summary>
internal sealed class AssemblyImage : IDisposable
{
    private AssemblyImage(string inputName, PEReader peReader, CorHeader corHeader, MetadataReader reader)
    {
        InputName = inputName;
        PEReader = peReader;
        CorHeader = corHeader;
        Reader = reader;
    }

    public string InputName { get; }

    public PEReader PEReader { get; }

    public CorHeader CorHeader { get; }

    public MetadataReader Reader { get; }

    public void Dispose() => PEReader.Dispose();

    public static byte[]? ReadFile(string path, out Diagnostic? failure)
    {
        string inputName = Path.GetFileName(path);
        try
        {
            failure = null;
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Operating-system messages contain absolute paths, which would make output machine-dependent.
            string reason = exception switch
            {
                FileNotFoundException => "the file does not exist",
                DirectoryNotFoundException => "the directory does not exist",
                UnauthorizedAccessException => "access is denied, or the path is a directory",
                ArgumentException or NotSupportedException => "the path is invalid",
                _ => "an I/O error occurred",
            };
            failure = new Diagnostic(DiagnosticIds.InputUnreadable, DiagnosticSeverity.Error, $"Cannot read '{inputName}': {reason}.");
            return null;
        }
    }

    public static AssemblyImage? Open(byte[] image, string inputName, out Diagnostic? failure)
    {
        var peReader = new PEReader(ImmutableArray.Create(image));
        failure = Validate(peReader, inputName, out CorHeader? corHeader, out MetadataReader? reader);
        if (failure is not null)
        {
            peReader.Dispose();
            return null;
        }
        return new AssemblyImage(inputName, peReader, corHeader!, reader!);
    }

    private static Diagnostic? Validate(PEReader peReader, string inputName, out CorHeader? corHeader, out MetadataReader? reader)
    {
        corHeader = null;
        reader = null;
        PEHeaders headers;
        try
        {
            headers = peReader.PEHeaders;
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception))
        {
            return new Diagnostic(
                DiagnosticIds.NotPortableExecutable, DiagnosticSeverity.Error,
                $"'{inputName}' is not a valid portable executable file: {exception.Message}");
        }

        corHeader = headers.CorHeader;
        if (corHeader is null)
        {
            return new Diagnostic(
                DiagnosticIds.NoCliMetadata, DiagnosticSeverity.Error,
                $"'{inputName}' contains no CLI metadata; it is a native image, not a .NET assembly.");
        }

        try
        {
            reader = peReader.GetMetadataReader();
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is InvalidOperationException)
        {
            return new Diagnostic(
                DiagnosticIds.MalformedMetadata, DiagnosticSeverity.Error,
                $"'{inputName}' has malformed CLI metadata: {exception.Message}");
        }
        return null;
    }
}
