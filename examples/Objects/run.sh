#!/usr/bin/env bash
set -euo pipefail

example_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_dir="$(cd -- "$example_dir/../.." && pwd)"
configuration=Release
optimization=O2
output_dir="$repository_dir/out/objects"
build=true
build_options=(-m:1 -p:UseSharedCompilation=false -nodeReuse:false)
while (($#)); do
    case "$1" in
        --optimization) optimization="${2:?Missing optimization}"; shift 2 ;;
        --no-build) build=false; shift ;;
        --no-restore) build_options+=(--no-restore); shift ;;
        --configuration) configuration="${2:?Missing configuration}"; shift 2 ;;
        --output) output_dir="${2:?Missing output directory}"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done
case "$configuration" in Debug|Release) ;; *) echo "Configuration must be Debug or Release" >&2; exit 2 ;; esac
case "$optimization" in O0|O2) ;; *) echo "Optimization must be O0 or O2" >&2; exit 2 ;; esac
# Run from the repository so global.json selects its pinned SDK.
cd -- "$repository_dir"
if "$build"; then
    dotnet build "$example_dir/Prepare/Prepare.csproj" -c "$configuration" \
        "${build_options[@]}"
fi
dotnet "$example_dir/Prepare/bin/$configuration/net10.0/Cilica.Examples.Objects.Prepare.dll" "$output_dir"
dotnet "$example_dir/Emit/bin/$configuration/net10.0/Cilica.Examples.Objects.Emit.dll" "$output_dir"
"${CILICA_CXX:-clang++}" -std=c++20 "-$optimization" -Wall -Wextra -Werror -fno-exceptions -fno-rtti \
    -Wframe-larger-than=2048 -fsanitize=address,undefined -fno-sanitize-recover=all -I "$output_dir" \
    "$example_dir/driver.cpp" -o "$output_dir/objects"
"$output_dir/objects" > "$output_dir/actual.txt"
diff -u -- "$output_dir/expected.txt" "$output_dir/actual.txt"
cat -- "$output_dir/actual.txt"
echo "Native output matches .NET; generated source: $output_dir/generated.cpp"
