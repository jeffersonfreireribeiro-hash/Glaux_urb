param(
    [string]$Definition = (Join-Path $PSScriptRoot 'fixtures\picui-pipeline-fixture.gh'),
    [string]$GhIo = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll'
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $GhIo
$archive = [GH_IO.Serialization.GH_Archive]::new()
if (-not $archive.ReadFromFile((Resolve-Path -LiteralPath $Definition).Path)) { throw 'Cannot read GH definition' }
$objects = $archive.GetRootNode.Chunks[0].Chunks | Where-Object Name -eq 'DefinitionObjects'
function Component([string]$name) {
    $match = @($objects.Chunks | Where-Object { ($_.Items | Where-Object Name -eq 'Name')._string -eq $name })
    if ($match.Count -ne 1) { throw "Expected exactly one $name" }
    return $match[0]
}
function Port($component, [string]$kind, [int]$index) {
    $match = @($component.Chunks[0].Chunks | Where-Object { $_.Name -eq $kind -and $_.Index -eq $index })
    if ($match.Count -ne 1) { throw "Missing $kind $index" }
    return $match[0]
}
function GuidItem($chunk, [string]$name) {
    $item = @($chunk.Items | Where-Object Name -eq $name)
    if ($item.Count -ne 1) { throw "Expected one $name in $($chunk.Name) $($chunk.Index)" }
    return $item[0]._guid
}
$assign = Component 'Street Profile Assignment'
$trans = Component 'Road Transversals from GIS'
$fit = Component 'Street Profile Fitting'
$profiled = GuidItem (Port $assign 'param_output' 0) 'InstanceGuid'
$transAxis = GuidItem (Port $trans 'param_input' 0) 'Source'
$sections = GuidItem (Port $trans 'param_output' 20) 'InstanceGuid'
$fitSections = GuidItem (Port $fit 'param_input' 6) 'Source'
$points = GuidItem (Port $trans 'param_output' 1) 'InstanceGuid'
$fitPoints = GuidItem (Port $fit 'param_input' 1) 'Source'
if ($transAxis -ne $profiled) { throw 'Axis is not connected to Assignment.Profiled' }
if ($fitSections -ne $sections) { throw 'Fitting.Sections is not connected to Transversals.Sections' }
if ($fitPoints -ne $points) { throw 'Fitting.Pts is not connected to Transversals.Pts' }
'GH archive pipeline connections: 3/3 PASS (archive only; not a Grasshopper runtime test)'
