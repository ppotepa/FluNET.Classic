#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$repo_root/src/Hosts/FluNET.Classic.Cli/FluNET.Classic.Cli.csproj"
tool="FluNET.Classic.Cli"
package_directory="$(mktemp -d "${TMPDIR:-/tmp}/flunet-classic.XXXXXX")"
trap 'rm -rf "$package_directory"' EXIT

command -v dotnet >/dev/null 2>&1 || {
  echo "The .NET SDK is required. Install .NET 8 SDK and run this installer again." >&2
  exit 1
}

dotnet pack "$project" --configuration Release --output "$package_directory" --nologo
package="$(find "$package_directory" -maxdepth 1 -type f -name "$tool.*.nupkg" -print -quit)"
if [[ -z "$package" ]]; then
  echo "The CLI package was not produced." >&2
  exit 1
fi

package_name="$(basename "$package")"
version="${package_name#"$tool."}"
version="${version%.nupkg}"

if dotnet tool list --global 2>/dev/null | grep -q "${tool}"; then
  dotnet tool update --global "$tool" --version "$version" --add-source "$package_directory" --no-cache
else
  dotnet tool install --global "$tool" --version "$version" --add-source "$package_directory" --no-cache
fi

echo "Installed FluNET.Classic $version as the 'fluc' command."
echo "The existing 'flunet' installation was not changed."
fluc --help
