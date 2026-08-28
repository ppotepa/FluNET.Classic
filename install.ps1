[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$project = Join-Path $repoRoot "src\Hosts\FluNET.Classic.Cli\FluNET.Classic.Cli.csproj"
$tool = "FluNET.Classic.Cli"
$tempPackageDirectory = Join-Path ([IO.Path]::GetTempPath()) ("flunet-classic-" + [Guid]::NewGuid().ToString("N"))

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is required. Install .NET 8 SDK and run this installer again."
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "Could not find the FluNET.Classic CLI project: $project"
}

New-Item -ItemType Directory -Path $tempPackageDirectory -Force | Out-Null
try {
    Push-Location $repoRoot
    & dotnet pack $project --configuration Release --output $tempPackageDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

    $package = Get-ChildItem -LiteralPath $tempPackageDirectory -Filter "$tool.*.nupkg" | Select-Object -First 1
    if ($null -eq $package) { throw "The CLI package was not produced." }
    $version = $package.BaseName.Substring($tool.Length + 1)
    $installed = @(& dotnet tool list --global 2>$null) | Where-Object { $_ -match "\b$([Regex]::Escape($tool))\b" }

    if ($installed) {
        & dotnet tool update --global $tool --version $version --add-source $tempPackageDirectory --no-cache
    }
    else {
        & dotnet tool install --global $tool --version $version --add-source $tempPackageDirectory --no-cache
    }
    if ($LASTEXITCODE -ne 0) { throw "The global tool installation failed with exit code $LASTEXITCODE." }

    Write-Host "Installed FluNET.Classic $version as the 'fluc' command."
    Write-Host "The existing 'flunet' installation was not changed."
    & fluc --help
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $tempPackageDirectory) {
        Remove-Item -LiteralPath $tempPackageDirectory -Recurse -Force
    }
}
