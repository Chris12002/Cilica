using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedInterfaceMethodTargetTests
{
    private static TypeSignature Signature(AssemblyResolver resolver, ResolvedAssembly input, Type type) => type == typeof(long)
        ? new TypeSignature.Primitive(PrimitiveTypeCode.Int64) : type == typeof(int)
        ? new TypeSignature.Primitive(PrimitiveTypeCode.Int32) : type == typeof(string)
        ? new TypeSignature.Primitive(PrimitiveTypeCode.String) : throw new InvalidOperationException(type.ToString());
    private static ConstructedType Closed(AssemblyResolver resolver, ResolvedAssembly input, Type type) =>
        new(resolver.ResolveType(input, MetadataTokens.TypeDefinitionHandle(type.MetadataToken & 0xffffff)).Value!,
            new(type.GenericTypeArguments.Select(t => Signature(resolver, input, t))));
    private static ResolvedMethod Declaration(AssemblyResolver resolver, ResolvedAssembly input, Type type, MethodInfo member)
    {
        var open = resolver.ResolveMethod(input, MetadataTokens.MethodDefinitionHandle(member.MetadataToken & 0xffffff)).Value!;
        var owner = Closed(resolver, input, type);
        var argument = owner.Arguments[0];
        return open with { DeclaringType = owner, Signature = open.Signature with {
            ReturnType = open.Signature.ReturnType is TypeSignature.Variable ? argument : open.Signature.ReturnType,
            Parameters = new(open.Signature.Parameters.Select(p => p is TypeSignature.Variable ? argument : p)) } };
    }

    [Theory]
    [InlineData(typeof(int))] [InlineData(typeof(long))] [InlineData(typeof(string))]
    public void Exact_closed_mappings_match_authored_dotnet_calls(Type argument)
    {
        Type[] definitions = [typeof(ClosedDispatchBase<>), typeof(ClosedDispatchOverride<>), typeof(ClosedDispatchHidden<>),
            typeof(ClosedDispatchReimplemented<>), typeof(ClosedDispatchExplicit<>), typeof(ClosedDispatchExplicitChild<>), typeof(ClosedDispatchReexplicit<>)];
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var contract = typeof(IClosedDispatch<>).MakeGenericType(argument);
        foreach (var definition in definitions)
            Check(definition.MakeGenericType(argument));
        if (argument == typeof(long)) Check(typeof(ClosedDispatchConcrete));
        if (argument != typeof(string)) Check(typeof(ClosedDispatchDual));
        void Check(Type receiver)
        {
            var map = receiver.GetInterfaceMap(contract);
            for (int index = 0; index < map.InterfaceMethods.Length; ++index)
            {
                var declaration = Declaration(resolver, input, contract, map.InterfaceMethods[index]);
                var budget = new MemberResolutionBudget();
                var result = resolver.ResolveClosedInterfaceMethodTarget(declaration, Closed(resolver, input, receiver), library, budget);
                Assert.True(result.Success, string.Join("; ", result.Diagnostics));
                var target = result.Value!; var expected = map.TargetMethods[index];
                Assert.Equal(expected.MetadataToken, MetadataTokens.GetToken(target.Implementation.Handle));
                Assert.Equal(Closed(resolver, input, expected.DeclaringType!), target.Implementation.DeclaringType);
                Assert.Equal(declaration.Signature, target.Implementation.Signature);
                var instance = Activator.CreateInstance(receiver)!;
                object value = argument == typeof(string) ? "closed" : argument == typeof(long) ? (object)123L : 123;
                Assert.Equal(Call(map.InterfaceMethods[index], instance, value, true), Call(expected, instance, value, false));
                int used = 65536 - budget.Remaining;
                Assert.True(resolver.ResolveClosedInterfaceMethodTarget(declaration, Closed(resolver, input, receiver), library, new(used)).Success);
                var shortResult = resolver.ResolveClosedInterfaceMethodTarget(declaration, Closed(resolver, input, receiver), library, new(used - 1));
                Assert.False(shortResult.Success); Assert.Contains(shortResult.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
            }
        }
    }

    private static object Call(MethodInfo method, object instance, object value, bool dispatch)
    {
        var function = new DynamicMethod("ClosedInterfaceOracle", typeof(object), [typeof(object), typeof(object)], typeof(ClosedInterfaceMethodTargetTests).Module, true);
        var il = function.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, method.DeclaringType!);
        foreach (var parameter in method.GetParameters()) { il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Unbox_Any, parameter.ParameterType); }
        il.Emit(dispatch ? OpCodes.Callvirt : OpCodes.Call, method);
        if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
        il.Emit(OpCodes.Ret); return function.CreateDelegate<Func<object, object, object>>()(instance, value);
    }

    [Theory]
    [InlineData("different-argument")] [InlineData("forged-signature")] [InlineData("open-owner")]
    [InlineData("constraint")] [InlineData("variant-only")]
    public void Closed_targets_reject_inexact_declarations(string invalid)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var declaration = Declaration(resolver, input, typeof(IClosedDispatch<long>), typeof(IClosedDispatch<long>).GetMethod("Echo")!);
        if (invalid == "forged-signature") declaration = declaration with { Signature = declaration.Signature with { ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int32) } };
        if (invalid == "open-owner") declaration = declaration with { DeclaringType = declaration.DeclaringType with { Arguments = new([new TypeSignature.Variable(false, 0)]) } };
        var receiver = Closed(resolver, input, invalid == "different-argument" ? typeof(ClosedDispatchBase<int>) : typeof(ClosedDispatchBase<long>));
        if (invalid == "constraint") receiver = new(Closed(resolver, input, typeof(ClosedDispatchReference<string>)).Definition,
            new([new TypeSignature.Primitive(PrimitiveTypeCode.Int64)]));
        if (invalid == "variant-only")
        {
            declaration = Declaration(resolver, input, typeof(IClosedProducer<string>), typeof(IClosedProducer<string>).GetMethod("Get")!);
            var argument = new TypeSignature.Primitive(PrimitiveTypeCode.Object);
            declaration = declaration with { DeclaringType = declaration.DeclaringType with { Arguments = new([argument]) },
                Signature = declaration.Signature with { ReturnType = argument } };
            receiver = Closed(resolver, input, typeof(ClosedProducer<string>));
        }
        var result = resolver.ResolveClosedInterfaceMethodTarget(declaration, receiver, library);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
