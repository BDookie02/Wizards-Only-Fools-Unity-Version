param(
    [string]$ProjectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity',
    [string]$TestFilter = ''
)

$ErrorActionPreference = 'Stop'
$resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if (-not $resolvedProjectRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The Unity project must stay on D:. Refusing $resolvedProjectRoot"
}

$unityConsole = 'D:\UnityEditors\6000.3.21f1\Editor\Unity.com'
$taskRoot = 'D:\tmp\wof-unity'
$logRoot = Join-Path $taskRoot 'logs'
$resultPath = Join-Path $logRoot 'editmode-results.xml'
$performancePath = Join-Path $logRoot 'editmode-performance-results.json'
$logPath = Join-Path $logRoot 'editmode-tests.log'
$validatorPath = Join-Path $PSScriptRoot 'assert-nunit-results.ps1'
foreach ($requiredPath in @($unityConsole, $validatorPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required test dependency is missing: $requiredPath"
    }
}

New-Item -ItemType Directory -Force -Path $taskRoot, $logRoot | Out-Null
foreach ($artifact in @($resultPath, $performancePath, $logPath)) {
    $resolvedArtifact = [System.IO.Path]::GetFullPath($artifact)
    if (-not $resolvedArtifact.StartsWith('D:\tmp\wof-unity\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Test artifacts must stay under D:\tmp\wof-unity. Refusing $resolvedArtifact"
    }
    if (Test-Path -LiteralPath $resolvedArtifact -PathType Leaf) {
        Remove-Item -LiteralPath $resolvedArtifact -Force
    }
}

$env:TEMP = $taskRoot
$env:TMP = $taskRoot
$env:UPM_CACHE_ROOT = 'D:\UnityPackageCache'
$env:UPM_NPM_CACHE_PATH = 'D:\UnityPackageCache\npm'
$env:UPM_CACHE_PATH = 'D:\UnityPackageCache\packages'
$env:UPM_GIT_LFS_CACHE_PATH = 'D:\UnityPackageCache\git-lfs'

Push-Location 'D:\'
try {
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-accept-apiupdate',
        '-projectPath', $resolvedProjectRoot,
        '-giCustomCacheLocation', 'D:\UnityEditorProfile\GICache',
        '-logFile', $logPath,
        '-runTests',
        '-testPlatform', 'EditMode',
        '-testResults', $resultPath,
        '-perfTestResults', $performancePath
    )
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('-testFilter', $TestFilter)
    }
    & $unityConsole @arguments
    $unityExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($unityExitCode -ne 0) {
    $tail = Get-Content -LiteralPath $logPath -Tail 160 -ErrorAction SilentlyContinue
    throw "Unity EditMode tests failed with exit code $unityExitCode.`n$($tail -join [Environment]::NewLine)"
}

& $validatorPath -Path $resultPath
