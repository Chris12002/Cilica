using System;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;

// This process references the compiler only. It never loads or executes the kernel.
if (args.Length != 1) { Console.Error.WriteLine("Usage: Emit <prepared-output-directory>"); return 2; }
string directory = Path.GetFullPath(args[0]);
string assembly = File.ReadAllText(Path.Combine(directory, "assembly.path"));
string library = File.ReadAllText(Path.Combine(directory, "runtime.path"));
int token = int.Parse(File.ReadAllText(Path.Combine(directory, "root.token")), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
var created = AssemblyResolver.Create([Path.GetDirectoryName(assembly)!, Path.GetDirectoryName(library)!]);
if (!created.Success) { Console.Error.WriteLine(string.Join("; ", created.Diagnostics)); return 1; }
using var resolver = created.Value!;
var input = resolver.OpenInput(assembly); var runtime = resolver.OpenInput(library);
if (!input.Success || !runtime.Success) { Console.Error.WriteLine(string.Join("; ", input.Diagnostics) + string.Join("; ", runtime.Diagnostics)); return 1; }
var translated = ObjectTranslator.Translate(resolver, input.Value!, (MethodDefinitionHandle)MetadataTokens.EntityHandle(token), runtime.Value!);
if (!translated.Success) { Console.Error.WriteLine(string.Join("; ", translated.Diagnostics)); return 1; }
var artifact = ObjectCppEmitter.EmitProgram(translated.Program!);
File.WriteAllText(Path.Combine(directory, "generated.cpp"), artifact.Text, new UTF8Encoding(false));
File.WriteAllText(Path.Combine(directory, "entry.hpp"), "#pragma once\n#include \"generated.cpp\"\n#define CILICA_DEMO_ENTRY " + artifact.EntryPoint + "\n", new UTF8Encoding(false));
Console.WriteLine("Emitted " + ObjectCppEmitter.Profile + ": " + translated.Program!.Flow.Methods.Length + " managed bodies.");
return 0;
