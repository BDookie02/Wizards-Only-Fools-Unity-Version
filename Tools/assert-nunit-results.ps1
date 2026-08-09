param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

$resolvedPath = [System.IO.Path]::GetFullPath($Path)
if ([System.IO.Path]::GetPathRoot($resolvedPath) -ne 'D:\') {
    throw "NUnit results must stay on D:. Refusing to read $resolvedPath"
}

if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
    throw "NUnit result file was not created: $resolvedPath"
}

if ((Get-Item -LiteralPath $resolvedPath).Length -eq 0) {
    throw "NUnit result file is empty: $resolvedPath"
}

$readerSettings = [System.Xml.XmlReaderSettings]::new()
$readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$readerSettings.XmlResolver = $null

$reader = [System.Xml.XmlReader]::Create($resolvedPath, $readerSettings)
try {
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    $document.Load($reader)
}
finally {
    $reader.Dispose()
}

$testRun = $document.DocumentElement
if ($null -eq $testRun -or $testRun.LocalName -ne 'test-run') {
    throw "NUnit result root must be <test-run>: $resolvedPath"
}

function Get-RequiredCount {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$Element,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $text = $Element.GetAttribute($Name)
    $value = 0
    $parsed = [int]::TryParse(
        $text,
        [System.Globalization.NumberStyles]::Integer,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$value)
    if (-not $parsed -or $value -lt 0) {
        throw "NUnit result attribute '$Name' is missing or invalid in $resolvedPath"
    }

    return $value
}

$total = Get-RequiredCount -Element $testRun -Name 'total'
$passed = Get-RequiredCount -Element $testRun -Name 'passed'
$failed = Get-RequiredCount -Element $testRun -Name 'failed'
$inconclusive = Get-RequiredCount -Element $testRun -Name 'inconclusive'
$skipped = Get-RequiredCount -Element $testRun -Name 'skipped'
$result = $testRun.GetAttribute('result')

if ($total -eq 0) {
    throw "NUnit reported zero tests; an empty run is not a pass: $resolvedPath"
}

if (($passed + $failed + $inconclusive + $skipped) -ne $total) {
    throw "NUnit result counts do not reconcile (total=$total passed=$passed failed=$failed inconclusive=$inconclusive skipped=$skipped): $resolvedPath"
}

if ($result -ne 'Passed' -or $failed -ne 0 -or $inconclusive -ne 0) {
    throw "NUnit run did not pass cleanly (result=$result total=$total passed=$passed failed=$failed inconclusive=$inconclusive skipped=$skipped): $resolvedPath"
}

Write-Output "[WOF-AUTOMATION] NUNIT_RESULTS_VALIDATED total=$total passed=$passed skipped=$skipped path=$resolvedPath"
