$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -Path (Join-Path $root 'src/Urb/AdaptiveSectionPlanner.cs')
$checks = 0
function Assert([bool]$condition, [string]$name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:checks++
}
function Event([double]$station, [int]$side, [int]$vertex, [string]$reason='LOT_CORNER') {
    $e = [Buraqueira_Urb.SectionEvent]::new()
    $e.Station=$station; $e.Side=$side; $e.VertexId=$vertex
    $e.BoundaryDistance=5
    $e.SourceId='Block:1'; $e.Reason=$reason; $e.Hard=$true
    return $e
}
$p=[Buraqueira_Urb.AdaptiveSectionPlanner]
$a=$p::Build(100, 20, .25, [Buraqueira_Urb.SectionEvent[]]@())
Assert ($a.Support -eq 4 -and $a.Stations.Count -eq 6) 'A straight road gets support samples'
Assert ((@($a.Stations | Where-Object { $_.Station -gt 0 -and $_.Station -lt 100 } | ForEach-Object Station) -join ',') -eq '20,40,60,80') 'J support captures midpoint bottleneck'
$b=$p::Build(100,20,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 33 1 1)))
Assert (@($b.Stations | Where-Object { $_.Station -eq 33 -and $_.Required -and $_.Hard }).Count -eq 1) 'B left corner required'
$c=$p::Build(100,20,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 25 1 1),(Event 45 1 2),(Event 70 1 3)))
Assert ($c.GeometryRequired -eq 5) 'C several corners retained'
Assert ($p::ClassifyVertex(0,0,5,0,10,0,12,.2) -eq 'COLLINEAR_OR_CURVE_SAMPLE') 'D collinear suppressed'
Assert ($p::ClassifyVertex(0,0,1,0,2,.05,12,.2) -eq 'COLLINEAR_OR_CURVE_SAMPLE') 'E dense curve sample suppressed'
Assert ($p::ClassifyVertex(0,0,5,0,5,5,12,.2) -eq 'LOT_CORNER') 'F significant left turn'
$g=$p::Build(30,10,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 12 -1 1)))
Assert (@($g.Stations | Where-Object { $_.Station -eq 12 -and $_.Side -eq -1 }).Count -eq 1) 'G right-only event retained'
$h=$p::Build(30,10,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 12 1 1),(Event 12.18 -1 2)))
Assert ($h.Merged -eq 1 -and $h.GeometryRequired -eq 3) 'H nearby opposite events merged'
$i=$p::Build(30,10,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 12 1 1),(Event 12.18 1 2)))
Assert ($i.GeometryRequired -eq 3) 'I nearby same-side duplicates merged'
$far=Event 12.18 1 2; $far.BoundaryDistance=6
$distinct=$p::Build(30,10,.25,[Buraqueira_Urb.SectionEvent[]]@((Event 12 1 1),$far))
Assert ($distinct.GeometryRequired -eq 4) 'distinct lateral jump retained'
$noisy=$p::ClassifyVertex(0,0,.05,0,.08,.02,12,.2)
Assert ($noisy -eq 'MINOR_CHANGE') 'short noisy edge suppressed'
Write-Output "AdaptiveSectionPlanner: $checks/$checks PASS"

