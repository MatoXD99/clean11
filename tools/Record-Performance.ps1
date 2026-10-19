param([ValidateSet('Start','Stop')][string]$Action='Start',[string]$Output=(Join-Path $PSScriptRoot '../artifacts/performance/trace.etl'))
$ErrorActionPreference='Stop'
if($Action -eq 'Start'){
    & wpr -start CPU -start GPU -start DesktopComposition -start "$PSScriptRoot/Clean11.wprp!Clean11" -filemode
    if($LASTEXITCODE -ne 0){throw 'Windows denied tracing. Run this script from an elevated terminal; no trace was confirmed.'}
}else{
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([IO.Path]::GetFullPath($Output))) | Out-Null
    & wpr -stop $Output
    if($LASTEXITCODE -ne 0){throw 'Trace export failed.'}
}
