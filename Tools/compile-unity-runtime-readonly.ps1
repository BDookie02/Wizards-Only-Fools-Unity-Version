param(
    [string]$ProjectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity',
    [string]$ResponseFile = 'Library\Bee\artifacts\1900b0aP.dag\WOF.Runtime.rsp',
    [switch]$IncludeTreeHouseTests,
    [switch]$IncludeGraveyardTests,
    [switch]$IncludeDesertTests,
    [switch]$IncludeChicagoTests,
    [switch]$IncludeSwampTests
)

$ErrorActionPreference = 'Stop'
$resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if (-not $resolvedProjectRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The Unity project must stay on D:. Refusing $resolvedProjectRoot"
}

$resolvedResponseFile = if ([System.IO.Path]::IsPathRooted($ResponseFile)) {
    [System.IO.Path]::GetFullPath($ResponseFile)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $resolvedProjectRoot $ResponseFile))
}
$sentinelPath = Join-Path $PSScriptRoot 'WofReadonlyCompileSentinel.cs'
$dotnet = 'D:\UnityEditors\6000.3.21f1\Editor\Data\NetCoreRuntime\dotnet.exe'
$compiler = 'D:\UnityEditors\6000.3.21f1\Editor\Data\DotNetSdkRoslyn\csc.dll'
foreach ($requiredPath in @($resolvedResponseFile, $sentinelPath, $dotnet, $compiler)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Read-only compiler dependency was not found: $requiredPath"
    }
}

$responseLines = Get-Content -LiteralPath $resolvedResponseFile
$responseSources = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($line in $responseLines) {
    if ($line -match '^"(.+\.cs)"$') {
        $source = ([regex]::Match($line, '^"(.+\.cs)"$')).Groups[1].Value
        $fullSource = if ([System.IO.Path]::IsPathRooted($source)) {
            [System.IO.Path]::GetFullPath($source)
        } else {
            [System.IO.Path]::GetFullPath((Join-Path $resolvedProjectRoot $source))
        }
        [void]$responseSources.Add($fullSource)
    }
}
$newSources = @(
    Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Assets\WOF\Runtime') `
        -Filter '*.cs' -File -Recurse |
        Where-Object { -not $responseSources.Contains($_.FullName) } |
        Select-Object -ExpandProperty FullName
)
$testSources = @()
$testReferences = @()
if ($IncludeTreeHouseTests) {
    $treeHouseTest = Join-Path $resolvedProjectRoot 'Assets\WOF\Tests\EditMode\WofTreeHouseTraversalRulesTests.cs'
    $nunitReference = Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Library\PackageCache') `
        -Filter 'nunit.framework.dll' -File -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not (Test-Path -LiteralPath $treeHouseTest -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($nunitReference)) {
        throw 'Tree-house EditMode test source or NUnit reference was not found.'
    }
    $testSources = @($treeHouseTest)
    $testReferences = @($nunitReference)
}
if ($IncludeGraveyardTests) {
    $graveyardTest = Join-Path $resolvedProjectRoot 'Assets\WOF\Tests\EditMode\WofGraveyardTraversalRulesTests.cs'
    $nunitReference = Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Library\PackageCache') `
        -Filter 'nunit.framework.dll' -File -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not (Test-Path -LiteralPath $graveyardTest -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($nunitReference)) {
        throw 'Graveyard EditMode test source or NUnit reference was not found.'
    }
    $testSources += $graveyardTest
    if ($testReferences.Count -eq 0) {
        $testReferences = @($nunitReference)
    }
}
if ($IncludeDesertTests) {
    $desertTest = Join-Path $resolvedProjectRoot 'Assets\WOF\Tests\EditMode\WofDesertTraversalRulesTests.cs'
    $nunitReference = Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Library\PackageCache') `
        -Filter 'nunit.framework.dll' -File -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not (Test-Path -LiteralPath $desertTest -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($nunitReference)) {
        throw 'Desert EditMode test source or NUnit reference was not found.'
    }
    $testSources += $desertTest
    if ($testReferences.Count -eq 0) {
        $testReferences = @($nunitReference)
    }
}
if ($IncludeChicagoTests) {
    $chicagoTest = Join-Path $resolvedProjectRoot 'Assets\WOF\Tests\EditMode\WofChicagoTraversalRulesTests.cs'
    $nunitReference = Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Library\PackageCache') `
        -Filter 'nunit.framework.dll' -File -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not (Test-Path -LiteralPath $chicagoTest -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($nunitReference)) {
        throw 'Chicago EditMode test source or NUnit reference was not found.'
    }
    $testSources += $chicagoTest
    if ($testReferences.Count -eq 0) {
        $testReferences = @($nunitReference)
    }
}
if ($IncludeSwampTests) {
    $swampTest = Join-Path $resolvedProjectRoot 'Assets\WOF\Tests\EditMode\WofSwampTraversalRulesTests.cs'
    $nunitReference = Get-ChildItem -LiteralPath (Join-Path $resolvedProjectRoot 'Library\PackageCache') `
        -Filter 'nunit.framework.dll' -File -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not (Test-Path -LiteralPath $swampTest -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($nunitReference)) {
        throw 'Swamp EditMode test source or NUnit reference was not found.'
    }
    $testSources += $swampTest
    if ($testReferences.Count -eq 0) {
        $testReferences = @($nunitReference)
    }
}

# Roslyn reports diagnostics for the whole compilation before emitting. A
# deliberate, uniquely named preprocessor error guarantees that it never opens
# the response file's D-drive output paths. The gate succeeds only when that
# sentinel is the sole compiler error, which proves the current runtime sources
# parsed and type-checked without mutating Library, Temp, logs, C:, or a build.
$compilerArguments = @(
    "`"$compiler`"",
    "@`"$resolvedResponseFile`""
) + @($newSources | ForEach-Object { "`"$_`"" }) +
    @($testReferences | ForEach-Object { "/r:`"$_`"" }) +
    @($testSources | ForEach-Object { "`"$_`"" }) +
    @("`"$sentinelPath`"")

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $dotnet
$startInfo.Arguments = $compilerArguments -join ' '
$startInfo.WorkingDirectory = $resolvedProjectRoot
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
try {
    if (-not $process.Start()) {
        throw 'Unity Roslyn compiler process did not start.'
    }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    if (-not $process.WaitForExit(120000)) {
        $process.Kill()
        throw 'Read-only WOF.Runtime compile exceeded two minutes.'
    }

    $diagnosticText = ($stdout, $stderr) -join [Environment]::NewLine
    $errorLines = @(
        $diagnosticText -split '\r?\n' |
            Where-Object { $_ -match '(?i)\berror\s+CS\d+:' }
    )
    $sentinelErrors = @(
        $errorLines | Where-Object {
            $_ -match 'error CS1029:' -and $_ -match 'WOF_READONLY_COMPILE_SENTINEL'
        }
    )
    $unexpectedErrors = @(
        $errorLines | Where-Object {
            -not ($_ -match 'error CS1029:' -and $_ -match 'WOF_READONLY_COMPILE_SENTINEL')
        }
    )
    if ($sentinelErrors.Count -ne 1 -or $unexpectedErrors.Count -gt 0) {
        if (-not [string]::IsNullOrWhiteSpace($diagnosticText)) {
            Write-Output $diagnosticText.TrimEnd()
        }
        throw "Read-only WOF.Runtime compile failed: sentinelErrors=$($sentinelErrors.Count) unexpectedErrors=$($unexpectedErrors.Count)."
    }
    if ($process.ExitCode -eq 0) {
        throw 'Read-only compiler unexpectedly emitted instead of stopping at the sentinel.'
    }

    Write-Output "Read-only WOF.Runtime compile passed: baselineSources=$($responseSources.Count) newSources=$($newSources.Count) targetedTests=$($testSources.Count) expectedSentinelErrors=1 diskWrites=0"
}
finally {
    $process.Dispose()
}
