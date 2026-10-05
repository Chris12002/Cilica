using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectInterfacePlanTests
{
    private const TypeAttributes Interface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    private const MethodAttributes Abstract = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract;
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private static readonly Type[] Receivers = [typeof(ExactInterfaceBase), typeof(ExactInterfaceOverride), typeof(ExactInterfaceHidden),
        typeof(ExactInterfaceReimplemented), typeof(ExactInterfaceNonVirtualHidden), typeof(ExactInterfaceInherited),
        typeof(ExactInterfaceExplicit), typeof(ExactInterfaceExplicitInherited), typeof(ExactInterfaceExplicitReimplemented),
        typeof(ExactInterfaceReexplicit), typeof(ExactInterfaceFromPlain), typeof(ExactInterfaceDiamond), typeof(ExactInterfaceUnrelated)];
    private static readonly MethodInfo[] Declarations = [.. typeof(IExactRead).GetMethods(), .. typeof(IExactSecond).GetMethods()];

    private static ObjectInterfaceDispatchPlan Authored()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(IExactRead).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver,
            [.. Receivers.Select(t => resolver.ResolveType(image, (TypeDefinitionHandle)MetadataTokens.EntityHandle(t.MetadataToken)).Value!)],
            [.. Declarations.Select(m => resolver.ResolveDeclaredMethod(image, MetadataTokens.EntityHandle(m.MetadataToken)).Value!)], runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Plan!;
    }

    [Fact]
    public void Detached_maps_match_every_authored_dotnet_receiver_and_contract_member()
    {
        var plan = Authored(); ObjectInterfacePlanValidator.Validate(plan); int comparisons = 0;
        foreach (var map in plan.Dispatch)
        {
            var declaration = Declarations.Single(m => m.MetadataToken == map.Declaration.Token);
            foreach (var target in map.Targets)
            {
                var receiver = Receivers.Single(t => t.MetadataToken == target.Receiver.Token);
                var oracle = receiver.GetInterfaceMap(declaration.DeclaringType!);
                var expected = oracle.TargetMethods[Array.IndexOf(oracle.InterfaceMethods, declaration)];
                Assert.Equal(expected.MetadataToken, target.Implementation.Token);
                var body = plan.Methods.Single(m => m.Binding.Method == target.Implementation);
                Assert.Equal(expected.IsVirtual ? expected.GetBaseDefinition().MetadataToken : (int?)null, body.Binding.VirtualSlot?.Method.Token);
                Assert.Equal(expected.IsPrivate, (body.Binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private);
                comparisons++;
            }
        }
        Assert.Equal(47, comparisons);
        Assert.All(plan.Declarations, d => Assert.True((d.Attributes & MethodAttributes.Abstract) != 0));
        Assert.DoesNotContain(plan.Methods, m => plan.Declarations.Any(d => IrMethodId.From(d.Source) == m.Binding.Method));
        var detached = JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(plan))!;
        ObjectInterfacePlanValidator.Validate(detached); // All readers have already been disposed; no fields in this fixture.
    }

    [Theory]
    [InlineData("missing_map")]
    [InlineData("duplicate_map")]
    [InlineData("missing_receiver")]
    [InlineData("duplicate_receiver")]
    [InlineData("unrelated_receiver")]
    [InlineData("missing_body")]
    [InlineData("wrong_body")]
    [InlineData("wrong_mapping")]
    [InlineData("implicit_private")]
    [InlineData("slot_identity")]
    [InlineData("slot_name")]
    [InlineData("body_return")]
    [InlineData("private_flags")]
    [InlineData("declaration_flags")]
    [InlineData("declaration_receiver")]
    [InlineData("duplicate_declaration")]
    [InlineData("unrelated_method")]
    [InlineData("default_methods")]
    [InlineData("default_targets")]
    [InlineData("null_declaration")]
    public void Forged_dispatch_assertions_fail_validation(string damage)
    {
        var plan = Authored(); var map = plan.Dispatch[0];
        var explicitTarget = map.Targets.First(t => t.IsExplicit);
        var virtualTarget = map.Targets.First(t => plan.Methods.Single(m => m.Binding.Method == t.Mapping).Binding.VirtualSlot is not null && !t.IsExplicit);
        var body = plan.Methods.Single(m => m.Binding.Method == virtualTarget.Implementation);
        switch (damage)
        {
            case "missing_map": plan = plan with { Dispatch = plan.Dispatch.RemoveAt(0) }; break;
            case "duplicate_map": plan = plan with { Dispatch = plan.Dispatch.SetItem(1, map) }; break;
            case "missing_receiver": map = map with { Targets = map.Targets.RemoveAt(0) }; break;
            case "duplicate_receiver": map = map with { Targets = map.Targets.Add(map.Targets[0]) }; break;
            case "unrelated_receiver": map = map with { Targets = map.Targets.SetItem(0, map.Targets[0] with
                { Receiver = new(map.Targets[0].Receiver.ContentHash, typeof(ExactInterfaceUnrelated).MetadataToken) }) }; break;
            case "missing_body": plan = plan with { Methods = plan.Methods.Remove(body) }; break;
            case "wrong_body": map = map with { Targets = map.Targets.Replace(virtualTarget, virtualTarget with { Implementation = explicitTarget.Implementation }) }; break;
            case "wrong_mapping": map = map with { Targets = map.Targets.Replace(virtualTarget, virtualTarget with { Mapping = explicitTarget.Mapping }) }; break;
            case "implicit_private": map = map with { Targets = map.Targets.Replace(explicitTarget, explicitTarget with { IsExplicit = false }) }; break;
            case "slot_identity": body = body with { Binding = body.Binding with { VirtualSlot = new(explicitTarget.Mapping, body.Binding.Owner) } }; break;
            case "slot_name": body = body with { Binding = body.Binding with { VirtualName = "forged" } }; break;
            case "body_return": body = body with { ReturnType = IrType.Void }; break;
            case "private_flags":
                body = plan.Methods.Single(m => m.Binding.Method == explicitTarget.Mapping);
                plan = plan with { Methods = plan.Methods.Replace(body, body with { Binding = body.Binding with { Attributes = MethodAttributes.Private | MethodAttributes.Virtual } }) }; break;
            case "declaration_flags": plan = plan with { Declarations = plan.Declarations.SetItem(0, plan.Declarations[0] with { Attributes = MethodAttributes.Public | MethodAttributes.Static }) }; break;
            case "declaration_receiver": plan = plan with { Declarations = plan.Declarations.SetItem(0, plan.Declarations[0] with { Parameters = [IrType.Int32] }) }; break;
            case "duplicate_declaration": plan = plan with { Declarations = plan.Declarations.Add(plan.Declarations[0]) }; break;
            case "unrelated_method": plan = plan with { Methods = plan.Methods.Add(new(new(new(body.Binding.Method.ContentHash, 0x0600ffff), body.Binding.Owner,
                ObjectMethodKind.Instance, MethodAttributes.Public, true), "Extra", body.Parameters, IrType.Int32)) }; break;
            case "default_methods": plan = plan with { Methods = default }; break;
            case "default_targets": map = map with { Targets = default }; break;
            case "null_declaration": plan = plan with { Declarations = plan.Declarations.SetItem(0, null!) }; break;
        }
        if (damage is "slot_identity" or "slot_name" or "body_return") plan = plan with { Methods = plan.Methods.Replace(plan.Methods.Single(m => m.Binding.Method == body.Binding.Method), body) };
        if (damage is not "missing_map" and not "duplicate_map") plan = plan with { Dispatch = plan.Dispatch.SetItem(0, map) };
        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan));
    }

    private static (ResolutionAssemblyBuilder Builder, TypeDefinitionHandle Root, MethodDefinitionHandle Declaration) Raw(string damage = "")
    {
        var builder = new ResolutionAssemblyBuilder(new("InterfacePlan", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); var contract = builder.Type("Example", "Read", attributes: Interface);
        var declaration = builder.Method("Read", damage == "storage" ? [0x20, 0, 10] : [0x20, 0, 8], attributes: damage == "default" ? Virtual | MethodAttributes.NewSlot : Abstract,
            emit: damage == "default" ? il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); } : null);
        var root = builder.Type("Example", "Root", baseType: rootObject); builder.Implements(root, contract);
        if (damage != "missing") builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot,
            emit: damage == "runtime" ? null : il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        return (builder, root, declaration);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("storage")]
    [InlineData("missing")]
    [InlineData("runtime")]
    [InlineData("duplicate")]
    public void Unsupported_requests_return_diagnostics_without_partial_plans(string damage)
    {
        var raw = Raw(damage); using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", raw.Builder.Build())).Value!;
        var declaration = resolver.ResolveDeclaredMethod(image, raw.Declaration).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver, [resolver.ResolveType(image, raw.Root).Value!],
            damage == "duplicate" ? [declaration, declaration] : [declaration], image);
        Assert.Null(result.Plan); Assert.Single(result.Diagnostics);
        Assert.Equal(damage == "storage" ? DiagnosticIds.ExecutionSignatureUnsupported : damage == "missing" ? DiagnosticIds.MemberNotFound
            : DiagnosticIds.ExecutionUnsupported, result.Diagnostics[0].Id);
    }

    [Fact]
    public void Preparation_obeys_exact_short_and_exhausted_shared_metadata_budgets()
    {
        var raw = Raw(); using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", raw.Builder.Build())).Value!;
        var root = resolver.ResolveType(image, raw.Root).Value!; var declaration = resolver.ResolveDeclaredMethod(image, raw.Declaration).Value!;
        var measured = new MemberResolutionBudget(); Assert.True(ObjectInterfacePlanBuilder.Build(resolver, [root], [declaration], image, measured).Success);
        int used = 65536 - measured.Remaining;
        var exact = new MemberResolutionBudget(used); Assert.True(ObjectInterfacePlanBuilder.Build(resolver, [root], [declaration], image, exact).Success); Assert.Equal(0, exact.Remaining);
        var shortResult = ObjectInterfacePlanBuilder.Build(resolver, [root], [declaration], image, new(used - 1));
        Assert.Null(shortResult.Plan); Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(shortResult.Diagnostics).Id);
        var exhausted = new MemberResolutionBudget(1); Assert.True(exhausted.TryConsume(1));
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(ObjectInterfacePlanBuilder.Build(resolver, [root], [declaration], image, exhausted).Diagnostics).Id);
    }

    [Fact]
    public void Foreign_readers_are_rejected_before_equal_content_deduplication()
    {
        var raw = Raw(); using var files = new ResolverFiles(); var path = files.Write("input.dll", raw.Builder.Build());
        using var first = files.Open(); using var second = files.Open();
        var own = first.OpenInput(path).Value!; var foreign = second.OpenInput(path).Value!;
        var root = first.ResolveType(own, raw.Root).Value!; var declaration = second.ResolveDeclaredMethod(foreign, raw.Declaration).Value!;
        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanBuilder.Build(first, [root], [declaration], own));
    }

    [Fact]
    public void Hiding_cannot_replace_a_valid_inherited_interface_mapping()
    {
        var plan = Authored(); int hiddenToken = typeof(ExactInterfaceHidden).GetMethod("Read", [typeof(int)])!.MetadataToken;
        var hidden = plan.Methods.Single(m => m.Binding.Method.Token == hiddenToken);
        var declaration = Declarations.Single(m => m.DeclaringType == typeof(IExactRead) && m.Name == "Read" && m.GetParameters().Length == 1);
        var map = plan.Dispatch.Single(m => m.Declaration.Token == declaration.MetadataToken);
        var inherited = map.Targets.Single(t => t.Receiver.Token == typeof(ExactInterfaceHidden).MetadataToken);
        // Both body and mapping agree on a real, matching hidden slot. The missing
        // reimplementation edge is what makes this replacement invalid.
        var changed = map with { Targets = map.Targets.Replace(inherited, inherited with
            { Mapping = hidden.Binding.Method, Implementation = hidden.Binding.Method }) };
        var exception = Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Dispatch = plan.Dispatch.Replace(map, changed) }));
        Assert.Contains("inherited interface mapping", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_private_final_mapping_cannot_acquire_a_derived_override()
    {
        var plan = Authored(); var map = plan.Dispatch[0];
        var inherited = map.Targets.Single(t => t.Receiver.Token == typeof(ExactInterfaceExplicitInherited).MetadataToken);
        var mapping = plan.Methods.Single(m => m.Binding.Method == inherited.Mapping);
        var id = new IrMethodId(inherited.Receiver.ContentHash, 0x0600ffff);
        var replacement = mapping with { Binding = mapping.Binding with { Method = id, Owner = inherited.Receiver,
            Attributes = MethodAttributes.Public | MethodAttributes.Virtual }, Parameters = mapping.Parameters.SetItem(0, IrType.Reference(inherited.Receiver)) };
        var changed = plan with { Methods = plan.Methods.Add(replacement), Dispatch = plan.Dispatch.SetItem(0,
            map with { Targets = map.Targets.Replace(inherited, inherited with { Implementation = id }) }) };
        Assert.Contains("final class implementation", Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(changed)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_and_void_declarations_keep_exact_signatures_without_executable_bodies()
    {
        var builder = new ResolutionAssemblyBuilder(new("ReferenceContracts", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); var contract = builder.Type("Example", "Ref", attributes: Interface);
        byte[] referenceSignature = [0x20, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(contract) * 4), 0x1c];
        var identity = builder.Method("Identity", referenceSignature, attributes: Abstract);
        var clear = builder.Method("Clear", [0x20, 0, 1], attributes: Abstract);
        var root = builder.Type("Example", "Root", baseType: rootObject); builder.Implements(root, contract);
        builder.Method("Identity", referenceSignature, attributes: MethodAttributes.Public, emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ret); });
        builder.Method("Clear", [0x20, 0, 1], attributes: MethodAttributes.Public, emit: il => il.OpCode(ILOpCode.Ret));
        using var files = new ResolverFiles(); using var resolver = files.Open(); var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver, [resolver.ResolveType(image, root).Value!],
            [resolver.ResolveDeclaredMethod(image, identity).Value!, resolver.ResolveDeclaredMethod(image, clear).Value!], image);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var plan = result.Plan!;
        Assert.Equal(IrType.Void, plan.Declarations.Single(d => d.Name == "Clear").ReturnType);
        var declaration = plan.Declarations.Single(d => d.Name == "Identity");
        Assert.Equal(IrType.Reference(declaration.Owner), declaration.ReturnType); Assert.Equal(IrType.Reference(plan.Types.ObjectBase), declaration.Parameters[1]);
        ObjectInterfacePlanValidator.Validate(plan);
        var body = plan.Methods.Single(m => m.Name == "Identity");
        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(body, body with
            { ReturnType = IrType.Reference(plan.Types.ObjectBase) }) })); // Return covariance is not part of this profile.
    }

    private static ObjectInterfaceDispatchPlan Small()
    {
        var raw = Raw(); using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", raw.Builder.Build())).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver, [resolver.ResolveType(image, raw.Root).Value!],
            [resolver.ResolveDeclaredMethod(image, raw.Declaration).Value!], image);
        Assert.True(result.Success); return result.Plan!;
    }

    private static ObjectInterfaceDispatchPlan Expanded(int count, int parameters = 1)
    {
        var seed = Small(); var declaration = seed.Declarations[0]; var method = seed.Methods[0]; var target = seed.Dispatch[0].Targets[0];
        var declarations = ImmutableArray.CreateBuilder<ObjectInterfaceDeclaration>(); var methods = ImmutableArray.CreateBuilder<ObjectDispatchMethod>();
        var maps = ImmutableArray.CreateBuilder<ObjectInterfaceDispatch>();
        for (int i = 0; i < count; i++)
        {
            var declarationId = new IrMethodId(declaration.Owner.ContentHash, 0x06000100 + i);
            var methodId = new IrMethodId(method.Binding.Owner.ContentHash, 0x06001000 + i); string name = "Read" + i;
            declarations.Add(declaration with { Source = declaration.Source with { Token = declarationId.Token }, Name = name,
                Parameters = [declaration.Parameters[0], .. Enumerable.Repeat(IrType.Int32, parameters - 1)] });
            methods.Add(method with { Name = name, Binding = method.Binding with { Method = methodId, VirtualName = name,
                VirtualSlot = new(methodId, method.Binding.Owner) }, Parameters = [method.Parameters[0], .. Enumerable.Repeat(IrType.Int32, parameters - 1)] });
            maps.Add(new(declarationId, [target with { Mapping = methodId, Implementation = methodId }]));
        }
        return seed with { Declarations = declarations.ToImmutable(), Methods = methods.ToImmutable(), Dispatch = maps.ToImmutable() };
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Declarations_and_class_facts_share_the_exact_method_limit(int count, bool success)
    {
        var plan = Expanded(count);
        if (success) ObjectInterfacePlanValidator.Validate(plan);
        else Assert.Contains("512-method limit", Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregate_signature_storage_has_an_exact_and_one_parameter_excess_boundary()
    {
        var plan = Expanded(128, 256); ObjectInterfacePlanValidator.Validate(plan); // 256 records * 256 parameters = 65,536.
        var declaration = plan.Declarations[0];
        plan = plan with { Declarations = plan.Declarations.SetItem(0, declaration with { Parameters = declaration.Parameters.Add(IrType.Int32) }) };
        Assert.Contains("aggregate declaration parameter limit", Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equal_named_equal_token_declarations_from_different_images_remain_separate_maps()
    {
        var coreIdentity = new AssemblyIdentity("PlanCore", new Version(1, 0, 0, 0)); var core = new ResolutionAssemblyBuilder(coreIdentity);
        core.Type("System", "Object"); core.Type("Example", "Read", attributes: Interface);
        var coreDeclaration = core.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var app = new ResolutionAssemblyBuilder(new("PlanApp", new Version(1, 0, 0, 0)));
        app.Type("Example", "Padding"); var appContract = app.Type("Example", "Read", attributes: Interface);
        var appDeclaration = app.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var scope = app.Reference(coreIdentity); var objectRef = app.TypeReference("System", "Object", scope);
        var contractRef = app.TypeReference("Example", "Read", scope); var root = app.Type("Example", "Root", baseType: objectRef);
        app.Implements(root, appContract); app.Implements(root, contractRef);
        var body = app.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs"); var coreImage = resolver.OpenInput(corePath).Value!; var appImage = resolver.OpenInput(appPath).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver, [resolver.ResolveType(appImage, root).Value!],
            [resolver.ResolveDeclaredMethod(coreImage, coreDeclaration).Value!, resolver.ResolveDeclaredMethod(appImage, appDeclaration).Value!], coreImage);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var plan = result.Plan!;
        Assert.Equal(2, plan.Dispatch.Length); Assert.Single(plan.Methods);
        Assert.Equal(plan.Dispatch[0].Declaration.Token, plan.Dispatch[1].Declaration.Token);
        Assert.NotEqual(plan.Dispatch[0].Declaration.ContentHash, plan.Dispatch[1].Declaration.ContentHash);
        Assert.All(plan.Dispatch, m => Assert.Equal(new IrMethodId(appImage.ContentHash, MetadataTokens.GetToken(body)), Assert.Single(m.Targets).Implementation));
        ObjectInterfacePlanValidator.Validate(plan);
    }
}
