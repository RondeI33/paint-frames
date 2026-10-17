param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$binary = Join-Path $projectRoot 'prebuilt/PaintFrames.exe'
$checksumLine = (Get-Content -LiteralPath (Join-Path $projectRoot 'prebuilt/SHA256SUMS.txt') -Raw).Trim()
if ($checksumLine -notmatch '^([0-9a-fA-F]{64})  PaintFrames\.exe$') {
    throw 'Invalid prebuilt executable checksum file.'
}
$expected = $Matches[1]
$actual = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash
if ($actual -ne $expected) {
    throw 'Prebuilt executable does not match its recorded SHA-256. No release was packaged.'
}

$artifactDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$releaseBinary = Join-Path $artifactDirectory 'PaintFrames.exe'
Copy-Item -LiteralPath $binary -Destination $releaseBinary -Force
$zip = Join-Path $artifactDirectory ("PaintFrames-$Version-windows.zip")
Compress-Archive -LiteralPath $releaseBinary,(Join-Path $projectRoot 'READ-ME.txt') -DestinationPath $zip -Force

if ((Get-FileHash -LiteralPath $releaseBinary -Algorithm SHA256).Hash -ne $expected) {
    throw 'Release executable differs from the prebuilt executable.'
}
$checksums = foreach ($file in @($releaseBinary, $zip)) {
    (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $file)
}
$checksums | Set-Content -LiteralPath (Join-Path $artifactDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Packaged $Version from the existing executable. SHA-256: $actual"
