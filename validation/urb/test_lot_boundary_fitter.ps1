$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'src/Urb/LotBoundaryProfileFitter.cs')
function New-Case([double]$l,[double]$road,[double]$r) {
    $q = [Buraqueira_Urb.LotBoundaryFitRequest]::new()
    $q.ExistingLeft=$l; $q.ExistingRoad=$road; $q.ExistingRight=$r
    $q.SidewalkLeft=$true; $q.SidewalkRight=$true
    $q.MinimumLeft=1.5; $q.MinimumRight=1.5
    $q.LaneCount=2; $q.LanePreferred=3; $q.LaneMinimum=2.5
    $q.MedianPreferred=1; $q.MedianMinimum=0.6
    $q.PedestrianMinimum=3
    return $q
}
function Check([string]$name,[Buraqueira_Urb.LotBoundaryFitRequest]$q,[bool]$expected) {
    $v=[Buraqueira_Urb.LotBoundaryProfileFitter]::Fit($q)
    if($v.Success -ne $expected){throw "$name status unexpected: $($v.Diagnostic)"}
    if($v.Success){
        $sum=$v.LeftWidth+$v.RoadWidth+$v.RightWidth
        if([Math]::Abs($sum-$v.AvailableWidth) -gt 1e-7){throw "$name lot width exceeded"}
        if(-not $q.Pedestrian -and $q.SidewalkLeft -and $v.LeftWidth+1e-7 -lt $q.MinimumLeft){throw "$name left minimum"}
        if(-not $q.Pedestrian -and $q.SidewalkRight -and $v.RightWidth+1e-7 -lt $q.MinimumRight){throw "$name right minimum"}
        if(-not $q.Pedestrian -and $v.LaneWidth+1e-7 -lt $q.LaneMinimum){throw "$name lane minimum"}
    }
    Write-Output "$name PASS: $($v.Diagnostic)"
    return $v
}
$q=New-Case 1.5 11 1.5; $null=Check A_wide $q $true
$q=New-Case .8 6.4 .8; $v=Check B_narrow $q $true; if([Math]::Abs($v.LeftWidth-1.5)-gt 1e-7 -or [Math]::Abs($v.RightWidth-1.5)-gt 1e-7){throw 'B sidewalks not reserved'}
$q=New-Case .8 6.4 .8; $q.SidewalkRight=$false; $null=Check C_left_only $q $true
$q=New-Case .8 6.4 .8; $q.SidewalkLeft=$false; $null=Check D_right_only $q $true
$q=New-Case .8 6.4 .8; $q.SidewalkLeft=$false; $q.SidewalkRight=$false; $null=Check E_no_sidewalk $q $true
$q=New-Case .3 3.4 .3; $q.Pedestrian=$true; $null=Check F_pedestrian $q $true
$q=New-Case 1.5 8 1.5; $q.HasMedian=$true; $q.MedianRequired=$true; $v=Check G_median $q $true; if($v.MedianWidth -lt .6){throw 'G median minimum'}
$q=New-Case 1.5 5 1.5; $null=Check H_exact $q $true
$q=New-Case 1.5 0 1.5; $null=Check I_road_missing $q $false
$q=New-Case 1 2 1; $null=Check J_total_insufficient $q $false
$q=New-Case 1.5 5.1 1.5; $q.HasMedian=$true; $q.MedianRequired=$false; $v=Check Optional_median $q $true; if(-not $v.MedianRemoved){throw 'optional median not reported'}

Write-Output "LotBoundaryProfileFitter: 11/11 PASS"
