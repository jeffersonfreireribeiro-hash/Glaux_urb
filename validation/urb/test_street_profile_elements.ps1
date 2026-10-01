$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[System.IO.File]::ReadAllText((Join-Path $root 'src/Urb/StreetProfileModel.cs'))
$curveStub='namespace Rhino.Geometry { public class Curve { public bool IsValid => true; public double GetLength() => 0; } public struct Point3d { public double X,Y,Z; } }'
Add-Type -TypeDefinition ($source+"`n"+$curveStub)
$n=0
function Check([bool]$condition,[string]$name){if(-not $condition){throw "FAIL $name"};$script:n++}
function Element([string]$kind,[double]$pref,[double]$min,[bool]$required=$true,[int]$direction=0){
 $e=[Buraqueira_Urb.StreetProfileElement]::new();$e.Id=[guid]::NewGuid();$e.Type=$kind
 $e.PreferredWidth=$pref;$e.MinimumWidth=$min;$e.MaximumWidth=$pref;$e.Required=$required;$e.Direction=$direction;return $e
}
function Profile($elements){$p=[Buraqueira_Urb.StreetProfile]::new();$p.Street='Rua Teste';$p.Type='Custom';foreach($e in $elements){$p.Elements.Add($e)};return $p}
$f=[Buraqueira_Urb.StreetProfileElementFitter]
$road=Profile @((Element 'Road' 8 5));$r=$f::Fit($road,8)
Check ($r.Success -and $r.RoadWidth -eq 8 -and $r.Profile.Elements.Count -eq 1) 'new road profile'
$r=$f::Fit($road,10)
Check (-not $r.Success -and $r.Reason -eq 'EXCESS_WIDTH' -and $r.Excess -eq 2) 'width above maximum reported'
$list=@((Element 'Sidewalk' 1.5 1.5),(Element 'Tree Strip' 1 0 $false),(Element 'Lane' 3 2.5 $true 1),(Element 'Lane' 3 2.5 $true -1),(Element 'Sidewalk' 1.5 1.5))
$p=Profile $list;$r=$f::Fit($p,10)
Check ($r.Success -and $r.Profile.Elements.Count -eq 5) 'ordered simple profile'
Check (($r.Profile.Elements | ForEach-Object Type) -join ',' -eq 'Sidewalk,Tree Strip,Lane,Lane,Sidewalk') 'order preserved'
Check ((($r.Profile.Elements | ForEach-Object Id) -join ',') -eq (($p.Elements | ForEach-Object Id) -join ',')) 'IDs preserved'
$r=$f::Fit($p,8)
Check ($r.Success -and $r.Profile.Elements[1].Status -eq 'SUPPRESSED' -and $r.Profile.Elements[1].FittedWidth -eq 0) 'optional tree strip explicitly suppressed'
Check ($r.Profile.Elements[0].FittedWidth -ge 1.5 -and $r.Profile.Elements[4].FittedWidth -ge 1.5) 'sidewalk minima preserved'
$p2=Profile @((Element 'Sidewalk' 1.5 1.5),(Element 'Lane' 3 2.5),(Element 'Median' 1.2 0 $false),(Element 'Lane' 3 2.5),(Element 'Sidewalk' 1.5 1.5))
$r=$f::Fit($p2,8)
Check ($r.Success -and $r.Profile.Elements[2].Status -eq 'SUPPRESSED' -and $r.Profile.Elements.Count -eq 5) 'optional median slot retained'
$r=$f::Fit($p2,7)
Check (-not $r.Success -and $r.Reason -eq 'INSUFFICIENT_PUBLIC_WIDTH' -and $r.Deficit -gt 0) 'absolute minimum conflict'
$ped=Profile @((Element 'Pedestrian' 6 3));$r=$f::Fit($ped,6)
Check ($r.Success -and $r.RoadWidth -eq 6 -and $r.LeftWidth -eq 0 -and $r.RightWidth -eq 0) 'pedestrian profile'
$p3=Profile @((Element 'Sidewalk' 2 1));$r=$f::Fit($p3,4)
Check (-not $r.Success -and $r.Reason -eq 'ROAD_DOMAIN_REQUIRED') 'road domain required'
$oneWay=Profile @((Element 'Lane' 3 2.5 $true 1),(Element 'Lane' 3 2.5 $true -1));$oneWay.StreetType='One Way'
Check ((@([Buraqueira_Urb.StreetProfileTypeValidator]::Validate($oneWay))).Count -eq 1) 'one-way contradiction reported'
Check ($oneWay.Elements.Count -eq 2) 'contradiction does not alter elements'
$pedWrong=Profile @((Element 'Lane' 3 2.5));$pedWrong.StreetType='Pedestrian'
Check ((@([Buraqueira_Urb.StreetProfileTypeValidator]::Validate($pedWrong))).Count -eq 1) 'pedestrian contradiction reported'
$bounded=Profile @((Element 'Sidewalk' 3 1.5),(Element 'Lane' 3.5 2.5),(Element 'Lane' 3.5 2.5),(Element 'Sidewalk' 3 1.5))
$r=$f::Fit($bounded,10.5)
Check ($r.Success -and [math]::Abs((($r.Profile.Elements | Measure-Object -Property FittedWidth -Sum).Sum)-10.5) -lt 1e-7) 'domain fit uses available width'
Check ((@($r.Profile.Elements | Where-Object { $_.FittedWidth -lt $_.MinimumWidth -or $_.FittedWidth -gt $_.MaximumWidth })).Count -eq 0) 'all fitted widths inside domains'
$fixed=Profile @((Element 'Sidewalk' 2 2),(Element 'Road' 3 3));$r=$f::Fit($fixed,5)
Check ($r.Success -and $r.Profile.Elements[0].FittedWidth -eq 2) 'fixed domain remains fixed'
$gap=Profile @((Element 'Road' 3 3),(Element 'Median' 2 2 $false));$r=$f::Fit($gap,4)
Check (-not $r.Success -and $r.Reason -eq 'WIDTH_DOMAIN_GAP') 'optional fixed-band gap reported'
$assignmentProfile=Profile @((Element 'Road' 3 2));$assignmentProfile.StreetName='Rua Amazonas'
$items=@()
foreach($id in @('id:101','id:102','id:103')){
 $item=[Buraqueira_Urb.RawGisStreetItem]::new();$item.Name='  RUA   AMAZONAS ';$item.SourceId=$id;$items+= $item
}
$other=[Buraqueira_Urb.RawGisStreetItem]::new();$other.Name='Rua Pará';$other.SourceId='id:104';$items+=$other
$assigned=[Buraqueira_Urb.StreetProfileAssignmentService]::Assign([Buraqueira_Urb.RawGisStreetItem[]]$items,[Buraqueira_Urb.StreetProfile[]]@($assignmentProfile),$true)
Check ($assigned.MatchedCount -eq 3 -and $assigned.UnmatchedCount -eq 1) 'one profile matches three GIS segments'
Check ((($assigned.Profiled | ForEach-Object StreetID) -join ',') -eq 'id:101,id:102,id:103') 'feature identifiers preserved'
$duplicate=Profile @((Element 'Road' 3 2));$duplicate.StreetName='RUA AMAZONAS'
$ambiguous=[Buraqueira_Urb.StreetProfileAssignmentService]::Assign([Buraqueira_Urb.RawGisStreetItem[]]$items,[Buraqueira_Urb.StreetProfile[]]@($assignmentProfile,$duplicate),$true)
Check ($ambiguous.MatchedCount -eq 0 -and $ambiguous.AmbiguousCount -eq 3) 'competing profiles reported'
$unnamed=[Buraqueira_Urb.RawGisStreetItem]::new();$unnamed.SourceId='id:105'
$empty=[Buraqueira_Urb.StreetProfileAssignmentService]::Assign([Buraqueira_Urb.RawGisStreetItem[]]@($unnamed),[Buraqueira_Urb.StreetProfile[]]@($assignmentProfile),$true)
Check ($empty.UnmatchedCount -eq 1 -and $empty.Profiled.Count -eq 0) 'missing street name stays unmatched'
Check ([Buraqueira_Urb.StreetNameNormalizer]::Normalize(' Rua   Amazonas ') -eq 'RUA AMAZONAS') 'name normalization is deterministic'
$section=[Buraqueira_Urb.ProfiledSection]::new();$section.StreetID='id:101';$section.StreetName='Rua Amazonas';$section.StreetProfile=$assignmentProfile;$section.StreetPathIndex=2;$section.StationIndex=7
Check ($section.StreetProfile -eq $assignmentProfile -and $section.StreetID -eq 'id:101' -and $section.StationIndex -eq 7) 'profiled section carries profile and street identity'
Write-Output "StreetProfileElementFitter: $n/$n PASS"
