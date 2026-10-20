from pathlib import Path
import hashlib, json, os, re, subprocess, sys
root = Path.cwd()
recipe = Path(__file__).resolve().parent
out = Path(sys.argv[1]).resolve()
source = out / "plain.cpp"
env = dict(os.environ, CILICA_APPROVED_DIFFPLEX_PIN="f500e73f28e28813f942f1f16dec5a34b40e48e3",
    CILICA_DIFFPLEX_ASSEMBLY=str(Path(sys.argv[2]).resolve()),
    CILICA_BENCH_TEMPLATE=str(recipe.parent / "native-host.cpp.template"),
    CILICA_BENCH_CPP=str(source), CILICA_BENCH_CORE_CAPTURE=str(out / "core"))
emit = ["dotnet", "tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll",
        "-explicit", "only", "-method", "Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark",
        "-culture", "invariant", "-noColor"]
result = subprocess.run(emit, env=env, text=True, capture_output=True, timeout=180)
(out / "emission.log").write_text(result.stdout + result.stderr)
result.check_returncode()
baseline = root / "artifacts/splitcache-20261020/splitcache.cpp"
assert source.read_bytes() == baseline.read_bytes(), "Current emission must match accepted committed core/compiler"
text = source.read_text()
app_hash = hashlib.sha256(Path(sys.argv[2]).read_bytes()).hexdigest()
mapping = json.loads((out / "prepare.json").read_text())["boundaries"]
for boundary in mapping:
    symbol = "cilica_m_" + app_hash + "_" + boundary["original_token"]
    pattern = r"(^[^\n;]*\b" + symbol + r"\([^\n]*\) noexcept \{)"
    text, count = re.subn(pattern, lambda m: m[0] + "\n    section_clock::Scope section_scope(" +
                         str(boundary["section"]) + ");", text, flags=re.M)
    assert count == 1, (symbol, count)
text = (recipe / "section-clock.h").read_text() + "\n" + text
text = text.replace('int main(int argc,char** argv) {',
    'int main(int argc,char** argv) {\n section_clock::enabled=argc>4 && std::strcmp(argv[4],"on")==0;')
text = text.replace('for(int sample=0;sample<7;++sample) {',
    'for(int sample=0;sample<7;++sample) {\n section_clock::reset();')
old = next(line for line in text.splitlines() if 'std::printf("{\\"sample\\"' in line)
new = r'''        if(section_clock::current!=-1) std::abort();
        const std::int64_t expected[4]{iterations,2LL*iterations,2LL*iterations,iterations};
        for(int i=0;i<4;++i) if(section_clock::calls[i]!=(section_clock::enabled?expected[i]:0)) std::abort();
        std::printf("{\"sample\":%d,\"iterations\":%d,\"work\":%d,\"elapsedTicks\":%lld,\"frequency\":1000000000,\"checksum\":%d,\"sectionTicks\":[%lld,%lld,%lld,%lld],\"calls\":[%lld,%lld,%lld,%lld]}\n",
            sample,iterations,work,(long long)ns,checksum,
            (long long)section_clock::ticks[0],(long long)section_clock::ticks[1],(long long)section_clock::ticks[2],(long long)section_clock::ticks[3],
            (long long)section_clock::calls[0],(long long)section_clock::calls[1],(long long)section_clock::calls[2],(long long)section_clock::calls[3]);'''
assert old in text
text = text.replace(old, new)
(out / "sections.cpp").write_text(text)
commands = []
for name in ["plain", "sections"]:
    cmd = ["clang++", "-std=c++20", "-O2", "-DNDEBUG", "-fno-exceptions", "-fno-rtti",
           "-Wall", "-Wextra", "-Werror", str(out / (name + ".cpp")), "-o", str(out / name)]
    result = subprocess.run(cmd, text=True, capture_output=True, timeout=180)
    (out / (name + "-build.log")).write_text(result.stdout + result.stderr)
    result.check_returncode()
    commands.append(cmd)
(out / "native-provenance.json").write_text(json.dumps({
    "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
    "commands": [emit, *commands], "environment": {k:v for k,v in env.items() if k.startswith("CILICA_")},
    "files": {str(p.relative_to(out)): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in [source, out / "sections.cpp", out / "plain", out / "sections", out / "core/Cilica.Core.dll"]},
    "fresh_emission_matches_committed_baseline": True, "boundaries": mapping,
    "instrumentation": "Diagnostic generated C++ copies only; compiler and runtime source unchanged"
}, indent=2))
print("Native section timers built")
