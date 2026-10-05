using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
using static Cilica.Metadata.Tests.ClosedInterfaceMethodTargetTests;

namespace Cilica.Metadata.Tests;

public sealed class ClosedClassVirtualTargetTests
{
    [Theory]
    [InlineData(typeof(int))] [InlineData(typeof(long))] [InlineData(typeof(string))]
    public void Closed_slots_match_dotnet_calls_and_canonical_base_definitions(Type argument)
    {
        Type[] definitions = [typeof(ClosedDispatchBase<>), typeof(ClosedDispatchOverride<>), typeof(ClosedDispatchHidden<>),
            typeof(ClosedDispatchReimplemented<>), typeof(ClosedDispatchNewSlot<>), typeof(ClosedDispatchFinal<>), typeof(ClosedDispatchReference<>)];
        var types = definitions.Where(t => t != typeof(ClosedDispatchReference<>) || argument == typeof(string))
            .Select(t => t.MakeGenericType(argument)).ToArray();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        foreach (var owner in types)
        foreach (var method in owner.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.IsVirtual))
        foreach (var receiver in types.Where(owner.IsAssignableFrom))
        {
            var declaration = Declaration(resolver, input, owner, method);
            var budget = new MemberResolutionBudget();
            var result = resolver.ResolveClosedClassVirtualTarget(declaration, Closed(resolver, input, receiver), library, budget);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            var target = result.Value!;
            Assert.Equal(method.GetBaseDefinition().MetadataToken, MetadataTokens.GetToken(target.Slot.Handle));
            var implementationOwner = types.Single(t => Closed(resolver, input, t) == target.Implementation.DeclaringType);
            var implementation = implementationOwner.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Single(m => m.MetadataToken == MetadataTokens.GetToken(target.Implementation.Handle));
            var instance = Activator.CreateInstance(receiver)!;
            object value = argument == typeof(string) ? "closed" : argument == typeof(long) ? (object)123L : 123;
            Assert.Equal(Call(method, instance, value, true), Call(implementation, instance, value, false));
            Assert.Equal(Closed(resolver, input, receiver), target.Receiver);
            int used = 65536 - budget.Remaining;
            Assert.True(resolver.ResolveClosedClassVirtualTarget(declaration, target.Receiver, library, new(used)).Success);
            Assert.Contains(resolver.ResolveClosedClassVirtualTarget(declaration, target.Receiver, library, new(used - 1)).Diagnostics,
                d => d.Id == DiagnosticIds.TranslationLimit);
        }
    }

    [Theory]
    [InlineData("argument")] [InlineData("signature")] [InlineData("open")] [InlineData("constraint")] [InlineData("nonvirtual")]
    public void Inexact_or_unsupported_inputs_do_not_produce_choices(string invalid)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var owner = invalid == "nonvirtual" ? typeof(ClosedDispatchHidden<long>) : typeof(ClosedDispatchBase<long>);
        var declaration = Declaration(resolver, input, owner, owner.GetMethod("Echo")!);
        if (invalid == "signature") declaration = declaration with { Signature = declaration.Signature with { ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int32) } };
        if (invalid == "open") declaration = declaration with { DeclaringType = declaration.DeclaringType with { Arguments = new([new TypeSignature.Variable(false, 0)]) } };
        var receiver = Closed(resolver, input, invalid == "argument" ? typeof(ClosedDispatchBase<int>) : typeof(ClosedDispatchOverride<long>));
        if (invalid == "constraint") receiver = new(Closed(resolver, input, typeof(ClosedDispatchReference<string>)).Definition,
            new([new TypeSignature.Primitive(PrimitiveTypeCode.Int64)]));
        Assert.False(resolver.ResolveClosedClassVirtualTarget(declaration, receiver, library).Success);
    }

    [Fact]
    public void Foreign_readers_are_rejected_before_identity_deduplication()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        using var foreign = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var other = foreign.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var declaration = Declaration(resolver, input, typeof(ClosedDispatchBase<long>), typeof(ClosedDispatchBase<long>).GetMethod("Code")!);
        Assert.Throws<ArgumentException>(() => resolver.ResolveClosedClassVirtualTarget(declaration, Closed(foreign, other, typeof(ClosedDispatchOverride<long>)), library));
        Assert.Throws<ArgumentException>(() => resolver.ResolveClosedClassVirtualTarget(
            Declaration(foreign, other, typeof(ClosedDispatchBase<long>), typeof(ClosedDispatchBase<long>).GetMethod("Code")!),
            Closed(resolver, input, typeof(ClosedDispatchOverride<long>)), library));
    }
}
