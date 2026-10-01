#!/usr/bin/env bash
set -euo pipefail

example_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_dir="$(cd -- "$example_dir/../.." && pwd)"
configuration=Release
output_dir="$repository_dir/out/primitive"
build=true
build_options=(-m:1 -p:UseSharedCompilation=false -nodeReuse:false)
while (($#)); do
    case "$1" in
        --no-build) build=false; shift ;;
        --no-restore) build_options+=(--no-restore); shift ;;
        --configuration) configuration="${2:?Missing configuration}"; shift 2 ;;
        --output) output_dir="${2:?Missing output directory}"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done
case "$configuration" in Debug|Release) ;; *) echo "Configuration must be Debug or Release" >&2; exit 2 ;; esac
# Run from the repository so global.json selects its pinned SDK.
cd -- "$repository_dir"
if "$build"; then
    dotnet build "$example_dir/Prepare/Prepare.csproj" -c "$configuration" \
        "${build_options[@]}"
fi
dotnet "$example_dir/Prepare/bin/$configuration/net10.0/Cilica.Examples.Primitive.Prepare.dll" "$output_dir"
assembly="$(cat -- "$output_dir/assembly.path")"
root="$(cat -- "$output_dir/root.token")"
dotnet "$repository_dir/src/Cilica.Cli/bin/$configuration/net10.0/cilica.dll" emit-cpp \
    "$assembly" --root "$root" --output "$output_dir/generated.cpp"
"${CILICA_CXX:-clang++}" -std=c++20 -O2 -Wall -Wextra -Werror -fno-exceptions -fno-rtti \
    -fsanitize=undefined -fno-sanitize-recover=all -I "$output_dir" \
    "$example_dir/driver.cpp" -o "$output_dir/primitive"
"$output_dir/primitive" > "$output_dir/actual.txt"
diff -u -- "$output_dir/expected.txt" "$output_dir/actual.txt"
cat -- "$output_dir/actual.txt"
echo "Native output matches .NET; generated source: $output_dir/generated.cpp"
