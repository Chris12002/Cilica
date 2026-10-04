using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeLocationCarrierTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scalar_carriers_authenticate_lifetimes_types_and_owners_before_native_access(bool optimized)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(LocationProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LocationProbe).GetMethod(nameof(LocationProbe.Build))!.MetadataToken), library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var program = result.Program!;
        var artifact = ObjectCppEmitter.EmitProgram(program);
        static string Symbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
        string Type(Type type) => Symbol(ObjectTypeId.From(program.Flow.Types.Types.Single(t =>
            t.Source.ContentHash == input.ContentHash && t.Source.Token == type.MetadataToken).Source));
        string node = Type(typeof(LocationNode)), parent = Type(typeof(LocationBase)), objectType = Symbol(program.Flow.Types.ObjectBase);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <memory>
            #include <vector>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static constexpr ReferencePredicate node_slots[] = { {{node}}_accepts, {{node}}_accepts };
            [[gnu::noinline]] static void local_slots_and_nested_borrow() {
                Instance heap; Context c(heap);
                std::uint32_t numbers[] = {17u, 23u}; Object* references[] = {nullptr, nullptr}; Frame frame;
                RootScope roots(c, frame, references, 2);
                BorrowScope borrow(c, frame, numbers, 2, node_slots);
                assert(borrow.status == LocationStatus::complete);
                const auto first = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0);
                const auto second = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1);
                assert(first.status == LocationStatus::complete && second.status == LocationStatus::complete);
                std::uint32_t bits = 99; Object* value = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(load_location_integer(c, first.value, bits) == LocationStatus::complete && bits == 17u);
                assert(store_location_integer(c, first.value, 0xffffffffu) == LocationStatus::complete && numbers[0] == 0xffffffffu);
                assert(load_location_reference(c, second.value, value) == LocationStatus::complete && value == nullptr);
                {
                    Frame nested; RootScope inner(c, nested, nullptr, 0); BorrowScope child(c, nested, nullptr, 0, nullptr);
                    assert(child.status == LocationStatus::complete);
                    assert(store_location_integer(c, first.value, 31u) == LocationStatus::complete && numbers[0] == 31u);
                }
                // Wrong-kind operations never reinterpret a pointer slot as an integer.
                bits = 99; value = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(load_location_integer(c, second.value, bits) == LocationStatus::invalid && bits == 99);
                assert(load_location_reference(c, first.value, value) == LocationStatus::invalid && value == reinterpret_cast<Object*>(std::uintptr_t{1}));
                assert(store_location_integer(c, second.value, 1) == LocationStatus::invalid && references[1] == nullptr);
                assert(store_location_reference(c, first.value, nullptr) == LocationStatus::invalid && numbers[0] == 31u);
                const auto readonly = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0, true);
                assert(readonly.status == LocationStatus::complete && store_location_integer(c, readonly.value, 9) == LocationStatus::read_only);
                assert(numbers[0] == 31u);
                assert(frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 2).status == LocationStatus::invalid);
                assert(frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 2).status == LocationStatus::invalid);
                assert(frame_location(c, borrow.frame.token, LocationOrigin::heap_field, 0).status == LocationStatus::invalid);
                assert(frame_location(c, 0, LocationOrigin::frame_integer, 0).status == LocationStatus::invalid);
            }
            [[gnu::noinline]] static void frame_reuse_and_context_isolation() {
                Instance heap; Context c(heap), other(heap);
                std::uint32_t integer = 5; Frame frame; Location stale;
                {
                    RootScope roots(c, frame, nullptr, 0); BorrowScope borrowed(c, frame, &integer, 1, nullptr);
                    stale = frame_location(c, borrowed.frame.token, LocationOrigin::frame_integer, 0).value;
                    assert(store_location_integer(other, stale, 99) == LocationStatus::invalid && integer == 5);
                }
                assert(c.borrows == nullptr && c.top == nullptr && c.depth == 0);
                std::uint32_t output = 71;
                assert(load_location_integer(c, stale, output) == LocationStatus::invalid && output == 71);
                {
                    RootScope roots(c, frame, nullptr, 0); BorrowScope replacement(c, frame, &integer, 1, nullptr);
                    assert(replacement.frame.token != stale.frame);
                    assert(store_location_integer(c, stale, 99) == LocationStatus::invalid && integer == 5);
                    auto fresh = frame_location(c, replacement.frame.token, LocationOrigin::frame_integer, 0).value;
                    assert(store_location_integer(c, fresh, 13) == LocationStatus::complete && integer == 13);
                }
                // A surviving registration is explicitly retired by root-frame pop.
                auto roots = std::make_unique<RootScope>(c, frame, nullptr, 0);
                BorrowScope borrowed(c, frame, &integer, 1, nullptr);
                auto expired = frame_location(c, borrowed.frame.token, LocationOrigin::frame_integer, 0).value;
                roots.reset();
                { RootScope reused(c, frame, nullptr, 0);
                  assert(store_location_integer(c, expired, 99) == LocationStatus::invalid && integer == 13); }
            }
            [[gnu::noinline]] static void registration_limits() {
                Instance heap; Context c(heap); Frame unpublished; std::uint32_t integer = 1;
                BorrowScope missing(c, unpublished, &integer, 1, nullptr);
                assert(missing.status == LocationStatus::invalid && c.borrows == nullptr);
                Frame frame; Object* slot = nullptr; RootScope roots(c, frame, &slot, 1);
                BorrowScope no_types(c, frame, &integer, 1, nullptr);
                assert(no_types.status == LocationStatus::invalid);
                BorrowScope no_storage(c, frame, nullptr, 1, node_slots);
                assert(no_storage.status == LocationStatus::invalid);
                BorrowScope too_wide(c, frame, &integer, 65537, node_slots);
                assert(too_wide.status == LocationStatus::invalid);
                BorrowScope good(c, frame, &integer, 1, node_slots);
                assert(good.status == LocationStatus::complete);
                BorrowScope duplicate(c, frame, &integer, 1, node_slots);
                assert(duplicate.status == LocationStatus::invalid);
                auto location = frame_location(c, good.frame.token, LocationOrigin::frame_integer, 0).value;
                ++frame.count;
                assert(store_location_integer(c, location, 99) == LocationStatus::invalid && integer == 1);
                --frame.count;
                location.slot = std::numeric_limits<std::size_t>::max();
                assert(store_location_integer(c, location, 99) == LocationStatus::invalid && integer == 1);
            }
            [[gnu::noinline]] static void heap_fields_and_reference_checks() {
                Instance heap, foreign; Context c(heap), elsewhere(foreign);
                Object* owned[3]{}; Frame frame; RootScope roots(c, frame, owned, 3);
                Object* owner = owned[0] = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* child = owned[1] = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* incompatible = owned[2] = allocate<{{parent}}>(c, {{parent}}_descriptor);
                Object* outsider = allocate<{{node}}>(elsewhere, {{node}}_descriptor);
                assert(owner && child && incompatible && outsider);
                const auto number = heap_location(c, owner, &{{parent}}_descriptor, 0);
                const auto next = heap_location(c, owner, &{{parent}}_descriptor, 1);
                const auto initial = heap_location(c, owner, &{{parent}}_descriptor, 2);
                assert(number.status == LocationStatus::complete && next.status == LocationStatus::complete && initial.status == LocationStatus::complete);
                assert(store_location_integer(c, number.value, 37) == LocationStatus::complete);
                std::uint32_t integer = 0; assert(load_location_integer(c, number.value, integer) == LocationStatus::complete && integer == 37);
                assert(store_location_integer(c, initial.value, 9) == LocationStatus::read_only);
                auto forged_write = initial.value; forged_write.read_only = false;
                assert(store_location_integer(c, forged_write, 9) == LocationStatus::read_only);
                assert(store_location_reference(c, next.value, child) == LocationStatus::complete);
                Object* observed = nullptr;
                assert(load_location_reference(c, next.value, observed) == LocationStatus::complete && observed == child);
                for (Object* bad : {incompatible, outsider, reinterpret_cast<Object*>(std::uintptr_t{1})}) {
                    assert(store_location_reference(c, next.value, bad) == LocationStatus::invalid);
                    observed = nullptr; assert(load_location_reference(c, next.value, observed) == LocationStatus::complete && observed == child);
                }
                assert(heap_location(c, nullptr, &{{parent}}_descriptor, 0).status == LocationStatus::null_reference);
                assert(heap_location(c, outsider, &{{parent}}_descriptor, 0).status == LocationStatus::invalid);
                assert(heap_location(c, reinterpret_cast<Object*>(std::uintptr_t{1}), &{{parent}}_descriptor, 0).status == LocationStatus::invalid);
                assert(heap_location(c, owner, &{{objectType}}_descriptor, 0).status == LocationStatus::invalid);
                assert(heap_location(c, incompatible, &{{node}}_descriptor, 0).status == LocationStatus::invalid);
                const Descriptor copied = {{parent}}_descriptor;
                assert(heap_location(c, owner, &copied, 0).status == LocationStatus::invalid);
                assert(heap_location(c, owner, reinterpret_cast<const Descriptor*>(std::uintptr_t{1}), 0).status == LocationStatus::invalid);
                auto wrong = next.value; wrong.frame = 1;
                assert(store_location_reference(c, wrong, nullptr) == LocationStatus::invalid);
                // Corrupted contained values are rejected before their descriptor is read.
                *static_cast<Object**>(field_address(owner, 1)) = reinterpret_cast<Object*>(std::uintptr_t{1});
                observed = child;
                assert(load_location_reference(c, next.value, observed) == LocationStatus::invalid && observed == child);
                *static_cast<Object**>(field_address(owner, 1)) = nullptr;
            }
            [[gnu::noinline]] static void exact_registration_bounds() {
                Instance heap; Context c(heap);
                std::vector<Object*> references(1024);
                std::vector<ReferencePredicate> accepts(1024, {{node}}_accepts);
                std::vector<std::uint32_t> integers(65536);
                Frame frame; RootScope roots(c, frame, references.data(), references.size());
                BorrowScope borrow(c, frame, integers.data(), integers.size(), accepts.data());
                assert(borrow.status == LocationStatus::complete);
                auto integer = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 65535).value;
                auto reference = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1023).value;
                assert(store_location_integer(c, integer, 19) == LocationStatus::complete && integers.back() == 19);
                assert(store_location_reference(c, reference, nullptr) == LocationStatus::complete);
            }
            [[gnu::noinline]] static void exact_depth_bound() {
                Instance heap; Context c(heap);
                std::vector<Frame> frames(65);
                std::vector<std::unique_ptr<RootScope>> roots;
                std::vector<std::unique_ptr<BorrowScope>> borrows;
                for (int i = 0; i < 64; ++i) {
                    roots.push_back(std::make_unique<RootScope>(c, frames[i], nullptr, 0));
                    borrows.push_back(std::make_unique<BorrowScope>(c, frames[i], nullptr, 0, nullptr));
                    assert(borrows.back()->status == LocationStatus::complete);
                }
                { RootScope overflow(c, frames[64], nullptr, 0);
                  BorrowScope rejected(c, frames[64], nullptr, 0, nullptr);
                  assert(rejected.status == LocationStatus::invalid); }
                while (!borrows.empty()) { borrows.pop_back(); roots.pop_back(); }
                assert(c.borrows == nullptr && c.top == nullptr && c.depth == 0);
            }
            [[gnu::noinline]] static void frame_reference_collection() {
                Instance heap; Context c(heap);
                Object* references[2]{}; Frame frame; RootScope roots(c, frame, references, 2);
                BorrowScope borrow(c, frame, nullptr, 0, node_slots);
                auto location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1).value;
                Object* child = allocate<{{node}}>(c, {{node}}_descriptor);
                assert(store_location_reference(c, location, child) == LocationStatus::complete);
                heap.collect_before_allocation = true; heap.object_limit = 2;
                for (int i = 0; i < 20; ++i) {
                    assert(allocate<{{node}}>(c, {{node}}_descriptor) != nullptr);
                    Object* observed = nullptr;
                    assert(load_location_reference(c, location, observed) == LocationStatus::complete && observed == child);
                }
                Object* incompatible = allocate<{{parent}}>(c, {{parent}}_descriptor);
                assert(incompatible != nullptr);
                assert(store_location_reference(c, location, incompatible) == LocationStatus::invalid && references[1] == child);
                assert(store_location_reference(c, location, reinterpret_cast<Object*>(std::uintptr_t{1})) == LocationStatus::invalid && references[1] == child);
                assert(collect(c).unreachable == 1 && heap.count == 1);
                assert(store_location_reference(c, location, nullptr) == LocationStatus::complete);
                assert(collect(c).unreachable == 1 && heap.count == 0);
            }
            [[gnu::noinline]] static void rooted_location_collection() {
                Instance heap; Context c(heap);
                Location location = heap_location(c, allocate<{{node}}>(c, {{node}}_descriptor), &{{parent}}_descriptor, 1).value;
                Frame frame;
                {
                    RootScope roots(c, frame, &location.owner, 1);
                    Object* child = allocate<{{node}}>(c, {{node}}_descriptor);
                    assert(store_location_reference(c, location, child) == LocationStatus::complete);
                    heap.collect_before_allocation = true; heap.object_limit = 3;
                    for (int i = 0; i < 20; ++i) {
                        assert(allocate<{{node}}>(c, {{node}}_descriptor) != nullptr);
                        Object* observed = nullptr;
                        assert(load_location_reference(c, location, observed) == LocationStatus::complete && observed == child);
                    }
                    assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                    // Writes immediately change the owner's precise reference edge.
                    assert(store_location_reference(c, location, nullptr) == LocationStatus::complete);
                    assert(collect(c).unreachable == 1 && heap.count == 1);
                }
                assert(collect(c).unreachable == 1 && heap.count == 0 && heap.bytes == 0);
                Object* unchanged = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(load_location_reference(c, location, unchanged) == LocationStatus::invalid && unchanged == reinterpret_cast<Object*>(std::uintptr_t{1}));
            }
            struct Busy { Context& context; const Location& location; };
            static bool busy_callback(void* opaque, Object*, bool) noexcept {
                auto& state = *static_cast<Busy*>(opaque); auto& c = state.context;
                assert(store_location_integer(c, state.location, 99) == LocationStatus::busy);
                std::uint32_t unchanged = 17;
                assert(load_location_integer(c, state.location, unchanged) == LocationStatus::busy && unchanged == 17);
                Frame frame; BorrowScope blocked(c, frame, nullptr, 0, nullptr);
                assert(blocked.status == LocationStatus::busy);
                return true;
            }
            [[gnu::noinline]] static void errors_preserve_faults_and_exhaustion_is_terminal() {
                Instance heap; Context c(heap);
                Location location = heap_location(c, allocate<{{node}}>(c, {{node}}_descriptor), &{{parent}}_descriptor, 0).value;
                Frame frame; RootScope roots(c, frame, &location.owner, 1);
                c.pending = 2; c.method = 19; c.il = 23; const char* image = "saved"; c.image = image;
                Busy state{c, location};
                assert(mark_reachable(c, {}, busy_callback, &state).status == MarkStatus::complete);
                assert(c.pending == 2 && c.method == 19 && c.il == 23 && c.image == image && c.borrows == nullptr);
                next_borrow_token.store(std::numeric_limits<std::uint64_t>::max() - 1);
                std::uint32_t integer = 1;
                { BorrowScope last(c, frame, &integer, 1, node_slots);
                  assert(last.status == LocationStatus::complete && last.frame.token == std::numeric_limits<std::uint64_t>::max() - 1); }
                { BorrowScope exhausted(c, frame, &integer, 1, node_slots);
                  assert(exhausted.status == LocationStatus::limit && c.borrows == nullptr); }
                { BorrowScope again(c, frame, &integer, 1, node_slots);
                  assert(again.status == LocationStatus::limit && c.borrows == nullptr); }
                assert(c.pending == 2 && c.method == 19 && c.il == 23 && c.image == image);
            }
            int main() {
                local_slots_and_nested_borrow(); frame_reuse_and_context_isolation(); registration_limits();
                exact_registration_bounds(); exact_depth_bound();
                heap_fields_and_reference_checks(); frame_reference_collection(); rooted_location_collection(); errors_preserve_faults_and_exhaustion_is_terminal();
            }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-locations");
        string cpp = Path.Combine(directory, "locations.cpp"), executable = Path.Combine(directory, "locations");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
