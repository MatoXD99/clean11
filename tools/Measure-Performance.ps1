param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$LogPath,
    [int]$Runs=8,
    [switch]$AlreadyHidden,
    [string]$Output=(Join-Path $PSScriptRoot '../artifacts/performance/measurement.json')
)
$ErrorActionPreference='Stop'
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if($Runs -lt 1 -or $Runs -gt 50){throw 'Runs must be between 1 and 50.'}
function Launch([string]$Argument){$options=@{FilePath=$Executable;WindowStyle='Hidden';Wait=$true};if($Argument){$options.ArgumentList=$Argument};Start-Process @options}
if(!$AlreadyHidden){Launch '--hide'}
Start-Sleep -Milliseconds 250
Launch '';Start-Sleep -Milliseconds 1400
Launch '';Start-Sleep -Milliseconds 1000
$offset=if(Test-Path -LiteralPath $LogPath){@(Get-Content -LiteralPath $LogPath).Count}else{0}
for($run=0;$run -lt $Runs;$run++){
    Launch '';Start-Sleep -Milliseconds 1400
    Launch '';Start-Sleep -Milliseconds 1000
}
$lines=@(Get-Content -LiteralPath $LogPath | Select-Object -Skip $offset)
$preparation=@();$frames=@()
function Number([string]$Value){[double]::Parse($Value.TrimEnd('.',',').Replace(',','.'),[Globalization.CultureInfo]::InvariantCulture)}
foreach($line in $lines){
    if($line -match 'Prepared complete scene before show; total ms=([0-9.,]+)'){$preparation+=Number $Matches[1]}
    if($line -match 'Compositor motion: target Hz=([0-9.,]+), submitted updates=(\d+), duration ms=([0-9.,]+), median gap ms=([0-9.,]+), p95 gap ms=([0-9.,]+), max gap ms=([0-9.,]+)'){
        $frames += [pscustomobject]@{DisplayHz=Number $Matches[1];SubmittedUpdates=[int]$Matches[2];DurationMs=Number $Matches[3];MedianGapMs=Number $Matches[4];P95GapMs=Number $Matches[5];MaxGapMs=Number $Matches[6]}
    }
}
if($preparation.Count -ne $Runs -or $frames.Count -ne ($Runs*2)){throw "Incomplete benchmark: $($preparation.Count) openings and $($frames.Count) transitions; expected $Runs and $($Runs*2). Report not published."}
$report=[pscustomobject]@{Schema=1;Kind='Application update cadence, not displayed FPS';Runs=$Runs;PreparationMs=$preparation;Transitions=$frames}
$parent=Split-Path -Parent ([IO.Path]::GetFullPath($Output));New-Item -ItemType Directory -Force -Path $parent | Out-Null
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Output -Encoding UTF8
$report | Select-Object Runs,@{Name='MedianPreparationMs';Expression={($preparation|Sort-Object)[[int][Math]::Floor($preparation.Count/2)]}},Kind
