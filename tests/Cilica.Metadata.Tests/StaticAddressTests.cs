using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StaticAddressTests
{
    private static readonly string[] Names = ["Aliased", "Long", "References", "Value", "Nested", "Readonly", "Empty", "Default", "Generic", "FailedInitialization", "All"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(StaticAddressProbe), Names));
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    [InlineData(false, 6)] [InlineData(true, 6)] [InlineData(false, 7)] [InlineData(true, 7)]
    [InlineData(false, 8)] [InlineData(true, 8)] [InlineData(false, 9)] [InlineData(true, 9)]
    [InlineData(false, 10)] [InlineData(true, 10)]
    public async Task Static_borrows_preserve_aliases_storage_lifetimes_and_fault_order(bool optimized, int index)
    {
        var program = (await Programs.Value)[index];
        Assert.Contains(program.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.AddressStatic);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(Names[index], program, typeof(StaticAddressProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],
            persistentRoots: index == 9, expectedStaticRoots: index is 5 or 10 ? 1 : 0);
    }
    [Theory] [InlineData("type")] [InlineData("readonly")] [InlineData("field")] [InlineData("fault")]
    public async Task Both_ir_stages_validate_static_address_capabilities(string mutation)
    {
        var program = (await Programs.Value)[5];
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) => [.. methods.Select(m => m with { Blocks = [..m.Blocks.Select(b => b with {
            Statements = [..b.Statements.Select(s => s is IrStatement.Define { Value.Expression: IrExpression.AddressStatic address } d
                ? mutation == "fault" ? d with { Exceptional = null }
                : d with { Value = mutation == "readonly" ? d.Value with { Type = d.Value.Type with { ReadOnly = false } }
                    : mutation == "type" ? d.Value with { Type = IrType.Int32 }
                    : d.Value with { Expression = address with { Field = address.Field with { Token = 0x0400ffff } } } } : s)] })] })];
        var flow = program.Flow.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { Flow = new(flow with { Methods = Alter(flow.Methods) }) } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Alter(objects.Flow.Methods) } } } } } }));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Native_static_carriers_check_lifetimes_kinds_readonly_and_instance_ownership(bool optimized)
    {
        var program = ValueScalarLowering.LowerExceptions((await Programs.Value)[10]); var objects = program.Flow.Flow.Flow.Flow;
        var fields = objects.Flow.Types.Statics;
        int Slot(IrTypeKind kind, bool readOnly) => fields.IndexOf(fields.First(f => f.Type.Kind == kind && ((f.Attributes & FieldAttributes.InitOnly) != 0) == readOnly));
        int integer = Slot(IrTypeKind.Int32, false), wide = Slot(IrTypeKind.Int64, false), reference = Slot(IrTypeKind.Reference, false), frozen = Slot(IrTypeKind.Int64, true);
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}";
        string cell = Symbol(fields[reference].Type.Class!), root = Symbol(objects.Flow.Types.ObjectBase);
        var artifact = ObjectCppEmitter.EmitExceptions(program);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void malformed(Context& c, Location good) {
                std::uint32_t result = 71;
                auto bad = good; bad.slot = static_field_count;
                assert(load_location_integer(c,bad,result)==LocationStatus::invalid && result==71);
                bad=good; bad.owner=reinterpret_cast<Object*>(std::uintptr_t{1}); assert(load_location_integer(c,bad,result)==LocationStatus::invalid);
                bad=good; bad.declaring=&{{root}}_descriptor; assert(load_location_integer(c,bad,result)==LocationStatus::invalid);
                bad=good; bad.element=1; assert(load_location_integer(c,bad,result)==LocationStatus::invalid);
                bad=good; bad.narrow=LocationNarrow::UInt8; assert(load_location_integer(c,bad,result)==LocationStatus::invalid);
                bad=good; bad.frame=0; assert(load_location_integer(c,bad,result)==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void carriers(Context& c, Location& stale) {
                Frame frame; RootScope roots(c,frame,nullptr,0); BorrowScope borrow(c,frame,nullptr,0,nullptr);
                assert(borrow.status==LocationStatus::complete && ensure_static_storage(c));
                auto integer=static_location(c,borrow.frame.token,{{integer}});
                auto wide=static_location(c,borrow.frame.token,{{wide}});
                auto reference=static_location(c,borrow.frame.token,{{reference}});
                auto frozen=static_location(c,borrow.frame.token,{{frozen}});
                assert(integer.status==LocationStatus::complete && wide.status==LocationStatus::complete && reference.status==LocationStatus::complete && frozen.status==LocationStatus::complete);
                assert(store_location_integer(c,integer.value,17)==LocationStatus::complete);
                assert(store_location_wide_integer(c,wide.value,0xfedcba9876543210ull)==LocationStatus::complete);
                std::uint64_t bits=0; assert(load_location_wide_integer(c,wide.value,bits)==LocationStatus::complete && bits==0xfedcba9876543210ull);
                assert(store_location_wide_integer(c,integer.value,0)==LocationStatus::invalid);
                assert(store_location_integer(c,wide.value,0)==LocationStatus::invalid);
                assert(store_location_wide_integer(c,frozen.value,9)==LocationStatus::read_only);
                auto forged=frozen.value; forged.read_only=false; assert(store_location_wide_integer(c,forged,9)==LocationStatus::read_only);
                Object* cell=allocate<{{cell}}>(c,{{cell}}_descriptor); assert(cell!=nullptr);
                assert(store_location_reference(c,reference.value,cell)==LocationStatus::complete);
                assert(collect(c).status==MarkStatus::complete && c.instance.count==1);
                Object* observed=nullptr; assert(load_location_reference(c,reference.value,observed)==LocationStatus::complete && observed==cell);
                Object* wrong=allocate<{{root}}>(c,{{root}}_descriptor); assert(wrong!=nullptr);
                assert(store_location_reference(c,reference.value,wrong)==LocationStatus::invalid);
                Instance otherHeap; Context other(otherHeap), sibling(c.instance); Frame otherFrame;
                RootScope otherRoots(other,otherFrame,nullptr,0); BorrowScope otherBorrow(other,otherFrame,nullptr,0,nullptr);
                assert(ensure_static_storage(other));
                assert(store_location_integer(other,integer.value,99)==LocationStatus::invalid);
                assert(store_location_integer(sibling,integer.value,99)==LocationStatus::invalid);
                Object* foreign=allocate<{{cell}}>(other,{{cell}}_descriptor);
                assert(store_location_reference(c,reference.value,foreign)==LocationStatus::invalid);
                malformed(c,integer.value);
                c.instance.marking=true; assert(store_location_integer(c,integer.value,99)==LocationStatus::busy); c.instance.marking=false;
                assert(store_location_reference(c,reference.value,nullptr)==LocationStatus::complete);
                assert(collect(c).status==MarkStatus::complete && c.instance.count==0);
                stale=integer.value;
            }
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation=true; Location stale;
                carriers(c,stale); assert(c.top==nullptr && c.borrows==nullptr && c.depth==0);
                std::uint32_t observed=71; assert(load_location_integer(c,stale,observed)==LocationStatus::invalid && observed==71);
                Frame frame; RootScope roots(c,frame,nullptr,0); BorrowScope replacement(c,frame,nullptr,0,nullptr);
                assert(store_location_integer(c,stale,99)==LocationStatus::invalid);
                auto fresh=static_location(c,replacement.frame.token,{{integer}});
                assert(load_location_integer(c,fresh.value,observed)==LocationStatus::complete && observed==17);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("static-borrows"), cpp = Path.Combine(directory,"borrow.cpp"), exe = Path.Combine(directory,"borrow");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);
        var run = await NativeProcess.Run(exe,[],directory); Assert.True(run.ExitCode==0,run.Stdout+run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData(FieldAttributes.Public, false, true)] [InlineData(FieldAttributes.Assembly, false, true)]
    [InlineData(FieldAttributes.Private, false, false)] [InlineData(FieldAttributes.Family, false, false)]
    [InlineData(FieldAttributes.FamORAssem, false, true)] [InlineData(FieldAttributes.FamANDAssem, false, false)]
    [InlineData(FieldAttributes.Public | FieldAttributes.InitOnly, false, true)]
    [InlineData(FieldAttributes.Public | FieldAttributes.InitOnly, true, false)]
    public void Metadata_addresses_keep_access_and_readonly_boundaries(FieldAttributes flags, bool write, bool expected)
    {
        var b = new ResolutionAssemblyBuilder(new("StaticAddressFlags", new Version(1,0,0,0)));
        b.Type("Test","Holder"); var field = b.FieldWithAttributes("Number", flags | FieldAttributes.Static, 6, 8);
        b.Type("Test","Caller"); var root = b.Method("Run", [0,0,8], emit: il => {
            il.OpCode(ILOpCode.Ldsflda); il.Token(field);
            if(write) { il.OpCode(ILOpCode.Ldc_i4_1); il.OpCode(ILOpCode.Stind_i4); il.OpCode(ILOpCode.Ldc_i4_0); }
            else il.OpCode(ILOpCode.Ldind_i4);
            il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!; var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.True(expected == result.Success, string.Join("; ",result.Diagnostics));
        if(expected) ValueScalarLowering.LowerExceptions(result.Program!);
        else Assert.Contains(result.Diagnostics,d => d.Message.Contains(write ? "Readonly" : "Static field access",StringComparison.OrdinalIgnoreCase));
        Assert.False(ValueTranslator.TranslateExceptions(resolver,input,root,library).Success);
    }
}
