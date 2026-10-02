$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Add-Type -Path (Join-Path $root 'src\Urb\CurbRunTopology.cs')
$t = [Buraqueira_Urb.CurbRunTopology]
$checks = 0
function Check([bool]$condition, [string]$name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:checks++
}
Check ($null -eq $t::BreakReason(0,1,1,0,1,0)) 'straight run'
Check ($null -eq $t::BreakReason(1,2,1,0,0.98,0.2)) 'curved run'
Check ($t::BreakReason(1,2,1,0,-1,0) -eq 'SIDE_FLIP_OR_DIRECTION_BREAK') 'reversed source direction'
Check ($t::BreakReason(1,3,1,0,1,0) -eq 'MISSING_SECTION') 'intersection or missing section is open'
Check ($t::BreakReason(1,1,1,0,1,0) -eq 'DUPLICATE_OR_ORDER_ERROR') 'duplicate station'
Check ($t::BreakReason(1,2,0,0,1,0) -eq 'INVALID_DIRECTION') 'invalid side direction'
'CurbRunTopology: {0}/{0} PASS' -f $checks
