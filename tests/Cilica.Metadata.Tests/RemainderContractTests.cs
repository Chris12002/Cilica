using System;
using System.Reflection.Emit;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class RemainderContractTests
{
    [Theory]
    [InlineData(5, 4, 1, Int32Fault.None)]
    [InlineData(5, -4, 1, Int32Fault.None)]
    [InlineData(-5, 4, -1, Int32Fault.None)]
    [InlineData(-5, -4, -1, Int32Fault.None)]
    [InlineData(int.MinValue, -1, 0, Int32Fault.Overflow)]
    [InlineData(int.MaxValue, -1, 0, Int32Fault.None)]
    [InlineData(int.MinValue, 1, 0, Int32Fault.None)]
    [InlineData(int.MinValue, int.MinValue, 0, Int32Fault.None)]
    [InlineData(7, int.MinValue, 7, Int32Fault.None)]
    [InlineData(-7, int.MinValue, -7, Int32Fault.None)]
    [InlineData(0, -1, 0, Int32Fault.None)]
    [InlineData(int.MinValue, 0, 0, Int32Fault.DivideByZero)]
    [InlineData(1, 0, 0, Int32Fault.DivideByZero)]
    public void Signed_profile_is_pinned_against_checked_unchecked_and_raw_authored_dotnet(int a, int b, int value, Int32Fault fault)
    {
        var expected = (fault, value);
        Assert.Equal(expected, Observe(() => IntegerRemainder.SignedRemainder(a, b, 0)));
        Assert.Equal(expected, Observe(() => IntegerRemainder.CheckedRemainder(a, b, 0)));
        var raw = AuthoredRemainder(OpCodes.Rem);
        Assert.Equal(expected, Observe(() => raw(a, b)));
    }

    [Theory]
    [InlineData(-1, int.MinValue, int.MaxValue, Int32Fault.None)]
    [InlineData(int.MinValue, -1, int.MinValue, Int32Fault.None)]
    [InlineData(-1, 2, 1, Int32Fault.None)]
    [InlineData(int.MinValue, 1, 0, Int32Fault.None)]
    [InlineData(int.MinValue, 0, 0, Int32Fault.DivideByZero)]
    public void Unsigned_profile_interprets_bits_and_has_no_signed_overflow_case(int a, int b, int value, Int32Fault fault)
    {
        var expected = (fault, value);
        Assert.Equal(expected, Observe(() => IntegerRemainder.UnsignedRemainder(a, b, 0)));
        var raw = AuthoredRemainder(OpCodes.Rem_Un);
        Assert.Equal(expected, Observe(() => raw(a, b)));
    }

    private static (Int32Fault Fault, int Value) Observe(Func<int> reference)
    {
        try { return (Int32Fault.None, reference()); }
        catch (DivideByZeroException) { return (Int32Fault.DivideByZero, 0); }
        catch (OverflowException) { return (Int32Fault.Overflow, 0); }
    }

    private static Func<int, int, int> AuthoredRemainder(OpCode operation)
    {
        // Emit only these independently authored instructions. Never load or
        // decode compiler input bytes to construct a reference executable.
        var method = new DynamicMethod("AuthoredRemainder", typeof(int), [typeof(int), typeof(int)]);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(operation); il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<int, int, int>>();
    }
}
