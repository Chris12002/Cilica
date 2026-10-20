"""Diagnostic sizing experiment: scalar frame slots -> native locals in direct bodies.

Reads retained v116 generated C++ and writes variant A (locals only) and variant B
(locals plus removal of the now-unused scalar frame arrays). Methods whose direct
body can observe scalar slots indirectly are left unchanged.
"""
import json, re, sys
from pathlib import Path

source, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
lines = source.read_text().splitlines(keepends=True)

SLOT = re.compile(r"f->(integers|wide_integers)\[(\d+)\]")
# Anything that can reach a scalar slot other than by direct indexing.
INDIRECT = re.compile(r"LocationOrigin::frame_(integer|wide|narrow|unsigned|signed|character)|_local_output\(|_local_character\(|"
                      r"integers\.data\(\)|&f->integers|&f->wide_integers|integers\.size\(\)")
METHOD = re.compile(r"context\.method = (0x[0-9a-f]+)u")

regions, i = [], 0
while i < len(lines):
    if lines[i].startswith("#if CILICA_DIRECT_CONTROL_FLOW") and any("RootScope roots(" in lines[k] for k in range(max(0, i - 8), i)):
        depth, j = 0, i + 1
        while True:
            l = lines[j]
            if l.startswith("#if"): depth += 1
            elif l.startswith("#endif"):
                if depth == 0: break
                depth -= 1
            elif l.startswith("#else") and depth == 0: break
            j += 1
        regions.append((i, j))
        i = j
    i += 1

# The storage struct name of each function is the type in its Storage<...> line.
def storage_name(start):
    for k in range(start, max(0, start - 40), -1):
        m = re.search(r"Storage<(\w+_storage)> storage", lines[k])
        if m: return m.group(1)
    return None

report = {"regions": len(regions), "converted": [], "skipped": []}
converted_structs = set()
replacements = {}
for start, end in regions:
    body = "".join(lines[start + 1:end])
    method = (METHOD.search(body) or [None, "?"])[1]
    # Slots a managed location can reach stay in the registered borrow frame.
    pinned = set()
    for m in re.finditer(r"LocationOrigin::frame_(integer|wide_integer), (\d+)", body):
        pinned.add(("integers" if m.group(1) == "integer" else "wide_integers", int(m.group(2))))
    for m in re.finditer(r"&f->(integers|wide_integers)\[(\d+)\]", body):
        pinned.add((m.group(1), int(m.group(2))))
    for m in re.finditer(r"_local_character\(context, f->integers\[(\d+)\]", body):
        pinned.add(("integers", int(m.group(1))))
    for m in re.finditer(r"_local_output\([^;]*?f->borrow_token, (\d+)\)", body):
        pinned |= {("integers", int(m.group(1))), ("wide_integers", int(m.group(1)))}
    if re.search(r"integers\.data\(\)|integers\.size\(\)", body):
        report["skipped"].append({"method": method, "reason": "whole scalar array referenced"})
        continue
    slots = sorted({(m.group(1), int(m.group(2))) for m in SLOT.finditer(body)} - pinned)
    if not slots:
        report["skipped"].append({"method": method, "reason": "no convertible scalar slots"})
        continue
    names = {s: ("cilica_i_" if s[0] == "integers" else "cilica_w_") + str(s[1]) for s in slots}
    decl = []
    narrow = [names[s] for s in slots if s[0] == "integers"]
    wide = [names[s] for s in slots if s[0] == "wide_integers"]
    if narrow: decl.append("    std::uint32_t " + ", ".join(n + " = 0" for n in narrow) + ";\n")
    if wide: decl.append("    std::uint64_t " + ", ".join(n + " = 0" for n in wide) + ";\n")
    new_body = SLOT.sub(lambda m: names.get((m.group(1), int(m.group(2))), m.group(0)), body)
    replacements[start] = (end, "".join(decl) + new_body)
    struct = storage_name(start)
    borrows = any("BorrowScope borrowed(" in lines[k] for k in range(max(0, start - 8), start))
    if not pinned and not borrows: converted_structs.add(struct)
    report["converted"].append({"method": method, "struct": struct, "narrow": len(narrow), "wide": len(wide),
                                "pinned": sorted(f"{k}[{n}]" for k, n in pinned)})

def emit(remove_arrays):
    result, i = [], 0
    while i < len(lines):
        if i in replacements:
            end, text = replacements[i]
            result.append(lines[i]); result.append(text); i = end; continue
        result.append(lines[i]); i += 1
    text = "".join(result)
    if remove_arrays:
        for struct in converted_structs:
            pattern = re.compile(r"(struct " + struct + r" \{.*?)std::array<std::uint32_t, \d+> integers\{\};\n"
                                 r"(\s*)std::array<std::uint64_t, \d+> wide_integers\{\};", re.S)
            text, n = pattern.subn(r"\1std::array<std::uint32_t, 0> integers_removed{};\n\2"
                                   r"std::array<std::uint64_t, 0> wide_integers_removed{};", text, count=1)
            assert n == 1, struct
    return text

out_dir.mkdir(parents=True, exist_ok=True)
(out_dir / "variant_a.cpp").write_text(emit(False))
(out_dir / "variant_b.cpp").write_text(emit(True))
report["converted_count"] = len(report["converted"])
report["skipped_count"] = len(report["skipped"])
(out_dir / "transform-report.json").write_text(json.dumps(report, indent=1))
print(json.dumps({k: report[k] for k in ("regions", "converted_count", "skipped_count")}))
