param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$windowsRoot = Join-Path $projectRoot 'Builds\Windows'
$androidApk = Join-Path $projectRoot 'Builds\Android\WizardsOnlyFools.apk'
$releaseRoot = Join-Path $projectRoot 'Builds\Releases'
$windowsZip = Join-Path $releaseRoot "WizardsOnlyFools-Windows-Unity-Version-v$Version.zip"

foreach ($requiredPath in @($windowsRoot, $androidApk, $releaseRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required release input is missing: $requiredPath"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path -LiteralPath $windowsZip) {
    Remove-Item -LiteralPath $windowsZip -Force
}

$archive = [System.IO.Compression.ZipFile]::Open(
    $windowsZip,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -LiteralPath $windowsRoot -Recurse -File |
        Where-Object { $_.FullName -notlike '*BurstDebugInformation_DoNotShip*' }
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($windowsRoot.Length).TrimStart('\').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $relativePath,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

function Get-CompleteArchiveReceipt {
    param([Parameter(Mandatory = $true)][string]$Path)

    $entryCount = 0
    [long]$uncompressedBytes = 0
    $buffer = New-Object byte[] 131072
    $readArchive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in $readArchive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $entryCount++
            $stream = $entry.Open()
            try {
                while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $uncompressedBytes += $read
                }
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $readArchive.Dispose()
    }

    [pscustomobject]@{
        path = $Path
        entries = $entryCount
        uncompressedBytes = $uncompressedBytes
        length = (Get-Item -LiteralPath $Path).Length
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$apkArchive = [System.IO.Compression.ZipFile]::OpenRead($androidApk)
try {
    $nativeLibraries = @($apkArchive.Entries | Where-Object {
        $_.FullName -match '^lib/[^/]+/[^/]+\.so$'
    })
    $abis = @($nativeLibraries | ForEach-Object {
        ($_.FullName -split '/')[1]
    } | Sort-Object -Unique)
}
finally {
    $apkArchive.Dispose()
}

[pscustomobject]@{
    version = $Version
    windows = Get-CompleteArchiveReceipt -Path $windowsZip
    android = Get-CompleteArchiveReceipt -Path $androidApk
    androidNativeLibraryCount = $nativeLibraries.Count
    androidAbis = $abis
} | ConvertTo-Json -Depth 5
