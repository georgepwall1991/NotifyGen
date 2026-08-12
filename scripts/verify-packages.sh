#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "usage: $0 <nupkg-or-directory>" >&2
  exit 2
fi

package_path="$1"
if [[ -d "$package_path" ]]; then
  package_path="$(ls -1 "$package_path"/NotifyGen.*.nupkg | head -n1)"
fi

if [[ ! -f "$package_path" ]]; then
  echo "no NotifyGen nupkg at $1" >&2
  exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
unzip -qq "$package_path" -d "$work"

readme="$work/README.md"
nuspec="$(echo "$work"/*.nuspec)"

test -s "$readme"
test -s "$work/icon.png"
test -s "$work/assets/icon.png"
test -s "$work/assets/header.png"
test -s "$work/assets/demo.gif"

grep -q 'INotifyPropertyChanged' "$nuspec"
grep -q 'no runtime' "$nuspec"
grep -q 'ObservableObject' "$nuspec"
grep -q 'CommunityToolkit' "$nuspec"
if grep -q 'ReactiveUI' "$nuspec"; then
  echo "nuspec still contains ReactiveUI" >&2
  exit 1
fi

grep -q 'PrivateAssets' "$readme"
grep -q '2.0.2' "$readme"
if grep -q 'georgepwall1991.github.io/NotifyGen' "$readme"; then
  echo "README still advertises the unpublished Pages URL" >&2
  exit 1
fi

echo "verified $(basename "$package_path")"
