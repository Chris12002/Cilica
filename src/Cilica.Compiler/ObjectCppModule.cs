using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>A generated file with a relative, compiler-owned name.</summary>
public sealed record CppModuleFile(string Name, string Text);

/// <summary>Compile every source with the shared header available, then link their objects together.</summary>
public sealed record CppModuleArtifact(MethodSource Source, string EntryPoint, CppModuleFile Header,
    ImmutableArray<CppModuleFile> Sources)
{
    public const string Profile = "object-exceptions-module-v1";
}

internal sealed class ObjectCppModuleBuilder
{
    private const int MaximumSharedBytes = 64 * 1024 * 1024;
    private const int MaximumUnitBytes = 8 * 1024 * 1024;
    private const int MaximumUnits = 1024;
    private readonly int _methodsPerUnit;
    private readonly Dictionary<IrMethodId, string> _frames = [];
    private readonly ImmutableArray<CppModuleFile>.Builder _sources = ImmutableArray.CreateBuilder<CppModuleFile>();
    private readonly StringBuilder _unit = new();
    private CppModuleFile _header = null!;
    private string _scope = "", _symbol = "";
    private int _count, _bytes;

    internal ObjectCppModuleBuilder(int methodsPerUnit)
    {
        if (methodsPerUnit is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(methodsPerUnit), "A native source unit admits 1 through 256 complete methods.");
        _methodsPerUnit = methodsPerUnit;
    }
    internal void AddFrame(IrMethodId method, string text) => _frames.Add(method, text);
    internal string TakeFrame(IrMethodId method)
    {
        string frame = _frames[method]; _frames.Remove(method); return frame;
    }
    internal void Begin(string shared, string scope, string symbol)
    {
        _scope = scope; _symbol = symbol;
        string header = "#pragma once\n" + SharedLinkage(shared) + "}\n";
        if (Encoding.UTF8.GetByteCount(header) > MaximumSharedBytes)
            throw new ArgumentException("Native module shared definitions exceed the 64 MiB source limit.");
        _header = new(symbol + ".hpp", header);
    }
    private string Prefix => "#include \"" + _header.Name + "\"\nnamespace " + _scope + " {\n";
    internal void AddMethod(string text, MethodSource source)
    {
        int bytes = Encoding.UTF8.GetByteCount(text), overhead = Encoding.UTF8.GetByteCount(Prefix) + 2;
        if (bytes > MaximumUnitBytes - overhead)
            throw new ArgumentException("Native method '" + source.Name + "' exceeds the 8 MiB source-unit limit.");
        if (_count == _methodsPerUnit || bytes > MaximumUnitBytes - overhead - _bytes) Flush();
        _unit.Append(text); _bytes += bytes; ++_count;
    }
    private void Flush()
    {
        if (_count == 0) return;
        // Reserve the final source slot for the boundary.
        if (_sources.Count >= MaximumUnits - 1)
            throw new ArgumentException("Native module exceeds the 1,024 source-unit limit; group more methods per unit.");
        _sources.Add(new(_symbol + "_part_" + _sources.Count.ToString("D4", CultureInfo.InvariantCulture) + ".cpp", Prefix + _unit + "}\n"));
        _unit.Clear(); _bytes = 0; _count = 0;
    }
    internal void AddBoundary(string text)
    {
        Flush();
        string boundary = Prefix + text;
        if (Encoding.UTF8.GetByteCount(boundary) > MaximumUnitBytes)
            throw new ArgumentException("Native module boundary exceeds the 8 MiB source-unit limit.");
        _sources.Add(new(_symbol + "_boundary.cpp", boundary));
    }
    internal CppModuleArtifact Complete(CppArtifact boundary)
    {
        if (_frames.Count != 0) throw new InvalidOperationException("A native module has unconsumed method frames.");
        return new(boundary.Source, boundary.EntryPoint, _header, _sources.ToImmutable());
    }

    // This processes only Cilica's generated common definitions, not arbitrary C++.
    // Promote namespace-scope storage/functions to C++20 inline linkage. That gives
    // descriptors, token counters and function-local statics one identity across
    // all translation units. Method declarations already have external linkage.
    // Quoted text and comments cannot change brace depth; method-local static
    // exception text remains local to its (now shared inline) runtime function.
    private static string SharedLinkage(string text)
    {
        var result = new StringBuilder(text.Length);
        int start = 0, depth = 0;
        for (int i = 0; i < text.Length;)
        {
            char ch = text[i];
            if (ch == '/' && i + 1 < text.Length && text[i + 1] is '/' or '*')
            {
                bool line = text[i + 1] == '/'; i += 2;
                while (i < text.Length && (line ? text[i] != '\n' : !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))) ++i;
                if (!line) i = Math.Min(text.Length, i + 2);
                continue;
            }
            if (ch is '\'' or '"')
            {
                ++i;
                while (i < text.Length)
                {
                    char next = text[i++];
                    if (next == '\\' && i < text.Length) ++i;
                    else if (next == ch) break;
                }
                continue;
            }
            if (ch == '{') ++depth;
            else if (ch == '}') --depth;
            if (char.IsAsciiLetter(ch) || ch == '_')
            {
                int token = i++;
                while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) ++i;
                var word = text.AsSpan(token, i - token);
                if (word.SequenceEqual("namespace") && depth != 0)
                    throw new InvalidOperationException("Generated shared module definitions cannot contain nested namespaces.");
                if (i < text.Length && text[i] == '"' && word.EndsWith("R", StringComparison.Ordinal))
                    throw new InvalidOperationException("Generated shared module definitions cannot contain raw string literals.");
                if (depth == 1 && word.SequenceEqual("static"))
                {
                    result.Append(text.AsSpan(start, token - start)).Append("inline"); start = i;
                }
            }
            else ++i;
        }
        if (depth != 1) throw new InvalidOperationException("Generated module definitions must have one open closure namespace.");
        return result.Append(text.AsSpan(start)).ToString();
    }
}
