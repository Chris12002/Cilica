using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ProtectedVirtualTests
{
    private static readonly string[] Names = ["Scalars", "References", "Hidden", "Reabstract", "Generic", "NullOnly", "ArgumentFault", "CapturedFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ProtectedVirtualProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Bodyless_class_calls_dispatch_only_to_concrete_bodies(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ProtectedVirtualProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(),
            (from a in new[] { -1, 0, 1, 2, 3, 4, 127, int.MinValue, int.MaxValue }
             from b in new[] { 0, 1, 2, 3, -1, int.MinValue, int.MaxValue } select (a, b)).ToArray());
    }
    [Fact]
    public async Task Logical_and_scalar_IR_keep_signatures_out_of_executable_methods()
    {
        var programs = await Programs.Value;
        foreach (var program in programs)
        {
            var logical = program.Flow;
            var lowered = ValueScalarLowering.LowerExceptions(program);
            var scalar = lowered.Flow.Flow.Flow.Flow;
            var native = ObjectCppEmitter.EmitExceptions(lowered).Text;
            Assert.Equal(logical.AbstractMethods, scalar.AbstractMethods);
            Assert.All(logical.AbstractMethods, declaration => {
                var id = IrMethodId.From(declaration.Source);
                Assert.DoesNotContain(logical.Flow.Flow.Methods, m => IrMethodId.From(m.Source) == id);
                Assert.DoesNotContain(scalar.Flow.Methods, m => IrMethodId.From(m.Source) == id);
                var symbol = $"cilica_m_{id.ContentHash}_{id.Token:x8}" + (id.Specialization.Length == 0 ? "" : "_g_" + id.Specialization);
                Assert.DoesNotContain(symbol + "(Context&", native, StringComparison.Ordinal);
                Assert.Contains(scalar.Bindings, b => b.Method == id && (b.Attributes & MethodAttributes.Abstract) != 0);
            });
        }
    }
    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("flags")] [InlineData("owner")]
    [InlineData("final")] [InlineData("signature")] [InlineData("slot")] [InlineData("abstract_target")]
    [InlineData("missing_target")] [InlineData("wrong_target")] [InlineData("parameters")] [InlineData("private")] [InlineData("internal")] [InlineData("public")] [InlineData("strict-public")]
    public async Task Logical_and_scalar_validators_reject_forged_abstract_declarations(string damage)
    {
        var program = (await Programs.Value)[0]; var original = program.Flow;
        var declaration = original.AbstractMethods[0]; var id = IrMethodId.From(declaration.Source);
        var binding = original.Bindings.Single(b => b.Method == id);
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var map = objects.Dispatch.Single(m => m.Declaration == id);
        var changed = damage switch {
            "missing" => objects with { AbstractMethods = objects.AbstractMethods.Remove(declaration) },
            "duplicate" => objects with { AbstractMethods = objects.AbstractMethods.Add(declaration), Bindings = objects.Bindings.Add(binding) },
            "private" or "internal" or "public" or "strict-public" => objects with { Bindings = objects.Bindings.Replace(binding, binding with { Attributes = (binding.Attributes & ~(MethodAttributes.MemberAccessMask | (damage == "strict-public" ? 0 : MethodAttributes.CheckAccessOnOverride))) | (damage == "private" ? MethodAttributes.Private : damage == "internal" ? MethodAttributes.Assembly : MethodAttributes.Public) }) },
            "flags" => objects with { Bindings = objects.Bindings.Replace(binding, binding with { Attributes = binding.Attributes & ~MethodAttributes.Abstract }) },
            "owner" => objects with { Flow = objects.Flow with { Types = objects.Flow.Types with { Types = [.. objects.Flow.Types.Types.Select(t => ObjectTypeId.From(t.Source) == binding.Owner ? t with { Attributes = t.Attributes & ~TypeAttributes.Abstract } : t)] } } },
            "final" => objects with { Bindings = objects.Bindings.Replace(binding, binding with { Attributes = binding.Attributes | MethodAttributes.Final }) },
            "signature" => objects with { AbstractMethods = objects.AbstractMethods.Replace(declaration, declaration with { ReturnType = IrType.Int64 }) },
            "slot" => objects with { Bindings = objects.Bindings.Replace(binding, binding with { VirtualSlot = null }) },
            "abstract_target" => objects with { Dispatch = objects.Dispatch.Replace(map, map with { Targets = map.Targets.Replace(map.Targets[0], map.Targets[0] with { Implementation = id }) }) },
            "missing_target" => objects with { Dispatch = objects.Dispatch.Replace(map, map with { Targets = map.Targets.RemoveAt(0) }) },
            "wrong_target" => objects with { Dispatch = objects.Dispatch.Replace(map, map with { Targets = map.Targets.Replace(map.Targets[0], map.Targets[0] with { Implementation = objects.Flow.Entry }) }) },
            _ => objects with { AbstractMethods = objects.AbstractMethods.Replace(declaration, declaration with { Parameters = [.. Enumerable.Repeat(IrType.Reference(binding.Owner), IrLimits.MaximumParameters)] }) }
        };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = original with {
            AbstractMethods = changed.AbstractMethods, Bindings = changed.Bindings, Dispatch = changed.Dispatch,
            Flow = original.Flow with { Flow = original.Flow.Flow with { Types = original.Flow.Flow.Types with { References = changed.Flow.Types } } }
        } }));
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with {
            Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = changed } } }
        }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Abstract_calls_require_null_check_and_virtual_dispatch(bool voidCall)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[0]); var objects = scalar.Flow.Flow.Flow.Flow;
        var ids = objects.AbstractMethods.Select(d => IrMethodId.From(d.Source)).ToHashSet();
        foreach (bool keepVirtual in new[] { false, true })
        {
            var changed = objects with { Flow = objects.Flow with { Methods = [.. objects.Flow.Methods.Select(m => m with {
                Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select<IrStatement, IrStatement>(s => s switch {
                    IrStatement.Invoke call when voidCall && ids.Contains(call.Target) => call with { NullCheckReceiver = !keepVirtual, VirtualDispatch = keepVirtual },
                    IrStatement.Define d when !voidCall && d.Value.Expression is IrExpression.Call call && ids.Contains(call.Target) => d with { Value = d.Value with { Expression = call with { NullCheckReceiver = !keepVirtual, VirtualDispatch = keepVirtual } } },
                    _ => s
                })] })]
            })] } };
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with {
                Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = changed } } }
            }));
        }
    }

    [Fact]
    public async Task Inline_abstract_signatures_remain_actionably_unsupported()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ProtectedVirtualProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ProtectedVirtualProbe).GetMethod("UnsupportedInline")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("value returns and borrows need later lowering", StringComparison.Ordinal));
    }

}
