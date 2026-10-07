using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ScopedRefLocalSafetyTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program=new(async()=>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(ScopedRefLocalProbe),"Frame"))[0]);
    private static IrMethod Alter(IrMethod method,string mutation)
    {
        var borrow=IrType.Location(IrType.Int32);
        var invoke=method.Blocks.SelectMany(b=>b.Statements).OfType<IrStatement.Invoke>().First();
        IrStatement.Define D(int id,int offset,IrType type,IrExpression expression,bool fault=false)=>
            new(new(id,type,offset,expression),fault?IrExceptionTarget.MethodExit:null);
        ImmutableArray<IrStatement> store=[D(0,1,mutation=="readonly"?IrType.Location(IrType.Int32,true):borrow,new IrExpression.AddressLocal(0)),new IrStatement.StoreLocal(1,1,0)];
        if(mutation=="missing")store=store.RemoveAt(1);
        if(mutation=="duplicate")store=store.Add(new IrStatement.StoreLocal(1,1,0));
        if(mutation=="early")store=[D(0,1,borrow,new IrExpression.Local(1)),D(1,1,borrow,new IrExpression.AddressLocal(0)),new IrStatement.StoreLocal(1,1,1)];
        return method with { Locals=[IrType.Int32,borrow], ExceptionRegions=[], Blocks=[
            new(0,0,[],[D(0,0,IrType.Int32,new IrExpression.Parameter(0))],new IrBranch(0,Int32Comparison.Nonzero,0,null,new(1,[]),new(mutation=="bypass"?2:1,[]))),
            new(1,1,[],store,new IrJump(1,new(2,[]))),
            new(2,2,[],[D(0,2,borrow,new IrExpression.Local(1)),D(1,2,IrType.Int32,new IrExpression.LoadLocation(0),true),invoke with { ILOffset=2,Arguments=[0,1] }],new IrReturn(2,1))] };
    }
    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("early")] [InlineData("bypass")] [InlineData("readonly")]
    public async Task Both_stages_reject_unassigned_reassigned_nondominated_and_readonly_borrows(string mutation)
    {
        var program=await Program.Value;var flow=program.Flow.Flow.Flow;
        ImmutableArray<IrMethod> Change(ImmutableArray<IrMethod> methods,string kind)=>
            [..methods.Select(m=>IrMethodId.From(m.Source)==flow.Entry?Alter(m,kind):m)];
        var positive=program with { Flow=program.Flow with { Flow=program.Flow.Flow with { Flow=flow with { Methods=Change(flow.Methods,"") } } } };
        ManagedExceptionIrValidator.Validate(positive);
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with { Flow=program.Flow with {
            Flow=program.Flow.Flow with { Flow=flow with { Methods=Change(flow.Methods,mutation) } } } }));
        var scalar=ValueScalarLowering.LowerExceptions(positive);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        ScalarManagedExceptionIrValidator.Validate(scalar);
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with { Flow=scalar.Flow with {
            Flow=boxes with { Flow=boxes.Flow with { Flow=objects with { Flow=objects.Flow with { Methods=Change(objects.Flow.Methods,mutation) } } } } } }));
    }
}
